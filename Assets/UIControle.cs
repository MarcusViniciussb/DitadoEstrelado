using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text;
using UnityEngine.Events;

[RequireComponent(typeof(TextMeshProUGUI))]
public class UIControle : MonoBehaviour
{
    [Header("Ajuste do HUD superior (fracoes 0..1 da faixa)")]
    [Tooltip("X,Y do numero de PONTOS sobre o desenho")]      public Vector2 ancoraPontos = new Vector2(0.168f, 0.71f);
    [Tooltip("X,Y do 1o coracao (as VIDAS)")]                  public Vector2 ancoraVidas  = new Vector2(0.060f, 0.31f);
    [Tooltip("X,Y do numero do TEMPO sobre a estrela")]        public Vector2 ancoraTempo  = new Vector2(0.260f, 0.463f);
    [Tooltip("Tamanho de cada coracao")]                       public float   tamanhoCoracao = 28f;
    [Tooltip("Distancia entre os coracoes")]                   public float   espacoCoracao  = 32f;

    [Header("Gerenciador do jogo")]
    public GerenciadorDeJogo gerenciador;

    [Header("Controlador da camera (para a barra do sinal)")]
    public ControladorCamera controlador;

    [Header("Texto da pontuacao (deixe vazio: e criado sozinho)")]
    public TextMeshProUGUI textoScore;

    private TextMeshProUGUI tmp;
    private TextMeshProUGUI rotulo;      // "PALAVRA:" pequeno no topo do cartão
    private GameObject      chipScore;   // agora aponta para o painel de stats (imagem HUD)
    private GameObject      painelStats;  // imagem HUD_Superior (cluster esquerdo)
    private GameObject      containerVidas;
    private RectTransform   hudRect;       // faixa HUD (mantem o aspecto)
    private MenuPrincipal   menuPrincipal; // para o botao MENU invisivel
    private Image           riscoSomHud;   // risco do som na faixa

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
        tmp.fontSizeMax        = 130f;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.color              = COR_NORMAL;
        tmp.margin             = new Vector4(0, 42, 0, 64); // tracejado mais alto (a letra identificada fica visivel)
        tmp.text               = "";

        // Rótulo "PALAVRA:" preso ao topo do cartão (igual ao app de referência)
        rotulo = UIFabrica.CriarTexto(transform.parent, "RotuloPalavra", "PALAVRA:",
            36f, COR_ROTULO, new Vector2(0, 4), new Vector2(900, 50));
        UIFabrica.Ancorar(rotulo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        // Painel de estatisticas: imagem HUD_Superior (recorte do cluster
        // esquerdo) com PONTOS, VIDAS e a estrela do tempo. Os valores dinamicos
        // sao FILHOS da imagem, ancorados por fracao, entao ficam sempre no lugar
        // certo por cima do desenho, em qualquer tamanho de tela.
        {
            var canvas = GetComponentInParent<Canvas>();
            var hud = UIFabrica.CriarImagem(canvas.transform, "HudSuperior",
                Color.white, Vector2.zero, new Vector2(0, 258), Resources.Load<Sprite>("ui/hud"));
            hud.raycastTarget = false;
            var hr = hud.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f); hr.anchoredPosition = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            float wRef = hr.rect.width;
            if (wRef < 1f) { var cr = canvas.GetComponent<RectTransform>(); wRef = cr != null ? cr.rect.width : 1920f; }
            hr.sizeDelta = new Vector2(0f, wRef * 0.1345f); // ja no tamanho certo, sem atraso
            painelStats = hud.gameObject; chipScore = painelStats; hudRect = hr;

            textoScore = OverlayTexto(hud.transform, "Pontos", "0", 46f, Color.white,
                ancoraPontos,  new Vector2(0f, 0.5f), TextAlignmentOptions.Left);
            containerVidas = OverlayTexto(hud.transform, "Vidas", "", 10f, Color.white,
                ancoraVidas,   new Vector2(0f, 0.5f), TextAlignmentOptions.Left).gameObject;
            textoTempo = OverlayTexto(hud.transform, "Tempo", "0", 68f, COR_TEMPO_OK,
                ancoraTempo,   new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);

            for (int i = 0; i < 5; i++)
            {
                var coracao = UIFabrica.CriarImagem(containerVidas.transform, "Coracao" + i,
                    COR_VIDA, new Vector2(i * espacoCoracao, 0),
                    new Vector2(tamanhoCoracao, tamanhoCoracao), UIFabrica.Coracao());
                coracao.raycastTarget = false;
                coracoes.Add(coracao.gameObject);
            }

            // MENU e SOM ja estao desenhados na imagem; botoes invisiveis por cima
            menuPrincipal = FindObjectOfType<MenuPrincipal>();
            HitInvisivel(hud.transform, "HitMenu", new Vector2(0.878f, 0.55f), new Vector2(0.985f, 0.95f),
                () => { if (menuPrincipal != null) menuPrincipal.AbrirMenuComSom(); });
            var hitSom = HitInvisivel(hud.transform, "HitSom", new Vector2(0.905f, 0.05f), new Vector2(0.99f, 0.48f),
                AlternarSomHud);
            riscoSomHud = UIFabrica.CriarImagem(hitSom, "Risco",
                new Color(0.9f, 0.2f, 0.2f, 0.95f), Vector2.zero, new Vector2(70, 8),
                UIFabrica.Arredondado(), true);
            riscoSomHud.rectTransform.localEulerAngles = new Vector3(0, 0, 20f);
            riscoSomHud.raycastTarget = false;
            riscoSomHud.gameObject.SetActive(!GerenciadorDeAudio.MusicaLigada);

            painelStats.SetActive(false);
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

    }

