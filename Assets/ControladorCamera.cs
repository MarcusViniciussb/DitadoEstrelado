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
    private float tempoUltimoReconhecimento = -99f;

    private WebCamTexture minhaCamera;
    private HandLandmarkDetector detetive;
    private bool cameraPronta = false;
    private bool detetivePronto = false;

    // Pontos da mão atuais — lidos pelo VisualizadorMaoUI para desenhar o esqueleto
    public Vector3[] PontosDaMaoAtuais { get; private set; }
    public bool MaoDetectada { get; private set; }

    void Start()
    {
        if (ficheirosDaIA == null)
        {
            Debug.LogError("FATAL: 'ficheirosDaIA' (ResourceSet) nao esta vinculado no Inspector!");
            enabled = false;
            return;
        }

        // 640x480 melhora a detecção em posições excêntricas da mão
        minhaCamera = new WebCamTexture(640, 480);
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
            if (Input.GetKeyDown(KeyCode.M)) { if (shift) reconhecedor.ApagarLetra("M"); else StartCoroutine(GravarComAtraso("M")); }
            if (Input.GetKeyDown(KeyCode.A)) { if (shift) reconhecedor.ApagarLetra("A"); else StartCoroutine(GravarComAtraso("A")); }
            if (Input.GetKeyDown(KeyCode.C)) { if (shift) reconhecedor.ApagarLetra("C"); else StartCoroutine(GravarComAtraso("C")); }
            if (Input.GetKeyDown(KeyCode.B)) { if (shift) reconhecedor.ApagarLetra("B"); else StartCoroutine(GravarComAtraso("B")); }
            if (Input.GetKeyDown(KeyCode.N)) { if (shift) reconhecedor.ApagarLetra("N"); else StartCoroutine(GravarComAtraso("N")); }
            if (Input.GetKeyDown(KeyCode.V)) { if (shift) reconhecedor.ApagarLetra("V"); else StartCoroutine(GravarComAtraso("V")); }
            if (Input.GetKeyDown(KeyCode.L)) { if (shift) reconhecedor.ApagarLetra("L"); else StartCoroutine(GravarComAtraso("L")); }
            if (Input.GetKeyDown(KeyCode.I)) { if (shift) reconhecedor.ApagarLetra("I"); else StartCoroutine(GravarComAtraso("I")); }
            if (Input.GetKeyDown(KeyCode.O)) { if (shift) reconhecedor.ApagarLetra("O"); else StartCoroutine(GravarComAtraso("O")); }
            if (Input.GetKeyDown(KeyCode.T)) { if (shift) reconhecedor.ApagarLetra("T"); else StartCoroutine(GravarComAtraso("T")); }
            if (Input.GetKeyDown(KeyCode.P)) { if (shift) reconhecedor.ApagarLetra("P"); else StartCoroutine(GravarComAtraso("P")); }
            if (Input.GetKeyDown(KeyCode.E)) { if (shift) reconhecedor.ApagarLetra("E"); else StartCoroutine(GravarComAtraso("E")); }
            if (Input.GetKeyDown(KeyCode.R)) { if (shift) reconhecedor.ApagarLetra("R"); else StartCoroutine(GravarComAtraso("R")); }
            if (Input.GetKeyDown(KeyCode.S)) { if (shift) reconhecedor.ApagarLetra("S"); else StartCoroutine(GravarComAtraso("S")); }
            if (Input.GetKeyDown(KeyCode.G)) { if (shift) reconhecedor.ApagarLetra("G"); else StartCoroutine(GravarComAtraso("G")); }
            if (Input.GetKeyDown(KeyCode.D)) { if (shift) reconhecedor.ApagarLetra("D"); else StartCoroutine(GravarComAtraso("D")); }
            if (Input.GetKeyDown(KeyCode.U)) { if (shift) reconhecedor.ApagarLetra("U"); else StartCoroutine(GravarComAtraso("U")); }
        }

        // Space     = pula a PALAVRA
        // Tab       = pula a LETRA atual (sem pontos)
        // Backspace = volta à letra anterior
        if (Input.GetKeyDown(KeyCode.Space)     && gerenciador != null) gerenciador.PularPalavra();
        if (Input.GetKeyDown(KeyCode.Tab)       && gerenciador != null) gerenciador.PularLetra();
        if (Input.GetKeyDown(KeyCode.Backspace) && gerenciador != null) gerenciador.VoltarLetra();

        // Processa novo frame da câmera (apenas quando houver frame novo — performance)
        if (minhaCamera.isPlaying && minhaCamera.didUpdateThisFrame)
        {
            detetive.ProcessImage(minhaCamera);
            cameraPronta = true;
        }

        if (!cameraPronta) return;

        // Score: confiança da IA (0 a 1). Abaixo de 0.5 = sem mão confiável
        MaoDetectada = detetive.Score >= 0.5f;
        if (!MaoDetectada) return;

        Vector3[] pontosDaMao = ColetarPontosDaMao();
        PontosDaMaoAtuais = pontosDaMao;

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

            if (letraFeita != "Desconhecido" && letraFeita != "Nenhuma")
            {
                bool acertou = gerenciador.TentarLetra(letraFeita);
                if (acertou)
                {
                    // Aprendizado automático: cada acerto vira nova amostra de treinamento
                    reconhecedor.AprendizagemAutomatica(letraFeita, pontosDaMao);
                    tempoUltimoReconhecimento = Time.time;
                }
            }
        }
    }

    // Converte os 21 landmarks do enum para Vector3[].
    // HandLandmarkDetector.GetKeyPoint() retorna Vector2 (x,y normalizados 0-1).
    // Definimos z=0 pois o reconhecimento de LIBRAS usa apenas posição 2D.
    Vector3[] ColetarPontosDaMao()
    {
        Vector3[] pontos = new Vector3[21];
        var valores = (HandLandmarkDetector.KeyPoint[])System.Enum.GetValues(typeof(HandLandmarkDetector.KeyPoint));
        for (int i = 0; i < valores.Length; i++)
        {
            Vector2 v2 = detetive.GetKeyPoint(valores[i]);
            pontos[i] = new Vector3(v2.x, v2.y, 0f);
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
        if (minhaCamera != null && minhaCamera.isPlaying)
            minhaCamera.Stop();
    }
}
