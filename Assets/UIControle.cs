using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text;

[RequireComponent(typeof(TextMeshProUGUI))]
public class UIControle : MonoBehaviour
{
    [Header("Gerenciador do jogo")]
    public GerenciadorDeJogo gerenciador;

    [Header("Controlador da camera (para a barra do sinal)")]
    public ControladorCamera controlador;

    [Header("Texto da pontuacao (deixe vazio: e criado sozinho)")]
    public TextMeshProUGUI textoScore;

    private TextMeshProUGUI tmp;
    private TextMeshProUGUI rotulo;      // "PALAVRA:" pequeno no topo do cartão
    private GameObject      chipScore;   // fundo arredondado atrás da pontuação

    // Barra "reconhecendo o sinal X..." (feedback em tempo real)
    private GameObject      barraSinal;
    private Image           preenchimentoSinal;
    private TextMeshProUGUI letraSinal;
    private static readonly Color COR_BARRA_INICIO = new Color(0.2f, 0.8f, 1f,   0.9f);
    private static readonly Color COR_BARRA_FIM    = new Color(0.1f, 0.85f, 0.3f, 1f);

    // Vidas (corações) e relógio da palavra
    private GameObject chipVidas;
    private readonly System.Collections.Generic.List<GameObject> coracoes =
        new System.Collections.Generic.List<GameObject>();
    private GameObject      chipTempo;
    private TextMeshProUGUI textoTempo;
    private Transform       raizCanvas;   // raiz do Canvas (para confetes/flashes)
    private Image           flashTela;    // flash verde/vermelho (feedback visual)
    private static readonly Color COR_VIDA        = new Color(0.95f, 0.25f, 0.35f, 1f);
    private static readonly Color COR_TEMPO_OK    = Color.white;
    private static readonly Color COR_TEMPO_FIM   = new Color(1f, 0.35f, 0.25f, 1f);
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

