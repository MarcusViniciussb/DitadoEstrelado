using UnityEngine;
using UnityEngine.UI;
using MediaPipe.HandPose;
using System.Collections;

public class ControladorCamera : MonoBehaviour
{
    public RawImage fundoDoEcra;
    public ResourceSet ficheirosDaIA;
    public Transform bolinhaDoDedo;
    public ReconhecedorLibras reconhecedor;

    [Header("Calibração Visual")]
    public float escalaX = -15f;
    public float escalaY = 10f;
    public float translaX = 0f;
    public float translaY = 0f;

    [Header("CONTROLE DO JOGO")]
    public bool MODO_TREINAMENTO = true;
    public string palavraAlvo = "MACA";
    private int letraAtualQueOJogoPede = 0;

    private WebCamTexture minhaCamera;
    private HandPipeline detetive;
    private bool cameraPronta = false;
    private bool detetivePronto = false; // ← NOVO flag de segurança
    private bool frameProcessadoEsteUpdate = false;
    void Start()
    {
        // Guarda de segurança: valida dependências antes de tudo
        if (ficheirosDaIA == null)
        {
            Debug.LogError("❌ FATAL: 'ficheirosDaIA' (ResourceSet) não está vinculado no Inspector!");
            enabled = false; // desativa o script para não travar no Update
            return;
        }

        minhaCamera = new WebCamTexture();
        fundoDoEcra.texture = minhaCamera;
        minhaCamera.Play();

        // Delegamos a criação do HandPipeline para quando a câmera estiver pronta
        StartCoroutine(InicializarDetetiveAposCamera());
    }

    /// <summary>
    /// Aguarda a câmera ter dimensões válidas antes de criar o HandPipeline.
    /// Isso resolve a condição de corrida entre WebCamTexture e Barracuda/Sentis.
    /// </summary>
    IEnumerator InicializarDetetiveAposCamera()
    {
        Debug.Log("⏳ Aguardando câmera inicializar...");

        // Espera até a câmera estar rodando E com resolução real (não 16x16 placeholder)
        while (!minhaCamera.isPlaying || minhaCamera.width < 100)
        {
            yield return null; // aguarda o próximo frame
        }

        // Um frame extra de segurança para garantir que o driver terminou
        yield return new WaitForEndOfFrame();

        try
        {
            detetive = new HandPipeline(ficheirosDaIA);
            detetivePronto = true;
            Debug.Log($"✅ HandPipeline criado! Resolução da câmera: {minhaCamera.width}x{minhaCamera.height}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ FATAL: Falha ao criar HandPipeline.\n" +
                           $"Verifique se o ResourceSet está correto e os shaders compilaram.\n" +
                           $"Erro: {e.Message}");
            enabled = false;
        }
    }

    void Update()
    {
        // ── Guarda 1: IA ainda não iniciou ──────────────────────────────
        if (!detetivePronto) return;

        // ── INPUT: lido ANTES de qualquer return da câmera ──────────────
        // GetKeyDown só vive 1 frame. Nunca pode ficar atrás de um return.
        if (MODO_TREINAMENTO)
        {
            if (Input.GetKeyDown(KeyCode.M)) StartCoroutine(GravarComAtraso("M"));
            if (Input.GetKeyDown(KeyCode.A)) StartCoroutine(GravarComAtraso("A"));
            if (Input.GetKeyDown(KeyCode.C)) StartCoroutine(GravarComAtraso("C"));
        }

        // ── Câmera: só processa quando há frame novo ─────────────────────
        // Isso evita chamar ProcessImage em frames duplicados (ganho de performance)
        if (minhaCamera.isPlaying && minhaCamera.didUpdateThisFrame)
        {
            detetive.ProcessImage(minhaCamera);
            cameraPronta = true;
        }

        if (!cameraPronta) return;

        // ── Coleta os 21 pontos ──────────────────────────────────────────
        Vector3[] pontosDaMao = new Vector3[21];
        for (int i = 0; i < 21; i++) pontosDaMao[i] = detetive.GetKeyPoint(i);

        // Se a IA não detectou mão alguma, para por aqui
        if (pontosDaMao[0] == Vector3.zero) return;

        // ── Atualiza posição da bolinha (ponto 8 = ponta do indicador) ───
        Vector3 posicaoDaIA = pontosDaMao[8];
        float xNoEcra = ((posicaoDaIA.x - 0.5f) * escalaX) + translaX;
        float yNoEcra = ((posicaoDaIA.y - 0.5f) * escalaY) + translaY;
        bolinhaDoDedo.localPosition = new Vector3(xNoEcra, yNoEcra, 4f);

        // ── Modo Jogo: classifica letra e verifica acerto ────────────────
        if (!MODO_TREINAMENTO && reconhecedor != null)
        {
            string letraFeita = reconhecedor.ClassificarLetra(pontosDaMao);

            if (letraFeita != "Desconhecido" && letraFeita != "Nenhuma")
            {
                if (letraAtualQueOJogoPede < palavraAlvo.Length)
                {
                    string letraQuePrecisamos = palavraAlvo[letraAtualQueOJogoPede].ToString();
                    if (letraQuePrecisamos == "Ç") letraQuePrecisamos = "C";

                    if (letraFeita == letraQuePrecisamos)
                    {
                        Debug.Log("🎯 ACERTOU: " + letraFeita);
                        letraAtualQueOJogoPede++;
                    }
                }
            }
        }
    }

    IEnumerator GravarComAtraso(string letra)
    {
        Debug.Log("⏳ Prepare a letra [" + letra + "]! 3 segundos...");
        yield return new WaitForSeconds(3f); // simplifiquei os 3 yields desnecessários

        Vector3[] pontosAgora = new Vector3[21];
        for (int i = 0; i < 21; i++) pontosAgora[i] = detetive.GetKeyPoint(i);

        if (pontosAgora[0] == Vector3.zero)
            Debug.LogError("❌ Falhou! A IA perdeu sua mão.");
        else
            reconhecedor.GravarLetra(letra, pontosAgora);
    }

    void OnDestroy()
    {
        detetive?.Dispose();
        // Libera a câmera também — boa prática
        if (minhaCamera != null && minhaCamera.isPlaying)
            minhaCamera.Stop();
    }
}