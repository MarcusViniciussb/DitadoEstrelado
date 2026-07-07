using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// VisualizadorMaoUI: desenha o esqueleto da mão por cima do feed da câmera.
//
// Ele se configura SOZINHO: no início vira uma camada de tela cheia dentro do
// Canvas (logo acima da imagem da câmera) e usa a conversão de coordenadas do
// ControladorCamera — que já desconta o recorte e o espelho da imagem.
// Não é preciso ajustar rotação/escala manualmente no Inspector.
public class VisualizadorMaoUI : MonoBehaviour
{
    [Header("Fonte dos dados da mao")]
    public ControladorCamera controlador;

    [Header("Visual")]
    public Color corLinha = new Color(0f, 0.85f, 0.85f, 0.9f);  // ciano
    public Color corPonto = new Color(1f, 1f,    1f,    0.95f); // branco
    public Color corPulso = new Color(1f, 0.8f,  0.1f,  1f);    // amarelo — ponto 0

    [Range(1f, 8f)]  public float espessuraLinha = 4f;
    [Range(6f, 24f)] public float tamanhoPonto   = 14f;

    [Header("Suavizacao (maior = responde mais rapido; menor = mais macio)")]
    [Range(4f, 30f)] public float suavizacao = 14f;

    // Conexões do esqueleto (pares de índices dos 21 keypoints do MediaPipe)
    private static readonly int[,] OSSOS =
    {
        // Polegar
        {0,1},{1,2},{2,3},{3,4},
        // Indicador
        {0,5},{5,6},{6,7},{7,8},
        // Médio
        {0,9},{9,10},{10,11},{11,12},
        // Anelar
        {0,13},{13,14},{14,15},{15,16},
        // Mínimo
        {0,17},{17,18},{18,19},{19,20},
        // Palma (nós entre os dedos)
        {5,9},{9,13},{13,17}
    };

    private RectTransform meuRect;
    private Camera cameraUI;
    private List<RectTransform> linhas = new List<RectTransform>();
    private List<RectTransform> pontos = new List<RectTransform>();

    private Vector2[] suavizados = new Vector2[21];
    private bool temPosicaoAnterior = false;

    void Awake()
    {
        var canvas = GetComponentInParent<Canvas>();
        meuRect = (RectTransform)transform;

        // Vira uma camada de tela cheia direto no Canvas.
        // Isso desfaz qualquer rotação/escala antiga que atrapalhava o alinhamento.
        meuRect.SetParent(canvas.transform, false);
        meuRect.SetAsLastSibling(); // por cima de TUDO: funciona como um "cursor"
        meuRect.localRotation    = Quaternion.identity;
        meuRect.localScale       = Vector3.one;
        meuRect.anchorMin        = Vector2.zero;
        meuRect.anchorMax        = Vector2.one;
        meuRect.pivot            = new Vector2(0.5f, 0.5f);
        meuRect.anchoredPosition = Vector2.zero;
        meuRect.sizeDelta        = Vector2.zero;

        // Câmera usada pelo Canvas (necessária para converter tela → local)
        cameraUI = (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                   ? canvas.worldCamera : null;

        int numOssos = OSSOS.GetLength(0);
        for (int i = 0; i < numOssos; i++) linhas.Add(CriarLinha());
        for (int i = 0; i < 21; i++)       pontos.Add(CriarPonto(i));
    }

    RectTransform CriarLinha()
    {
        var go = new GameObject("_Linha", typeof(Image));
        go.layer = 5;
        go.transform.SetParent(transform, false);
        var img = go.GetComponent<Image>();
        img.color = corLinha;
        img.raycastTarget = false; // IMPORTANTE: não pode roubar cliques dos botões!
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, espessuraLinha);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        return rt;
    }

    RectTransform CriarPonto(int indice)
    {
        var go = new GameObject("_Ponto" + indice, typeof(Image));
        go.layer = 5;
        go.transform.SetParent(transform, false);
        var img = go.GetComponent<Image>();
        img.color = (indice == 0) ? corPulso : corPonto;
        img.sprite = UIFabrica.Circulo(); // bolinha redonda em vez de quadrado
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(tamanhoPonto, tamanhoPonto);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        return rt;
    }

    void Update()
    {
        bool visivel = controlador != null && controlador.MaoDetectada
                       && controlador.PontosDaMaoAtuais != null;

        for (int i = 0; i < linhas.Count; i++) linhas[i].gameObject.SetActive(visivel);
        for (int i = 0; i < pontos.Count; i++) pontos[i].gameObject.SetActive(visivel);

        if (!visivel)
        {
            temPosicaoAnterior = false; // quando a mão voltar, "teleporta" em vez de deslizar
            return;
        }

        // Mantém o esqueleto acima de tudo (o menu usa SetAsLastSibling também,
        // então reconquistamos o topo sempre que a mão está visível)
        if (meuRect.GetSiblingIndex() != meuRect.parent.childCount - 1)
            meuRect.SetAsLastSibling();

        Vector3[] pts = controlador.PontosDaMaoAtuais;

        // Suavização exponencial: cada ponto desliza em direção à posição nova.
        // Independente do FPS graças ao Time.deltaTime.
        float fator = 1f - Mathf.Exp(-suavizacao * Time.deltaTime);

        for (int i = 0; i < 21; i++)
        {
            Vector2 posTela = controlador.PontoParaTela(pts[i]);
            Vector2 posLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                meuRect, posTela, cameraUI, out posLocal);

            suavizados[i] = temPosicaoAnterior
                          ? Vector2.Lerp(suavizados[i], posLocal, fator)
                          : posLocal;
        }
        temPosicaoAnterior = true;

        for (int i = 0; i < 21 && i < pontos.Count; i++)
            pontos[i].anchoredPosition = suavizados[i];

        for (int i = 0; i < OSSOS.GetLength(0); i++)
            PosicionarLinha(linhas[i], suavizados[OSSOS[i, 0]], suavizados[OSSOS[i, 1]]);
    }

    void PosicionarLinha(RectTransform rt, Vector2 a, Vector2 b)
    {
        Vector2 dir = b - a;
        float   len = dir.magnitude;
        float   ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rt.anchoredPosition = (a + b) * 0.5f;
        rt.sizeDelta        = new Vector2(len, espessuraLinha);
        rt.localEulerAngles = new Vector3(0, 0, ang);
    }
}