        // Barra de progresso do sinal: mostra "estou quase aceitando a letra X"
        // - o jogador vê o sistema trabalhando em vez de achar que travou
        var fundoBarra = UIFabrica.CriarImagem(transform.parent, "BarraSinal",
            new Color(0f, 0f, 0f, 0.15f), new Vector2(20, 22), new Vector2(340, 16),
            UIFabrica.Arredondado(), true);
        UIFabrica.Ancorar(fundoBarra, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

        preenchimentoSinal = UIFabrica.CriarImagem(fundoBarra.transform, "Preenchimento",
            COR_BARRA_INICIO, Vector2.zero, new Vector2(340, 16),
            UIFabrica.Arredondado(), true);
        preenchimentoSinal.type       = Image.Type.Filled;
        preenchimentoSinal.fillMethod = Image.FillMethod.Horizontal;
        preenchimentoSinal.fillAmount = 0f;

        letraSinal = UIFabrica.CriarTexto(fundoBarra.transform, "Letra", "",
            34f, new Color(0.25f, 0.25f, 0.3f, 1f), new Vector2(-200, 2), new Vector2(60, 44));

        barraSinal = fundoBarra.gameObject;
        barraSinal.SetActive(false);

        raizCanvas = GetComponentInParent<Canvas>().transform;

        // Flash de tela: feedback VISUAL de acerto (verde) e erro (vermelho).
        // Essencial para o público surdo - os sons têm gêmeos visuais!
        flashTela = UIFabrica.CriarImagem(raizCanvas, "FlashTela",
            new Color(0f, 0f, 0f, 0f), Vector2.zero, Vector2.zero);
        flashTela.rectTransform.anchorMin = Vector2.zero;
        flashTela.rectTransform.anchorMax = Vector2.one;
        flashTela.rectTransform.sizeDelta = Vector2.zero;
        flashTela.raycastTarget = false;

        // Chip de VIDAS (corações), abaixo da pontuação
        var vidasChip = UIFabrica.CriarImagem(raizCanvas, "ChipVidas",
            new Color(0.08f, 0.10f, 0.30f, 0.75f), new Vector2(30, -135),
            new Vector2(330, 80), UIFabrica.Arredondado(), true);
        UIFabrica.Ancorar(vidasChip, new Vector2(0f, 1f), new Vector2(0f, 1f));
        for (int i = 0; i < 5; i++) // 5 = máximo de vidas
        {
            var coracao = UIFabrica.CriarImagem(vidasChip.transform, "Coracao" + i,
                COR_VIDA, new Vector2(-110 + i * 55, 0), new Vector2(46, 46),
                UIFabrica.Coracao());
            coracao.raycastTarget = false;
            coracoes.Add(coracao.gameObject);
        }
        chipVidas = vidasChip.gameObject;
        chipVidas.SetActive(false);

        // Relógio da palavra, no topo central
        var tempoChip = UIFabrica.CriarImagem(raizCanvas, "ChipTempo",
            new Color(0.08f, 0.10f, 0.30f, 0.75f), new Vector2(0, -30),
            new Vector2(170, 90), UIFabrica.Arredondado(), true);
        UIFabrica.Ancorar(tempoChip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        textoTempo = UIFabrica.CriarTexto(tempoChip.transform, "Texto", "0",
            48f, COR_TEMPO_OK, Vector2.zero, new Vector2(160, 90));
        chipTempo = tempoChip.gameObject;
        chipTempo.SetActive(false);
    }

    // OnEnable/OnDisable: o painel é ligado/desligado pelo MenuPrincipal,
    // então a inscrição nos eventos precisa acompanhar isso
    void OnEnable()  { Inscrever(); }
    void Start()     { Inscrever(); }

    void OnDisable()
    {
        celebrando = false; // corrotina morre junto com o objeto desativado

        // Painel desativado (menu aberto): esconde os chips também
        if (chipScore != null) chipScore.SetActive(false);
        if (chipVidas != null) chipVidas.SetActive(false);
        if (chipTempo != null) chipTempo.SetActive(false);

        if (!inscrito || gerenciador == null) return;
        gerenciador.OnPalavraCompleta     -= IniciarCelebracao;
        gerenciador.OnPontuacaoAtualizada -= AtualizarScore;
        gerenciador.OnVidasAtualizadas    -= AtualizarVidas;
        gerenciador.OnNovaFase            -= MostrarFase;
        gerenciador.OnPontosGastos        -= MostrarPontosGastos;
        gerenciador.OnLetraCorreta        -= FlashVerde;
        gerenciador.OnVidaPerdida         -= FlashVermelho;
        gerenciador.OnVidaGanha           -= AnimarVidaExtra;
        gerenciador.OnSemSaldo            -= AvisarSemSaldo;
        tremendoSaldo = false;
        inscrito = false;
    }

    void Inscrever()
    {
        if (inscrito || gerenciador == null) return;
        gerenciador.OnPalavraCompleta     += IniciarCelebracao;
        gerenciador.OnPontuacaoAtualizada += AtualizarScore;
        gerenciador.OnVidasAtualizadas    += AtualizarVidas;
        gerenciador.OnNovaFase            += MostrarFase;
        gerenciador.OnPontosGastos        += MostrarPontosGastos;
        gerenciador.OnLetraCorreta        += FlashVerde;
        gerenciador.OnVidaPerdida         += FlashVermelho;
        gerenciador.OnVidaGanha           += AnimarVidaExtra;
        gerenciador.OnSemSaldo            += AvisarSemSaldo;
        inscrito = true;
    }

    // Sem pontos para pular: o chip de pontos TREME e pisca vermelho
    // (feedback visual - o som de erro sozinho não serve ao público surdo)
    private bool tremendoSaldo = false;

    void AvisarSemSaldo()
    {
        if (!tremendoSaldo) StartCoroutine(RotinaSemSaldo());
    }

    IEnumerator RotinaSemSaldo()
    {
        if (chipScore == null) yield break;
        tremendoSaldo = true;

        var imagem      = chipScore.GetComponent<Image>();
        var rt          = (RectTransform)chipScore.transform;
        Color   corOriginal = imagem.color;
        Vector2 posOriginal = rt.anchoredPosition;
        Color   vermelho    = new Color(0.85f, 0.15f, 0.15f, 0.9f);

        float duracao = 0.5f;
        for (float t = 0f; t < duracao; t += Time.deltaTime)
        {
            float p = t / duracao;
            imagem.color = Color.Lerp(vermelho, corOriginal, p);
            // tremida horizontal que vai se acalmando
            rt.anchoredPosition = posOriginal +
                new Vector2(Mathf.Sin(p * 45f) * 12f * (1f - p), 0f);
            yield return null;
        }

        imagem.color        = corOriginal;
        rt.anchoredPosition = posOriginal;
        tremendoSaldo       = false;
    }

    // "+1 coracao": um coração com "+1" sobe perto do chip de vidas, e o coração
    // novo do chip aparece dando um "pulo" (escala 0 -> 1 com exagero)
    void AnimarVidaExtra()
    {
        StartCoroutine(RotinaVidaExtra());
    }

    IEnumerator RotinaVidaExtra()
    {
        // Coração flutuante com "+1" ao lado do chip de vidas
        var coracao = UIFabrica.CriarImagem(raizCanvas, "VidaExtra", COR_VIDA,
            new Vector2(380, -175), new Vector2(56, 56), UIFabrica.Coracao());
        UIFabrica.Ancorar(coracao, new Vector2(0f, 1f), new Vector2(0f, 1f));
        coracao.raycastTarget = false;
        var mais = UIFabrica.CriarTexto(coracao.transform, "Mais", "+1",
            36f, Color.white, new Vector2(52, 0), new Vector2(80, 56));

        // O coração novo do chip "pula" ao aparecer
        int indiceNovo = gerenciador.Vidas - 1;
        Transform coracaoDoChip = (indiceNovo >= 0 && indiceNovo < coracoes.Count)
                                  ? coracoes[indiceNovo].transform : null;

        var rt = coracao.rectTransform;
        Vector2 inicio = rt.anchoredPosition;
        float duracao = 1.3f;

        for (float t = 0f; t < duracao; t += Time.deltaTime)
        {
            float p = t / duracao;

            rt.anchoredPosition = inicio + new Vector2(0f, 100f * p);
            var c = coracao.color; c.a = 1f - p; coracao.color = c;
            var cm = mais.color;   cm.a = 1f - p; mais.color = cm;

            // "pulo" do coração do chip nos primeiros 40% da animação
            if (coracaoDoChip != null && p < 0.4f)
            {
                float pulo = Mathf.Sin((p / 0.4f) * Mathf.PI); // 0 -> 1 -> 0
                coracaoDoChip.localScale = Vector3.one * (1f + 0.6f * pulo);
            }
            yield return null;
        }

        if (coracaoDoChip != null) coracaoDoChip.localScale = Vector3.one;
        Destroy(coracao.gameObject);
    }

    void FlashVerde()    { StartCoroutine(RotinaDeFlash(new Color(0.2f, 0.9f, 0.3f, 0.30f))); }
    void FlashVermelho() { StartCoroutine(RotinaDeFlash(new Color(0.95f, 0.2f, 0.2f, 0.40f))); }

    IEnumerator RotinaDeFlash(Color cor)
    {
        if (flashTela == null) yield break;
        float duracao = 0.45f;
        for (float t = 0f; t < duracao; t += Time.deltaTime)
        {
            var c = cor;
            c.a = Mathf.Lerp(cor.a, 0f, t / duracao);
            flashTela.color = c;
            yield return null;
        }
        flashTela.color = new Color(0, 0, 0, 0);
    }

    // Animação "-5"/"-10" em vermelho subindo perto da pontuação
    void MostrarPontosGastos(int valor)
    {
        StartCoroutine(RotinaPontosGastos(valor));
    }

    IEnumerator RotinaPontosGastos(int valor)
    {
        var texto = UIFabrica.CriarTexto(raizCanvas, "PontosGastos", "-" + valor,
            52f, new Color(0.95f, 0.2f, 0.2f, 1f), new Vector2(200, -70), new Vector2(200, 80));
        UIFabrica.Ancorar(texto, new Vector2(0f, 1f), new Vector2(0f, 1f));

        var rt = (RectTransform)texto.transform;
        Vector2 inicio = rt.anchoredPosition;
        float duracao = 1.1f;

        for (float t = 0f; t < duracao; t += Time.deltaTime)
        {
            float p = t / duracao;
            rt.anchoredPosition = inicio + new Vector2(0f, 90f * p); // sobe
            var c = texto.color;
            c.a = 1f - p;                                            // some
            texto.color = c;
            yield return null;
        }
        Destroy(texto.gameObject);
    }

    void AtualizarVidas(int vidas)
    {
        for (int i = 0; i < coracoes.Count; i++)
            coracoes[i].SetActive(i < vidas);
    }

    void MostrarFase(string texto)
    {
        StartCoroutine(RotinaDeFase(texto));
    }

    IEnumerator RotinaDeFase(string texto)
    {
        celebrando = true;
        rotulo.gameObject.SetActive(false);
        if (barraSinal != null) barraSinal.SetActive(false);

        tmp.color = new Color(0.15f, 0.4f, 0.85f, 1f); // azul de fase
        tmp.text  = texto;
        yield return new WaitForSeconds(2.2f);

        tmp.color       = COR_NORMAL;
        tmp.text        = "";
        palavraAnterior = null;
        indiceAnterior  = -1;
        celebrando      = false;
    }

    void AtualizarScore(int score)
    {
        if (textoScore != null)
            textoScore.text = "PONTOS: " + score;
    }

    void Update()
    {
        if (gerenciador == null || celebrando) return;

        // Chips (pontos, vidas, tempo) só aparecem com o jogo rodando
        bool rodando = gerenciador.JogoIniciado;
        bool jogando = rodando && !gerenciador.JogoTerminado;
        if (chipScore != null && chipScore.activeSelf != rodando) chipScore.SetActive(rodando);
        if (chipVidas != null && chipVidas.activeSelf != rodando) chipVidas.SetActive(rodando);
        if (chipTempo != null && chipTempo.activeSelf != jogando) chipTempo.SetActive(jogando);

        // Relógio da palavra: fica vermelho nos últimos 5 segundos
        if (jogando && textoTempo != null)
        {
            int segundos = Mathf.CeilToInt(gerenciador.TempoRestante);
            textoTempo.text  = segundos.ToString();
            textoTempo.color = (segundos <= 5) ? COR_TEMPO_FIM : COR_TEMPO_OK;
        }

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
                barraSinal.SetActive(false);
                if (gerenciador.Venceu)
                {
                    tmp.color = COR_CELEBRACAO;
                    tmp.text  = "PARABÉNS!\nVOCÊ VENCEU!";
                    ChuvaDeConfetes.Lancar(raizCanvas);
                }
                else
                {
                    tmp.color = COR_PARABENS;
                    tmp.text  = "FIM DE JOGO";
                }
                palavraAnterior = "FIM";
            }
            return;
        }

