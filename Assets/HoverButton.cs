using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// HoverButton: seleciona um botão quando a mão fica parada sobre ele por 3 segundos.
//
// COMO CONFIGURAR:
//   1) Crie um GameObject filho deste botão chamado "CirculoHover"
//   2) Adicione um componente Image nele (Image Type = Filled, Fill Method = Radial 360,
//      Fill Origin = Top, Fill Amount = 0, Clockwise = true)
//   3) Arraste esse Image para o campo "imagemCirculo" no Inspector
//   4) Arraste o objeto que tem ControladorCamera para o campo "controlador"
//   5) Configure o evento "Ao Selecionar" com o método que deve ser chamado
//      (ex: GerenciadorDeJogo.PularPalavra)
public class HoverButton : MonoBehaviour
{
    [Header("Fonte dos dados da mao")]
    public ControladorCamera controlador;

    [Header("Circulo de progresso")]
    public Image imagemCirculo;       // Image com Fill Method = Radial 360
    public float tempoParaSelecionar = 3f;

    [Header("Evento disparado ao selecionar")]
    public UnityEvent aoSelecionar;

    [Header("Visual")]
    public Color corCirculoNormal    = new Color(0.2f, 0.8f, 1f, 0.8f);
    public Color corCirculoCompleto  = new Color(0.1f, 1f, 0.3f, 1f);

    private float       tempoHover = 0f;
    private bool        selecionando = false;
    private RectTransform meuRect;
    private Canvas        canvas;

    void Awake()
    {
        meuRect = GetComponent<RectTransform>();
        canvas  = GetComponentInParent<Canvas>();

        if (imagemCirculo != null)
        {
            imagemCirculo.type       = Image.Type.Filled;
            imagemCirculo.fillMethod = Image.FillMethod.Radial360;
            imagemCirculo.fillOrigin = (int)Image.Origin360.Top;
            imagemCirculo.fillAmount = 0f;
            imagemCirculo.color      = corCirculoNormal;
        }
    }

    void Update()
    {
        // Só ativa se houver mão detectada
        if (controlador == null || !controlador.MaoDetectada ||
            controlador.PontosDaMaoAtuais == null)
        {
            ResetarHover();
            return;
        }

        Vector2 posicaoMao = ObterPosicaoMaoNaTela();
        bool sobre = EstaSobreEste(posicaoMao);

        if (sobre)
        {
            tempoHover += Time.deltaTime;
            float progresso = Mathf.Clamp01(tempoHover / tempoParaSelecionar);

            if (imagemCirculo != null)
            {
                imagemCirculo.fillAmount = progresso;
                imagemCirculo.color = Color.Lerp(corCirculoNormal, corCirculoCompleto, progresso);
            }

            if (!selecionando && tempoHover >= tempoParaSelecionar)
            {
                selecionando = true;
                Selecionar();
            }
        }
        else
        {
            // Fora do botão: recua o círculo mais rápido do que avança
            tempoHover = Mathf.Max(0f, tempoHover - Time.deltaTime * 1.5f);
            if (imagemCirculo != null)
                imagemCirculo.fillAmount = Mathf.Clamp01(tempoHover / tempoParaSelecionar);

            if (tempoHover <= 0f)
                selecionando = false;
        }
    }

    void Selecionar()
    {
        Debug.Log("HoverButton: " + gameObject.name + " selecionado pela mao!");
        aoSelecionar?.Invoke();

        // Dispara também o onClick do Button padrão, se existir
        GetComponent<Button>()?.onClick?.Invoke();

        // Reseta após um breve delay para evitar duplo-disparo
        Invoke(nameof(ResetarHover), 0.5f);
    }

    void ResetarHover()
    {
        tempoHover   = 0f;
        selecionando = false;
        if (imagemCirculo != null) imagemCirculo.fillAmount = 0f;
    }

    // Pega a posição do dedo indicador (ponto 8) em coordenadas de tela.
    // Usa a conversão do ControladorCamera, que já desconta recorte e espelho —
    // assim o círculo enche exatamente onde a mão APARECE na tela.
    Vector2 ObterPosicaoMaoNaTela()
    {
        Vector3 kp = controlador.PontosDaMaoAtuais[8]; // ponta do indicador
        return controlador.PontoParaTela(kp);
    }

    // Verifica se a posição de tela está dentro dos limites deste RectTransform
    bool EstaSobreEste(Vector2 screenPos)
    {
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                     ? canvas.worldCamera : null;
        Vector2 localPos;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                   meuRect, screenPos, cam, out localPos)
               && meuRect.rect.Contains(localPos);
    }

    // Desenha um ponto no editor para visualizar onde a mão está
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || controlador?.PontosDaMaoAtuais == null) return;
        Gizmos.color = Color.cyan;
        Vector2 sp = ObterPosicaoMaoNaTela();
        Gizmos.DrawSphere(new Vector3(sp.x, sp.y, 0), 10f);
    }
}
