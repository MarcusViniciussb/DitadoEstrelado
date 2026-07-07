using UnityEngine;
using UnityEngine.UI;
using MediaPipe.HandLandmark;
using System.Collections;

// ControladorCamera: conecta a webcam ao detector de mão (HandLandmarkDetector)
// e passa os 21 pontos da mão para o ReconhecedorLibras.
// Este script fica num GameObject que também tem a RawImage da câmera.
public class ControladorCamera : MonoBehaviour
{
    [Header("Referências de Cena")]
    public RawImage fundoDoEcra;         // RawImage que exibe o feed da câmera
    public ResourceSet ficheirosDaIA;    // ScriptableObject do plugin HandLandmark
    public Transform bolinhaDoDedo;      // Objeto 3D que segue a ponta do indicador
    public ReconhecedorLibras reconhecedor;

    [Header("Calibração Visual (posição da bolinha)")]
    public float escalaX = -15f;
    public float escalaY = 10f;
    public float translaX = 0f;
    public float translaY = 0f;

    [Header("Controle do Jogo")]
    public bool MODO_TREINAMENTO = true;
    public GerenciadorDeJogo gerenciador; // Arraste o GameObject que tem GerenciadorDeJogo aqui

    [Header("Reconhecimento")]
    public float cooldownReconhecimento = 1.5f; // segundos mínimos entre duas letras reconhecidas
    public float tempoEstabilidade = 0.35f;     // segundos segurando o MESMO sinal para valer
    private float tempoUltimoReconhecimento = -99f;
    private string letraCandidata = "";
    private float tempoLetraCandidata = 0f;

    [Header("Exibicao da camera")]
    public bool espelharImagem  = true;  // modo "selfie" (RawImage com escala X = -1)
    public bool inverterVertical = false; // ligue se o esqueleto aparecer de cabeça para baixo

    [Header("Zoom inteligente (2 estagios, como o MediaPipe original)")]
    // Em vez de mandar o frame INTEIRO espremido para a IA, recorta um quadrado
    // ao redor da última posição da mão — a mão chega grande e nítida ao modelo.
    // Se algo ficar estranho, desligue aqui para voltar ao comportamento antigo.
    public bool zoomInteligente = true;
    [Range(1.5f, 4f)] public float margemDoZoom = 2.4f; // recorte = tamanho da mão × margem

    private RenderTexture texturaRecorte;                    // quadrado enviado à IA
    private Vector2 centroCaixa = new Vector2(0.5f, 0.5f);   // centro do recorte (normalizado)
    private float   ladoCaixaPx = 0f;                        // lado do recorte em px (0 = busca ampla)
    private Vector2 escalaBlit  = Vector2.one;               // último recorte aplicado
    private Vector2 offsetBlit  = Vector2.zero;              //   (para mapear pontos de volta)
    private int     framesSemMao = 0;

    private WebCamTexture minhaCamera;
    private HandLandmarkDetector detetive;
    private bool cameraPronta = false;
    private bool detetivePronto = false;

    // Recorte central aplicado ao feed para NÃO esticar a imagem na tela
    private Rect uvRecorte = new Rect(0, 0, 1, 1);
    private int larguraTelaAnterior = 0;
    private int alturaTelaAnterior = 0;

    // Pontos da mão atuais — lidos pelo VisualizadorMaoUI para desenhar o esqueleto
    public Vector3[] PontosDaMaoAtuais { get; private set; }
    public bool MaoDetectada { get; private set; }

    // Converte um keypoint (0..1 no frame INTEIRO da câmera) para posição na
    // tela em pixels, já descontando o recorte e o espelho.
    // O detector usa a convenção do Unity: Y cresce para CIMA.
    // Todo script que precisa saber onde a mão está na tela deve usar isto.
    public Vector2 PontoParaTela(Vector3 kp)
    {
        float u = (kp.x - uvRecorte.x) / uvRecorte.width;
        float v = (kp.y - uvRecorte.y) / uvRecorte.height;
        if (espelharImagem)   u = 1f - u;
        if (inverterVertical) v = 1f - v;
        return new Vector2(u * Screen.width, v * Screen.height);
    }