        AtualizarBarraSinal();

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

    // Mostra/atualiza a barrinha "reconhecendo o sinal X..."
    // Quando a letra esperada é DINÂMICA, vira uma dica pulsante de movimento.
    void AtualizarBarraSinal()
    {
        if (barraSinal == null || controlador == null) return;

        // Letra com movimento: barra pulsa como convite para mexer a mão
        if (controlador.EsperandoMovimento)
        {
            if (!barraSinal.activeSelf) barraSinal.SetActive(true);
            preenchimentoSinal.fillAmount = Mathf.PingPong(Time.time * 0.7f, 1f);
            preenchimentoSinal.color      = COR_BARRA_INICIO;
            letraSinal.text = "MOV";
            return;
        }

        string candidata = controlador.LetraCandidata;
        bool mostrar = !string.IsNullOrEmpty(candidata);

        if (barraSinal.activeSelf != mostrar) barraSinal.SetActive(mostrar);
        if (!mostrar) return;

        float progresso = controlador.ProgressoCandidata;
        preenchimentoSinal.fillAmount = progresso;
        preenchimentoSinal.color = Color.Lerp(COR_BARRA_INICIO, COR_BARRA_FIM, progresso);
        letraSinal.text = candidata;
    }

    IEnumerator RotinaDeCelebracao(string palavra)
    {
        celebrando = true;
        if (barraSinal != null) barraSinal.SetActive(false);

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
        // Mostra o NOME do objeto (não é trapaça: soletrar em LIBRAS é o desafio,
        // e ler a palavra escrita é justamente a parte de alfabetização!)
        rotulo.text = "PALAVRA:  " + palavra;
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
