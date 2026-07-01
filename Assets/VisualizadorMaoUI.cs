using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// VisualizadorMaoUI: desenha o esqueleto da mão sobre o feed da câmera.
// COMO USAR:
//   1) Adicione este script num GameObject "EsqueletoMao" filho do RawImage.
//   2) O RectTransform de EsqueletoMao deve ter Anchor = stretch/stretch (preenche o pai).
//   3) Arraste o objeto que tem ControladorCamera no campo "controlador".
//   4) Opcionalmente desative/exclua o objeto PontaDoDedo no Hierarchy.
public class VisualizadorMaoUI : MonoBehaviour
{
    [Header("Fonte dos dados da mao")]
    public ControladorCamera controlador;

    [Header("Visual")]
    public Color corLinha  = new Color(0f,   0.85f, 0.85f, 0.9f); // ciano
    public Color corPonto  = new Color(1f,   1f,    1f,    0.95f); // branco
    public Color corPulso  = new Color(1f,   0.8f,  0.1f,  1f);   // amarelo — ponto 0

    [Range(1f, 8f)]  public float espessuraLinha = 3f;
    [Range(6f, 20f)] public float tamanhoPonto   = 10f;

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
    private List<RectTransform> linhas = new List<RectTransform>();
    private List<RectTransform> pontos = new List<RectTransform>();

    void Awake()
    {
        meuRect = GetComponent<RectTransform>();

        int numOssos   = OSSOS.GetLength(0);
        for (int i = 0; i < numOssos; i++) linhas.Add(CriarLinha());
        for (int i = 0; i < 21; i++)       pontos.Add(CriarPonto(i));
    }

    RectTransform CriarLinha()
    {
        var go  = new GameObject("_Linha", typeof(Image));
        go.transform.SetParent(transform, false);
        go.GetComponent<Image>().color = corLinha;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, espessuraLinha);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        return rt;
    }

    RectTransform CriarPonto(int indice)
    {
        var go  = new GameObject("_Ponto" + indice, typeof(Image));
        go.transform.SetParent(transform, false);
        // Pulso (0) em amarelo, demais em branco
        go.GetComponent<Image>().color = (indice == 0) ? corPulso : corPonto;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(tamanhoPonto, tamanhoPonto);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        return rt;
    }

    void Update()
    {
        bool visivel = controlador != null && controlador.MaoDetectada
                       && controlador.PontosDaMaoAtuais != null;

        // Mostra ou esconde todos os elementos
        for (int i = 0; i < linhas.Count; i++) linhas[i].gameObject.SetActive(visivel);
        for (int i = 0; i < pontos.Count; i++) pontos[i].gameObject.SetActive(visivel);

        if (!visivel) return;

        Vector3[] pts = controlador.PontosDaMaoAtuais;
        float w = meuRect.rect.width;
        float h = meuRect.rect.height;

        // Atualiza posição dos pontos
        for (int i = 0; i < 21 && i < pontos.Count; i++)
            pontos[i].anchoredPosition = ParaUI(pts[i], w, h);

        // Atualiza posição e rotação das linhas
        for (int i = 0; i < OSSOS.GetLength(0); i++)
        {
            Vector2 a = ParaUI(pts[OSSOS[i, 0]], w, h);
            Vector2 b = ParaUI(pts[OSSOS[i, 1]], w, h);
            PosicionarLinha(linhas[i], a, b);
        }
    }

    // Converte keypoint normalizado (0-1) para coordenadas do RectTransform (centrado em 0,0)
    Vector2 ParaUI(Vector3 kp, float w, float h)
    {
        // Câmera: X cresce para direita, Y cresce para baixo
        // Unity UI: X cresce para direita, Y cresce para cima → inverte Y
        return new Vector2((kp.x - 0.5f) * w, (0.5f - kp.y) * h);
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