    void Start()
    {
        if (ficheirosDaIA == null)
        {
            Debug.LogError("FATAL: 'ficheirosDaIA' (ResourceSet) nao esta vinculado no Inspector!");
            enabled = false;
            return;
        }

        // 1280x720: imagem mais nítida na tela e mais detalhe para a IA
        // (se a webcam não suportar, o Unity escolhe a resolução mais próxima)
        minhaCamera = new WebCamTexture(1280, 720);
        fundoDoEcra.texture = minhaCamera;
        minhaCamera.Play();

        StartCoroutine(InicializarDetetiveAposCamera());
    }

    // Aguarda câmera ter dimensões reais antes de criar o detector.
    // WebCamTexture começa com 16x16 de placeholder — criar o detector nesse momento causaria crash.
    IEnumerator InicializarDetetiveAposCamera()
    {
        Debug.Log("Aguardando camera inicializar...");

        while (!minhaCamera.isPlaying || minhaCamera.width < 100)
            yield return null;

        yield return new WaitForEndOfFrame();

        try
        {
            detetive = new HandLandmarkDetector(ficheirosDaIA);
            detetivePronto = true;
            Debug.Log("HandLandmarkDetector criado! Resolucao: " + minhaCamera.width + "x" + minhaCamera.height);
        }
        catch (System.Exception e)
        {
            Debug.LogError("FATAL: Falha ao criar HandLandmarkDetector.\n" + e.Message);
            enabled = false;
        }
    }

    void Update()
    {
        // Guarda: IA ainda não iniciou
        if (!detetivePronto) return;

        // Input SEMPRE antes de qualquer return.
        // GetKeyDown só existe por 1 frame — se ficar atrás de um return, some para sempre.
        if (MODO_TREINAMENTO)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Shift+Tecla = APAGA todas as amostras daquela letra (para regravar do zero)
            // Tecla sozinha = GRAVA nova amostra (pode pressionar várias vezes para acumular)
            // O laço cobre TODAS as letras de A a Z (KeyCode.A até KeyCode.Z são sequenciais)
            for (KeyCode tecla = KeyCode.A; tecla <= KeyCode.Z; tecla++)
            {
                if (!Input.GetKeyDown(tecla)) continue;

                string letra = tecla.ToString(); // "A", "B", ... "Z"
                if (shift) reconhecedor.ApagarLetra(letra);
                else       StartCoroutine(GravarComAtraso(letra));
            }
        }

        // Space     = pula a PALAVRA
        // Tab       = pula a LETRA atual (sem pontos)
        // Backspace = volta à letra anterior
        if (Input.GetKeyDown(KeyCode.Space)     && gerenciador != null) gerenciador.PularPalavra();
        if (Input.GetKeyDown(KeyCode.Tab)       && gerenciador != null) gerenciador.PularLetra();
        if (Input.GetKeyDown(KeyCode.Backspace) && gerenciador != null) gerenciador.VoltarLetra();

        // Mantém a imagem sem distorção (recorte central estilo "cover")
        AjustarRecorteDaCamera();

        // Processa novo frame da câmera (apenas quando houver frame novo — performance)
        if (minhaCamera.isPlaying && minhaCamera.didUpdateThisFrame)
        {
            ProcessarFrame();
            cameraPronta = true;
        }

        if (!cameraPronta) return;

        // Detecção com histerese: precisa de confiança ALTA (0.6) para começar,
        // mas só solta quando cair BEM (0.45). Isso elimina o pisca-pisca do
        // esqueleto quando a confiança fica oscilando perto do limite.
        float confianca = detetive.Score;
        MaoDetectada = MaoDetectada ? (confianca >= 0.45f) : (confianca >= 0.6f);

