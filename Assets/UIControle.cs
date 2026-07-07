using UnityEngine;
using TMPro;
using System.Collections;
using System.Text;

[RequireComponent(typeof(TextMeshProUGUI))]
public class UIControle : MonoBehaviour
{
    [Header("Gerenciador do jogo")]
    public GerenciadorDeJogo gerenciador;

    [Header("Texto da pontuacao (deixe vazio: e criado sozinho)")]
    public TextMeshProUGUI textoScore;

    private TextMeshProUGUI tmp;
    private TextMeshProUGUI rotulo;      // "PALAVRA:" pequeno no topo do cartão
    private GameObject      chipScore;   // fundo arredondado atrás da pontuação
    private string palavraAnterior = null;
    private int    indiceAnterior  = -1;
    private bool   celebrando      = false;
    private bool   inscrito        = false;

    private static readonly Color COR_NORMAL     = new Color(0.12f, 0.12f, 0.12f, 1f);
    private static readonly Color COR_CELEBRACAO = new Color(0.1f,  0.65f, 0.15f, 1f);
    private static readonly Color COR_PARABENS   = new Color(0.85f, 0.55f, 0f,    1f);
    private static readonly Color COR_ROTULO     = new Color(0.45f, 0.45f, 0.52f, 1f);

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        tmp.richText           = false;
        tmp.enableAutoSizing   = true;
        tmp.fontSizeMin        = 20f;
        tmp.fontSizeMax        = 90f;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.color              = COR_NORMAL;
        tmp.margin             = new Vector4(0, 50, 0, 0); // abre espaço para o rótulo
        tmp.text               = "";

        // Rótulo "PALAVRA:" preso ao topo do cartão (igual ao app de referência)
        rotulo = UIFabrica.CriarTexto(transform.parent, "RotuloPalavra", "PALAVRA:",
            36f, COR_ROTULO, new Vector2(0, -14), new Vector2(900, 50));
        UIFabrica.Ancorar(rotulo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        // Pontuação: cria sozinho um "chip" no canto superior esquerdo da tela
        if (textoScore == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            var chip = UIFabrica.CriarImagem(canvas.transform, "ChipScore",
                new Color(0.08f, 0.10f, 0.30f, 0.75f), new Vector2(30, -30),
                new Vector2(330, 90), UIFabrica.Arredondado(), true);
            UIFabrica.Ancorar(chip, new Vector2(0f, 1f), new Vector2(0f, 1f));
            textoScore = UIFabrica.CriarTexto(chip.transform, "TextoScore", "PONTOS: 0",
                42f, Color.white, Vector2.zero, new Vector2(320, 90));
            chipScore = chip.gameObject;
            chipScore.SetActive(false); // só aparece durante o jogo
        }
    }

    // OnEnable/OnDisable: o painel é ligado/desligado pelo MenuPrincipal,
    // então a inscrição nos eventos precisa acompanhar isso
    void OnEnable()  { Inscrever(); }
    void Start()     { Inscrever(); }

    void OnDisable()
    {
        celebrando = false; // corrotina morre junto com o objeto desativado
        if (!inscrito || gerenciador == null) return;
        gerenciador.OnPalavraCompleta     -= IniciarCelebracao;
        gerenciador.OnPontuacaoAtualizada -= AtualizarScore;
        inscrito = false;
    }

    void Inscrever()
    {
        if (inscrito || gerenciador == null) return;
        gerenciador.OnPalavraCompleta     += IniciarCelebracao;
        gerenciador.OnPontuacaoAtualizada += AtualizarScore;
        inscrito = true;
    }

    void AtualizarScore(int score)
    {
        if (textoScore != null)
            textoScore.text = "PONTOS: " + score;
    }

    void Update()
    {
        if (gerenciador == null || celebrando) return;

        // Chip de pontos só aparece com o jogo rodando
        if (chipScore != null && chipScore.activeSelf != gerenciador.JogoIniciado)
            chipScore.SetActive(gerenciador.JogoIniciado);

        // No menu / treinamento não há palavra para mostrar
        if (!gerenciador.JogoIniciado)
        {
            if (palavraAnterior != null)
            {
                tmp.text        = "";
                palavraAnterior = null;
                indiceAnterior  = -1;
            }
            return;
        }

        if (gerenciador.JogoTerminado)
        {
            if (palavraAnterior != "FIM")
            {
                rotulo.gameObject.SetActive(false);
                tmp.color       = COR_PARABENS;
                tmp.text        = "FIM DO JOGO!";
                palavraAnterior = "FIM";
            }
            return;
        }

        string palavraAtual = gerenciador.PalavraAtual;
        int    indiceAtual  = gerenciador.IndiceLetraAtual;

        if (palavraAtual == palavraAnterior && indiceAtual == indiceAnterior) return;

        palavraAnterior = palavraAtual;
        indiceAnterior  = indiceAtual;

        ExibirPalavra(palavraAtual, indiceAtual);
    }

    void IniciarCelebracao(string palavraCompleta)
    {
        StartCoroutine(RotinaDeCelebracao(palavraCompleta));
    }

    IEnumerator RotinaDeCelebracao(string palavra)
    {
        celebrando = true;

        // Mostra palavra completa em verde
        tmp.color = COR_CELEBRACAO;
        MostrarPalavraCompleta(palavra);
        yield return new WaitForSeconds(1.2f);

        // Mensagem de parabéns
        rotulo.gameObject.SetActive(false);
        tmp.text = "MUITO BEM!";
        yield return new WaitForSeconds(1.0f);

        tmp.color       = COR_NORMAL;
        tmp.text        = "";
        palavraAnterior = null;
        indiceAnterior  = -1;
        celebrando      = false;
    }

    void MostrarPalavraCompleta(string palavra)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < palavra.Length; i++)
        {
            if (i > 0) sb.Append("  ");
            sb.Append(palavra[i]);
        }
        tmp.text = sb.ToString();
    }

    void ExibirPalavra(string palavra, int preenchidas)
    {
        if (string.IsNullOrEmpty(palavra)) { tmp.text = ""; return; }

        rotulo.gameObject.SetActive(true);
        tmp.color = COR_NORMAL;

        var sb = new StringBuilder();
        for (int i = 0; i < palavra.Length; i++)
        {
            if (i > 0) sb.Append("  ");
            sb.Append(i < preenchidas ? palavra[i] : '_');
        }
        tmp.text = sb.ToString();
    }
}