    // Botao invisivel (so area de clique) sobre um trecho da imagem do HUD.
    Transform HitInvisivel(Transform pai, string nome, Vector2 aMin, Vector2 aMax, UnityAction acao)
    {
        var img = UIFabrica.CriarImagem(pai, nome, new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.zero);
        var rt = img.rectTransform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        img.raycastTarget = true;
        img.gameObject.AddComponent<Button>().onClick.AddListener(acao);
        return img.transform;
    }

    void AlternarSomHud()
    {
        GerenciadorDeAudio.TocarClique();
        GerenciadorDeAudio.AlternarMusica();
        if (riscoSomHud != null) riscoSomHud.gameObject.SetActive(!GerenciadorDeAudio.MusicaLigada);
    }

    // Mantem a faixa do HUD na proporcao certa (altura = largura x aspecto)
    void LateUpdate()
    {
        if (hudRect == null) return;
        float alvo = hudRect.rect.width * 0.1345f;
        if (Mathf.Abs(hudRect.sizeDelta.y - alvo) > 1f)
            hudRect.sizeDelta = new Vector2(0f, alvo);
    }

    // Cria um texto ancorado por FRACAO (0..1) dentro de um pai, para ficar
    // por cima de um ponto especifico da imagem do HUD.
    TextMeshProUGUI OverlayTexto(Transform pai, string nome, string txt, float tam,
        Color cor, Vector2 anc, Vector2 pivo, TextAlignmentOptions alinhamento)
    {
        var t = UIFabrica.CriarTexto(pai, nome, txt, tam, cor, Vector2.zero, new Vector2(260, 80));
        t.alignment = alinhamento;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anc;
        rt.pivot = pivo;
        rt.anchoredPosition = Vector2.zero;
        return t;
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
            textoScore.text = score.ToString();
    }

    void Update()
    {
        if (gerenciador == null || celebrando) return;

        // Chips (pontos, vidas, tempo) só aparecem com o jogo rodando.
        // No modo sem pressão, tempo e vidas nem existem na tela.
        bool rodando    = gerenciador.JogoIniciado;
        bool jogando    = rodando && !gerenciador.JogoTerminado;
        bool comPressao = !gerenciador.modoSemPressao;
        bool verVidas   = rodando && comPressao;
        bool verTempo   = jogando && comPressao;
        if (chipScore != null && chipScore.activeSelf != rodando)  chipScore.SetActive(rodando);
        if (chipVidas != null && chipVidas.activeSelf != verVidas) chipVidas.SetActive(verVidas);
        if (chipTempo != null && chipTempo.activeSelf != verTempo) chipTempo.SetActive(verTempo);

        // Relógio da palavra: fica vermelho nos últimos 5 segundos
        if (verTempo && textoTempo != null)
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

    // Abre espaço à ESQUERDA do cartão para o objeto 3D ficar ao lado da
    // palavra (as letras, o rótulo e a barrinha deslocam para a direita).
    // Chamado pelo MenuPrincipal quando a orientação da tela muda.
    public void DefinirEspacoDoObjeto(float margemEsquerda, float deslocamentoX)
    {
        if (tmp != null)
        {
            var margem = tmp.margin;
            margem.x   = margemEsquerda;
            tmp.margin = margem;
        }
        if (rotulo != null)
        {
            var rt = rotulo.rectTransform;
            rt.anchoredPosition = new Vector2(deslocamentoX, rt.anchoredPosition.y);
        }
        if (barraSinal != null)
        {
            var rt = (RectTransform)barraSinal.transform;
            rt.anchoredPosition = new Vector2(deslocamentoX + 20f, rt.anchoredPosition.y);
        }
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
