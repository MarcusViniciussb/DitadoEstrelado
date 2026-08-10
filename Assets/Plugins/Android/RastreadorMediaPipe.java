package br.edu.ifto.ditadoestrelado;

import android.content.Context;
import android.graphics.Bitmap;

import com.google.mediapipe.framework.image.BitmapImageBuilder;
import com.google.mediapipe.framework.image.MPImage;
import com.google.mediapipe.tasks.components.containers.NormalizedLandmark;
import com.google.mediapipe.tasks.core.BaseOptions;
import com.google.mediapipe.tasks.core.Delegate;
import com.google.mediapipe.tasks.vision.core.RunningMode;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarker;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarkerResult;

import java.io.File;
import java.io.FileInputStream;
import java.nio.ByteBuffer;
import java.util.List;

/**
 * Ponte entre o aplicativo e o MediaPipe do Android.
 *
 * E o mesmo motor de rastreamento de maos que o computador usa por fora,
 * pelo Python, aqui rodando dentro do proprio aplicativo. O Unity manda a
 * imagem ja endireitada e recebe de volta os 21 pontos da mao.
 *
 * O modo LIVE_STREAM trabalha em segundo plano: o envio do quadro nao trava
 * o jogo e o resultado fica guardado ate alguem vir buscar.
 */
public class RastreadorMediaPipe {

    private static final int PONTOS  = 21;
    private static final int VALORES = PONTOS * 3;

    private HandLandmarker detector;

    // Tres imagens em rodizio: o MediaPipe ainda pode estar lendo a anterior
    // quando o quadro seguinte chega
    private final Bitmap[] quadros = new Bitmap[3];
    private int  proximoQuadro = 0;
    private int  larguraQuadro = 0, alturaQuadro = 0;
    private long relogio = 0;

    private final Object  trava    = new Object();
    private final float[] leitura  = new float[VALORES];
    private final float[] entregue = new float[2 + VALORES];
    private float confianca = 0f;
    private int   versao    = 0;

    private volatile String ultimoErro = "";

    /** Carrega o modelo e prepara o detector. Devolve true se deu certo. */
    public boolean iniciar(Context contexto, String caminhoDoModelo) {
        try {
            ByteBuffer modelo = lerModelo(caminhoDoModelo);

            BaseOptions base = BaseOptions.builder()
                    .setDelegate(Delegate.CPU)
                    .setModelAssetBuffer(modelo)
                    .build();

            HandLandmarker.HandLandmarkerOptions opcoes =
                    HandLandmarker.HandLandmarkerOptions.builder()
                            .setBaseOptions(base)
                            .setRunningMode(RunningMode.LIVE_STREAM)
                            .setNumHands(1)
                            .setMinHandDetectionConfidence(0.5f)
                            .setMinHandPresenceConfidence(0.5f)
                            .setMinTrackingConfidence(0.5f)
                            .setResultListener(this::guardar)
                            .setErrorListener(e -> ultimoErro = String.valueOf(e.getMessage()))
                            .build();

            detector = HandLandmarker.createFromOptions(contexto, opcoes);
            return detector != null;
        } catch (Throwable e) {
            ultimoErro = e.getClass().getSimpleName() + ": " + e.getMessage();
            detector = null;
            return false;
        }
    }

    /** O MediaPipe exige o modelo num buffer direto, fora da memoria do Java. */
    private ByteBuffer lerModelo(String caminho) throws Exception {
        File arquivo = new File(caminho);
        byte[] dados = new byte[(int) arquivo.length()];
        FileInputStream fluxo = new FileInputStream(arquivo);
        try {
            int lidos = 0;
            while (lidos < dados.length) {
                int n = fluxo.read(dados, lidos, dados.length - lidos);
                if (n < 0) break;
                lidos += n;
            }
        } finally {
            fluxo.close();
        }
        ByteBuffer buffer = ByteBuffer.allocateDirect(dados.length);
        buffer.put(dados);
        buffer.rewind();
        return buffer;
    }

    /** Recebe um quadro do Unity (bytes RGBA, linha de cima primeiro). */
    public void enviarQuadro(byte[] pixels, int largura, int altura) {
        if (detector == null) return;
        try {
            if (quadros[0] == null || larguraQuadro != largura || alturaQuadro != altura) {
                for (int i = 0; i < quadros.length; i++)
                    quadros[i] = Bitmap.createBitmap(largura, altura, Bitmap.Config.ARGB_8888);
                larguraQuadro = largura;
                alturaQuadro  = altura;
                proximoQuadro = 0;
            }

            Bitmap quadro = quadros[proximoQuadro];
            proximoQuadro = (proximoQuadro + 1) % quadros.length;

            quadro.copyPixelsFromBuffer(ByteBuffer.wrap(pixels));
            MPImage imagem = new BitmapImageBuilder(quadro).build();

            relogio += 33;   // o modo ao vivo exige tempos sempre crescentes
            detector.detectAsync(imagem, relogio);
        } catch (Throwable e) {
            ultimoErro = e.getClass().getSimpleName() + ": " + e.getMessage();
        }
    }

    /** Chamado pelo MediaPipe numa linha de execucao propria. */
    private void guardar(HandLandmarkerResult resultado, MPImage entrada) {
        synchronized (trava) {
            versao++;

            List<List<NormalizedLandmark>> maos = resultado.landmarks();
            if (maos == null || maos.isEmpty()) {
                confianca = 0f;
                return;
            }

            List<NormalizedLandmark> mao = maos.get(0);
            int total = Math.min(PONTOS, mao.size());
            for (int i = 0; i < total; i++) {
                NormalizedLandmark p = mao.get(i);
                leitura[i * 3]     = p.x();
                leitura[i * 3 + 1] = 1f - p.y();   // o Unity conta o Y de baixo para cima
                leitura[i * 3 + 2] = p.z();
            }

            confianca = 1f;
            try {
                confianca = resultado.handednesses().get(0).get(0).score();
            } catch (Throwable ignorado) {
                // sem esse dado, vale a presenca da mao
            }
        }
    }

    /**
     * Devolve tudo de uma vez para poupar travessias entre C# e Java:
     * [0] contador de versao, [1] confianca, [2..] os 21 pontos (x, y, z).
     */
    public float[] resultado() {
        synchronized (trava) {
            entregue[0] = versao;
            entregue[1] = confianca;
            System.arraycopy(leitura, 0, entregue, 2, VALORES);
            return entregue;
        }
    }

    public String erro() {
        return ultimoErro;
    }

    public void encerrar() {
        HandLandmarker atual = detector;
        detector = null;
        if (atual != null) {
            try {
                atual.close();
            } catch (Throwable ignorado) {
                // encerrando de qualquer forma
            }
        }
    }
}
