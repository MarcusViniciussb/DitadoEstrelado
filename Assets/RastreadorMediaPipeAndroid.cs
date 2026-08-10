using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// MediaPipe nativo do celular.
//
// No computador o rastreamento bom vem do MediaPipe rodando por fora, no
// Python. Esta classe traz o MESMO motor para dentro do aplicativo Android:
// a biblioteca oficial do Google (tasks-vision 0.10.35, a mesma versao do
// Python) com o modelo hand_landmarker.
//
// O aplicativo entrega a imagem inteira, ja endireitada, e o MediaPipe cuida
// sozinho de achar a palma, recortar, girar e ler os 21 pontos - o trabalho
// que antes era feito na mao aqui dentro, e mal.
//
// Os pontos voltam normalizados no quadro inteiro, com o Y contado de baixo
// para cima: exatamente a mesma convencao do rastreador do computador.
public class RastreadorMediaPipeAndroid
{
    public const int PONTOS = 21;

    const string ARQUIVO_DO_MODELO = "hand_landmarker.task";
    const int    LADO_MAIOR        = 512;  // resolucao enviada ao MediaPipe

    public bool      Pronto { get; private set; }
    public float     Score  { get; private set; }
    public string    Erro   { get; private set; } = "";
    public Vector3[] Pontos { get; private set; } = new Vector3[PONTOS];

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject ponte;
    RenderTexture     reduzida;
    byte[]            pixels;
    bool              lendoQuadro  = false;
    int               ultimaVersao = -1;

    public IEnumerator Iniciar()
    {
        // O MediaPipe le o modelo de um arquivo comum. Dentro do APK ele esta
        // compactado, entao a primeira execucao copia para a pasta do aplicativo.
        string destino = System.IO.Path.Combine(Application.persistentDataPath, ARQUIVO_DO_MODELO);
        bool precisaCopiar = !System.IO.File.Exists(destino) ||
                             new System.IO.FileInfo(destino).Length < 1000000;

        if (precisaCopiar)
        {
            string origem = Application.streamingAssetsPath + "/" + ARQUIVO_DO_MODELO;
            using (var pedido = UnityEngine.Networking.UnityWebRequest.Get(origem))
            {
                yield return pedido.SendWebRequest();
                if (pedido.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Erro = "modelo nao encontrado: " + pedido.error;
                    yield break;
                }
                System.IO.File.WriteAllBytes(destino, pedido.downloadHandler.data);
            }
        }

        try
        {
            var jogador   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var atividade = jogador.GetStatic<AndroidJavaObject>("currentActivity");

            ponte  = new AndroidJavaObject("br.edu.ifto.ditadoestrelado.RastreadorMediaPipe");
            Pronto = ponte.Call<bool>("iniciar", atividade, destino);
            if (!Pronto) Erro = ponte.Call<string>("erro");
        }
        catch (System.Exception e)
        {
            Erro   = e.Message;
            Pronto = false;
        }
    }

    // Manda um quadro para analise. Nao trava o jogo: a leitura da placa de
    // video volta depois, e o MediaPipe trabalha em segundo plano.
    public void Enviar(Texture origem)
    {
        if (!Pronto || origem == null || lendoQuadro) return;

        int largura, altura;
        if (origem.width >= origem.height)
        {
            largura = LADO_MAIOR;
            altura  = Mathf.RoundToInt(LADO_MAIOR * (float)origem.height / origem.width);
        }
        else
        {
            altura  = LADO_MAIOR;
            largura = Mathf.RoundToInt(LADO_MAIOR * (float)origem.width / origem.height);
        }
        largura = Mathf.Max(64, largura - largura % 4);
        altura  = Mathf.Max(64, altura  - altura  % 4);

        if (reduzida == null || reduzida.width != largura || reduzida.height != altura)
        {
            if (reduzida != null) reduzida.Release();
            reduzida = new RenderTexture(largura, altura, 0, RenderTextureFormat.ARGB32);
            reduzida.Create();
            pixels = new byte[largura * altura * 4];
        }

        // Espelha na vertical: o Unity guarda a imagem de baixo para cima e o
        // Android le de cima para baixo
        Graphics.Blit(origem, reduzida, new Vector2(1f, -1f), new Vector2(0f, 1f));

        lendoQuadro = true;
        AsyncGPUReadback.Request(reduzida, 0, TextureFormat.RGBA32, Receber);
    }

    void Receber(AsyncGPUReadbackRequest pedido)
    {
        lendoQuadro = false;
        if (!Pronto || pedido.hasError || pixels == null) return;

        var dados = pedido.GetData<byte>();
        if (dados.Length != pixels.Length) return;   // o tamanho mudou no meio do caminho
        dados.CopyTo(pixels);

        ponte.Call("enviarQuadro", pixels, reduzida.width, reduzida.height);
    }

    // Busca o ultimo resultado pronto. Devolve true quando ha novidade.
    public bool Atualizar()
    {
        if (!Pronto) return false;

        float[] resposta = ponte.Call<float[]>("resultado");
        if (resposta == null || resposta.Length < 2 + PONTOS * 3) return false;

        int versao = (int)resposta[0];
        if (versao == ultimaVersao) return false;
        ultimaVersao = versao;

        Score = resposta[1];
        for (int i = 0; i < PONTOS; i++)
            Pontos[i] = new Vector3(resposta[2 + i * 3],
                                    resposta[3 + i * 3],
                                    resposta[4 + i * 3]);
        return true;
    }

    public void Encerrar()
    {
        Pronto = false;
        if (ponte != null)
        {
            try { ponte.Call("encerrar"); } catch (System.Exception) { }
            ponte = null;
        }
        if (reduzida != null) { reduzida.Release(); reduzida = null; }
    }
#else
    // No computador esta classe nao entra em acao: quem rastreia e o Python.
    public IEnumerator Iniciar()          { yield break; }
    public void        Enviar(Texture t)  { }
    public bool        Atualizar()        { return false; }
    public void        Encerrar()         { }
#endif
}