        if (!MaoDetectada)
        {
            // Perdeu a mão: depois de ~15 frames volta à "busca ampla"
            // (recorte grande no centro) para reencontrá-la
            framesSemMao++;
            if (framesSemMao > 15)
            {
                centroCaixa = new Vector2(0.5f, 0.5f);
                ladoCaixaPx = 0f;
            }
            return;
        }
        framesSemMao = 0;

        Vector3[] pontosDaMao = ColetarPontosDaMao();
        PontosDaMaoAtuais = pontosDaMao;

        // Reposiciona o recorte para o próximo frame seguir a mão
        if (zoomInteligente) AtualizarCaixaDaMao(pontosDaMao);

        // Índice 8 = Index4 = ponta do dedo indicador
        Vector3 posicaoDaIA = pontosDaMao[8];
        float xNoEcra = ((posicaoDaIA.x - 0.5f) * escalaX) + translaX;
        float yNoEcra = ((posicaoDaIA.y - 0.5f) * escalaY) + translaY;

        if (bolinhaDoDedo != null)
            bolinhaDoDedo.localPosition = new Vector3(xNoEcra, yNoEcra, 4f);

        // Modo Jogo: reconhece letra e envia para o GerenciadorDeJogo
        if (!MODO_TREINAMENTO && reconhecedor != null && gerenciador != null)
        {
            // Cooldown: ignora reconhecimentos muito rápidos (evita registrar a mesma letra várias vezes)
            if (Time.time - tempoUltimoReconhecimento < cooldownReconhecimento) return;

            string letraFeita = reconhecedor.ClassificarLetra(pontosDaMao);

            if (letraFeita == "Desconhecido" || letraFeita == "Nenhuma")
            {
                letraCandidata = "";
                return;
            }

            // Filtro de estabilidade: a MESMA letra precisa ser vista por
            // 'tempoEstabilidade' segundos seguidos antes de valer.
            // Elimina falsos positivos enquanto a mão troca de um sinal para outro.
            if (letraFeita != letraCandidata)
            {
                letraCandidata      = letraFeita;
                tempoLetraCandidata = Time.time;
                return;
            }
            if (Time.time - tempoLetraCandidata < tempoEstabilidade) return;

            bool acertou = gerenciador.TentarLetra(letraFeita);
            if (acertou)
            {
                // Aprendizado automático: cada acerto vira nova amostra de treinamento
                reconhecedor.AprendizagemAutomatica(letraFeita, pontosDaMao);
                tempoUltimoReconhecimento = Time.time;
                letraCandidata = "";
            }
        }
    }

    // Ajusta o pedaço visível do feed (uvRect) para preencher a tela SEM esticar:
    // recorta o centro da imagem, como os apps de câmera de celular fazem.
    void AjustarRecorteDaCamera()
    {
        if (minhaCamera == null || minhaCamera.width < 100) return;
        if (Screen.width == larguraTelaAnterior && Screen.height == alturaTelaAnterior) return;
        larguraTelaAnterior = Screen.width;
        alturaTelaAnterior  = Screen.height;

        float aspectoTela   = (float)Screen.width / Screen.height;
        float aspectoCamera = (float)minhaCamera.width / minhaCamera.height;

        if (aspectoCamera > aspectoTela)
        {
            // Câmera mais "larga" que a tela → corta as laterais
            float w = aspectoTela / aspectoCamera;
            uvRecorte = new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        else
        {
            // Câmera mais "alta" que a tela → corta topo e base
            float h = aspectoCamera / aspectoTela;
            uvRecorte = new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }
        fundoDoEcra.uvRect = uvRecorte;
    }

    // Prepara a imagem e envia para a IA.
    // Com zoom inteligente: recorta um quadrado ao redor da mão (Graphics.Blit)
    // e envia só ele — a mão ocupa a imagem inteira e o modelo enxerga MUITO melhor.
    void ProcessarFrame()
    {
        if (!zoomInteligente)
        {
            escalaBlit = Vector2.one;
            offsetBlit = Vector2.zero;
            detetive.ProcessImage(minhaCamera);
            return;
        }

        if (texturaRecorte == null)
            texturaRecorte = new RenderTexture(320, 320, 0);

        float w = minhaCamera.width;
        float h = minhaCamera.height;

        // Sem mão rastreada: quadrado grande no centro (área visível da tela).
        // Com mão rastreada: quadrado do tamanho da mão × margem.
        float lado = (ladoCaixaPx > 0f) ? ladoCaixaPx : Mathf.Min(w, h);
        lado = Mathf.Clamp(lado, 160f, Mathf.Min(w, h));

        // Converte centro+lado do recorte em escala/deslocamento normalizados
        Vector2 escala = new Vector2(lado / w, lado / h);
        Vector2 offset = new Vector2(centroCaixa.x - escala.x * 0.5f,
                                     centroCaixa.y - escala.y * 0.5f);
        offset.x = Mathf.Clamp(offset.x, 0f, 1f - escala.x);
        offset.y = Mathf.Clamp(offset.y, 0f, 1f - escala.y);

        Graphics.Blit(minhaCamera, texturaRecorte, escala, offset);
        detetive.ProcessImage(texturaRecorte);

        escalaBlit = escala;
        offsetBlit = offset;
    }

    // Recalcula a caixa de recorte a partir da mão detectada (com suavização,
    // para o recorte deslizar atrás da mão em vez de pular)
    void AtualizarCaixaDaMao(Vector3[] pontos)
    {
        float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
        for (int i = 0; i < 21; i++)
        {
            if (pontos[i].x < minX) minX = pontos[i].x;
            if (pontos[i].x > maxX) maxX = pontos[i].x;
            if (pontos[i].y < minY) minY = pontos[i].y;
            if (pontos[i].y > maxY) maxY = pontos[i].y;
        }

        Vector2 centro = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        float larguraPx = (maxX - minX) * minhaCamera.width;
        float alturaPx  = (maxY - minY) * minhaCamera.height;
        float alvoLado  = Mathf.Max(larguraPx, alturaPx) * margemDoZoom;

        centroCaixa = Vector2.Lerp(centroCaixa, centro, 0.35f);
        ladoCaixaPx = (ladoCaixaPx <= 0f) ? alvoLado : Mathf.Lerp(ladoCaixaPx, alvoLado, 0.25f);
    }

    // Converte os 21 landmarks do enum para Vector3[].
    // HandLandmarkDetector.GetKeyPoint() retorna Vector2 (x,y normalizados 0-1)
    // DENTRO DO RECORTE — aqui mapeamos de volta para o frame inteiro, assim
    // todo o resto do jogo (esqueleto, hover, reconhecimento) continua igual.
    // Definimos z=0 pois o reconhecimento de LIBRAS usa apenas posição 2D.
    Vector3[] ColetarPontosDaMao()
    {
        Vector3[] pontos = new Vector3[21];
        var valores = (HandLandmarkDetector.KeyPoint[])System.Enum.GetValues(typeof(HandLandmarkDetector.KeyPoint));
        for (int i = 0; i < valores.Length; i++)
        {
            Vector2 v2 = detetive.GetKeyPoint(valores[i]);
            pontos[i] = new Vector3(offsetBlit.x + v2.x * escalaBlit.x,
                                    offsetBlit.y + v2.y * escalaBlit.y, 0f);
        }
        return pontos;
    }

    IEnumerator GravarComAtraso(string letra)
    {
        Debug.Log("Prepare a letra [" + letra + "]! Gravando em 3 segundos...");
        yield return new WaitForSeconds(3f);

        if (!detetivePronto || detetive.Score < 0.5f)
        {
            Debug.LogError("Falhou! A IA nao detectou sua mao. Tente novamente.");
            yield break;
        }

        Vector3[] pontosAgora = ColetarPontosDaMao();
        reconhecedor.GravarLetra(letra, pontosAgora);
    }

    void OnDestroy()
    {
        detetive?.Dispose();
        if (texturaRecorte != null) texturaRecorte.Release();
        if (minhaCamera != null && minhaCamera.isPlaying)
            minhaCamera.Stop();
    }
}
