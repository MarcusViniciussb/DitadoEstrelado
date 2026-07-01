using UnityEngine;
using TMPro;
using System.Collections;
using System.Text;

[RequireComponent(typeof(TextMeshProUGUI))]
public class UIControle : MonoBehaviour
{
    [Header("Gerenciador do jogo")]
    public GerenciadorDeJogo gerenciador;

    [Header("Texto da pontuacao (crie um segundo Text TMP e arraste aqui)")]
    public TextMeshProUGUI textoScore;

    private TextMeshProUGUI tmp;
    private string palavraAnterior = null;
    private int    indiceAnterior  = -1;
    private bool   celebrando      = false;

    private static readonly Color COR_NORMAL     = new Color(0.12f, 0.12f, 0.12f, 1f);
    private static readonly Color COR_CELEBRACAO = new Color(0.1f,  0.65f, 0.15f, 1f);
    private static readonly Color COR_PARABENS   = new Color(0.85f, 0.55f, 0f,    1f);
    private static readonly Color COR_SCORE      = new Color(0.1f,  0.1f,  0.5f,  1f);

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        tmp.richText           = false;
        tmp.enableAutoSizing   = true;
        tmp.fontSizeMin        = 20f;
        tmp.fontSizeMax        = 72f;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.color              = COR_NORMAL;

        if (textoScore != null)
        {
            textoScore.richText  = false;
            textoScore.fontSize  = 28f;
            textoScore.color     = COR_SCORE;
            textoScore.text      = "PONTOS: 0";
        }
    }

    void Start()
    {
        if (gerenciador == null)
        {
            Debug.LogError("UIControle: 'gerenciador' nao conectado!");
            enabled = false;
            return;
        }
        gerenciador.OnPalavraCompleta    += IniciarCelebracao;
        gerenciador.OnPontuacaoAtualizada += AtualizarScore;
        tmp.text = "";
    }

    void OnDestroy()
    {
        if (gerenciador == null) return;
        gerenciador.OnPalavraCompleta    -= IniciarCelebracao;
        gerenciador.OnPontuacaoAtualizada -= AtualizarScore;
    }

    void AtualizarScore(int score)
    {
        if (textoScore != null)
            textoScore.text = "PONTOS: " + score;
    }

    void Update()
    {
        if (gerenciador == null || celebrando) return;

        if (gerenciador.JogoTerminado)
        {
            if (palavraAnterior != "FIM")
            {
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
