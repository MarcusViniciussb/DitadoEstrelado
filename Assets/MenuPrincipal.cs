using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// MenuPrincipal: constrói o menu inicial e o HUD do jogo por código.
// Fica no Canvas. Controla a troca entre MENU <-> JOGO <-> TREINAMENTO.
//
// Todos os botões funcionam de 3 formas: toque na tela, clique do mouse,
// ou mão parada sobre o botão por 3 segundos (HoverButton).
public class MenuPrincipal : MonoBehaviour
{
    [Header("Referências (arrastar no Inspector)")]
    public GerenciadorDeJogo gerenciador;
    public ControladorCamera controlador;
    public GameObject painelPalavra; // Panel com o texto da palavra
    public GameObject botaoPular;    // Botão PULAR PALAVRA da cena

    // Paleta de cores do jogo
    // Fundo TRANSLÚCIDO (alfa < 1): a câmera aparece atrás do menu,
    // então o jogador se vê e consegue selecionar botões com a mão
    static readonly Color COR_FUNDO_TOPO = new Color(0.09f, 0.11f, 0.32f, 0.72f); // azul-noite
    static readonly Color COR_FUNDO_BASE = new Color(0.32f, 0.16f, 0.46f, 0.72f); // roxo
    static readonly Color COR_TITULO     = new Color(0.969f, 0.878f, 0.455f, 1f); // #F7E074 amarelo-estrela
    static readonly Color COR_JOGAR      = new Color(0.149f, 0.816f, 0.486f, 1f); // #26D07C verde vibrante
    static readonly Color COR_SINAIS     = new Color(0.922f, 0.596f, 0.184f, 1f); // #EB982F laranja aprendizado
    static readonly Color COR_TREINAR    = new Color(0.149f, 0.584f, 0.816f, 1f); // #2695D0 azul foco
    static readonly Color COR_SAIR       = new Color(0.816f, 0.282f, 0.227f, 1f); // #D0483A vermelho sair
    static readonly Color COR_HUD        = new Color(0.102f, 0.137f, 0.494f, 1f); // #1A237E

    [Header("Logo Universo IF (canto superior; desmarque para remover)")]
    public bool mostrarLogo = true;
    GameObject logoUniverso;

    [Header("Senha da area do professor (modo treinamento)")]
    public string senhaAdmin = "1234";

    GameObject telaMenu;
    GameObject botaoMenuHud;
    GameObject headerBar;          // barra unica #1A237E no topo, durante o jogo
    GameObject dicaTreinamento;
    TextMeshProUGUI textoContagem; // contador de amostras por letra (treinamento)

    GameObject botaoSom;           // liga/desliga a música (sempre visível)
    GameObject riscoSom;           // risco vermelho = música desligada
    GameObject botaoPularLetra;    // "PULAR LETRA -5" (aparece junto do outro)
    GameObject botaoContinuar;     // "CONTINUAR" (só quando há jogo pausado)
    TextMeshProUGUI rotuloJogar;   // vira "RECOMEÇAR" quando há jogo pausado

    GameObject painelSenha;        // teclado numérico da área do professor
    TextMeshProUGUI displaySenha;
    TextMeshProUGUI textoRecorde;  // "RECORDE: X" no menu
    string senhaDigitada = "";

    bool  menuAberto;
    bool  jogoPausado;
    float timerFimDeJogo;
    float timerContagem;

    // Distancia da camera em que o objeto 3D e exibido
    const float DISTANCIA_DO_OBJETO = 15f;
    float larguraDoSlot = 300f;   // espaco reservado na esquerda do cartao
    int   larguraDaTelaAnterior, alturaDaTelaAnterior;

    // Orientacao da tela: false = celular (retrato), true = PC/tablet (paisagem)
    bool telaHorizontal;
    // Verdadeiro quando o usuario escolheu o formato a dedo, nas opcoes.
    // Enquanto for falso, o formato acompanha a janela.
    bool escolhaDeTelaFeita;
    int  larguraJanelaAnterior, alturaJanelaAnterior;
    CanvasScaler escalador;

    // Painel de opções (engrenagem): tela, tempo/vidas, espelho da câmera
    GameObject painelOpcoes;
    TextMeshProUGUI rotuloTela;
    TextMeshProUGUI rotuloPressao;
    TextMeshProUGUI rotuloEspelho;
    TextMeshProUGUI tmpTitulo1;    // texto do título (muda entre os modos)
    UIControle uiControle;         // para abrir o espaço do objeto no cartão

    // Referencias para reposicionar o menu conforme a orientacao
    RectTransform rtTitulo1, rtTitulo2, rtSubtitulo, rtRecorde;
    RectTransform rtContinuar, rtJogar, rtAprender, rtTreinar, rtSair, rtDicaMenu, rtCreditos;
    ModoEstudo telaEstudo;
    readonly List<RectTransform> estrelasFundo = new List<RectTransform>();
    readonly List<Vector2>       estrelasBase  = new List<Vector2>();
    readonly List<RectTransform> brilhosTitulo = new List<RectTransform>();
    readonly List<Vector2>       brilhosBase   = new List<Vector2>();

    void Awake()
    {
        ConstruirMenu();
        ConstruirHud();
        CriarLogo();
    }

    void Start()
    {
        // Recupera as escolhas da última vez (tela, tempo/vidas, espelho)
        // Sem escolha salva, o formato segue o formato REAL da janela. O
        // padrao antigo era sempre retrato, o que no computador montava um
        // menu de celular numa janela larga e empilhava os elementos uns
        // sobre os outros. So passa a valer a preferencia depois que alguem
        // troca de propósito, pelas opcoes.
        escolhaDeTelaFeita = PlayerPrefs.GetInt("telaEscolhida", 0) == 1;
        telaHorizontal = escolhaDeTelaFeita
            ? PlayerPrefs.GetInt("telaHorizontal", 0) == 1
            : Screen.width >= Screen.height;
        if (gerenciador != null)
            gerenciador.modoSemPressao = PlayerPrefs.GetInt("semPressao", 0) == 1;
        AplicarEspelho(PlayerPrefs.GetInt("espelharImagem", 1) == 1);
        AplicarOrientacao();

        AbrirMenu();
        TrazerLogoAFrente();
        GerenciadorDeAudio.TocarMusica();
        // O jogador pode ter desligado a música na última vez - reflete no ícone
        riscoSom.SetActive(!GerenciadorDeAudio.MusicaLigada);
    }

    void Update()
    {
        // Janela redimensionada no computador: enquanto ninguem tiver
        // escolhido o formato a dedo, o layout acompanha a janela
        if (Screen.width != larguraJanelaAnterior || Screen.height != alturaJanelaAnterior)
        {
            larguraJanelaAnterior = Screen.width;
            alturaJanelaAnterior  = Screen.height;

            if (!escolhaDeTelaFeita)
            {
                bool deitada = Screen.width >= Screen.height;
                if (deitada != telaHorizontal)
                {
                    telaHorizontal = deitada;
                    AplicarOrientacao();
                }
            }
        }

        // Quando o jogo termina, espera e volta ao menu
        // (na vitória espera mais: tempo de curtir os confetes!)
        if (!menuAberto && gerenciador != null && gerenciador.JogoTerminado)
        {
            timerFimDeJogo += Time.deltaTime;
            if (timerFimDeJogo >= (gerenciador.Venceu ? 8f : 4f)) AbrirMenu();
        }
        else
        {
            timerFimDeJogo = 0f;
        }

        // Se a janela mudar de tamanho, o objeto 3D reencontra o cartão
        if (Screen.width != larguraDaTelaAnterior || Screen.height != alturaDaTelaAnterior)
        {
            larguraDaTelaAnterior = Screen.width;
            alturaDaTelaAnterior  = Screen.height;
            PosicionarObjeto3D();
        }

        // No treinamento, atualiza o contador de amostras a cada meio segundo
        if (dicaTreinamento != null && dicaTreinamento.activeSelf &&
            controlador != null && controlador.reconhecedor != null)
        {
            timerContagem -= Time.deltaTime;
            if (timerContagem <= 0f)
            {
                timerContagem = 0.5f;
                textoContagem.text = controlador.reconhecedor.ResumoDoBanco();
            }
        }
    }

    // ── Construção da interface ──────────────────────────────────────────────

    void ConstruirMenu()
    {
        // Fundo em gradiente (azul-noite -> roxo) cobrindo a tela inteira
        var fundo = UIFabrica.CriarImagem(transform, "TelaMenu", Color.white,
            Vector2.zero, Vector2.zero, UIFabrica.Gradiente(COR_FUNDO_TOPO, COR_FUNDO_BASE));
        var rt = fundo.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        telaMenu = fundo.gameObject;

        // Estrelas decorativas cintilando (cada uma no seu ritmo)
        var sorteio = new System.Random(7);
        for (int i = 0; i < 45; i++)
        {
            float x    = (float)sorteio.NextDouble() * 1000f - 500f;
            float y    = (float)sorteio.NextDouble() * 1780f - 890f;
            float tam  = 4f + (float)sorteio.NextDouble() * 10f;
            float alfa = 0.25f + (float)sorteio.NextDouble() * 0.6f;
            var estrela = UIFabrica.CriarImagem(telaMenu.transform, "Estrela",
                new Color(1f, 1f, 1f, alfa),
                new Vector2(x, y), new Vector2(tam, tam), UIFabrica.Circulo());
            estrela.gameObject.AddComponent<Cintilar>();
            estrelasFundo.Add(estrela.rectTransform);
            estrelasBase.Add(new Vector2(x, y));
        }

        rtTitulo1 = UIFabrica.CriarTexto(telaMenu.transform, "Titulo1", "DITADO",
            130f, COR_TITULO, new Vector2(0, 620), new Vector2(1000, 150)).rectTransform;
        rtTitulo2 = UIFabrica.CriarTexto(telaMenu.transform, "Titulo2", "ESTRELADO",
            130f, COR_TITULO, new Vector2(0, 480), new Vector2(1000, 150)).rectTransform;

        // Pontos de luz SOBRE o título, pulsando como estrelas no céu
        for (int i = 0; i < 16; i++)
        {
            float x   = (float)sorteio.NextDouble() * 680f - 340f;
            float y   = 425f + (float)sorteio.NextDouble() * 265f;
            float tam = 5f + (float)sorteio.NextDouble() * 9f;
            var brilho = UIFabrica.CriarImagem(telaMenu.transform, "BrilhoTitulo",
                new Color(1f, 1f, 0.85f, 0.9f),
                new Vector2(x, y), new Vector2(tam, tam), UIFabrica.Circulo());
            brilho.raycastTarget = false;
            var cintilar = brilho.gameObject.AddComponent<Cintilar>();
            cintilar.alfaMinimo  = 0f;    // some completamente...
            cintilar.escalaExtra = 0.8f;  // ...e volta inchando, bem "estrela"
            brilhosTitulo.Add(brilho.rectTransform);
            brilhosBase.Add(new Vector2(x, y));
        }

        rtSubtitulo = UIFabrica.CriarTexto(telaMenu.transform, "Subtitulo", "Aprenda o alfabeto em LIBRAS",
            46f, new Color(1f, 1f, 1f, 0.9f), new Vector2(0, 360), new Vector2(1000, 80), false).rectTransform;

        // Recorde do jogador (salvo entre sessões; atualizado ao abrir o menu)
        textoRecorde = UIFabrica.CriarTexto(telaMenu.transform, "Recorde", "",
            36f, COR_TITULO, new Vector2(0, 295), new Vector2(700, 55));
        rtRecorde = textoRecorde.rectTransform;

        // Engrenagem de opções no canto superior esquerdo do menu
        // (alinhada com o botão de som, que fica no canto direito)
        var engrenagem = UIFabrica.CriarBotao(telaMenu.transform, "BotaoOpcoes", "",
            COR_HUD, new Vector2(30, -30), new Vector2(150, 90), 30f, controlador, AbrirOpcoes);
        UIFabrica.Ancorar(engrenagem, new Vector2(0f, 1f), new Vector2(0f, 1f));
        var iconeEngrenagem = UIFabrica.CriarImagem(engrenagem.transform, "Icone",
            Color.white, Vector2.zero, new Vector2(56, 56), UIFabrica.Engrenagem());
        iconeEngrenagem.raycastTarget = false;

        ConstruirPainelOpcoes();

        // CONTINUAR: aparece apenas quando o menu foi aberto no MEIO de um
        // jogo (pausa) - retoma exatamente de onde parou
        var continuar = UIFabrica.CriarBotao(telaMenu.transform, "BotaoContinuar",
            "CONTINUAR", new Color(0.10f, 0.78f, 0.55f, 1f),
            new Vector2(0, 195), new Vector2(560, 130), 52f, controlador, Continuar);
        botaoContinuar = continuar.gameObject;
        botaoContinuar.SetActive(false);
        rtContinuar = continuar.GetComponent<RectTransform>();
        AdicionarIcone(continuar.transform, UIFabrica.Seta(), false, 46f);

        // Acao primaria: maior, no topo da piramide, com a seta de "play"
        var jogar = UIFabrica.CriarBotao(telaMenu.transform, "BotaoJogar", "JOGAR", COR_JOGAR,
            new Vector2(0, 60),   new Vector2(560, 130), 56f, controlador, Jogar);
        rotuloJogar = jogar.transform.Find("Rotulo").GetComponent<TextMeshProUGUI>();
        rtJogar = jogar.GetComponent<RectTransform>();
        AdicionarIcone(jogar.transform, UIFabrica.Seta(), false, 50f); // ▶ play (JOGAR)
        var iconeVoltar = UIFabrica.CriarImagem(jogar.transform, "IconeVoltar", Color.white,
            Vector2.zero, new Vector2(54, 54), Resources.Load<Sprite>("ui/recomecar"));
        iconeVoltar.raycastTarget = false; iconeVoltar.preserveAspect = true;
        var ivr = iconeVoltar.rectTransform;
        ivr.anchorMin = ivr.anchorMax = new Vector2(0f, 0.5f); ivr.pivot = new Vector2(0f, 0.5f);
        ivr.anchoredPosition = new Vector2(28f, 0f);
        iconeVoltar.gameObject.SetActive(false);

        // Acoes secundarias: lado a lado, tamanho intermediario
        var aprender = UIFabrica.CriarBotao(telaMenu.transform, "BotaoAprender", "APRENDA OS SINAIS",
            COR_SINAIS, new Vector2(0, -75), new Vector2(560, 125), 46f, controlador, AbrirEstudo);
        rtAprender = aprender.GetComponent<RectTransform>();
        AdicionarIcone(aprender.transform, Resources.Load<Sprite>("simbolo_libras"), false, 54f);

        var treinar = UIFabrica.CriarBotao(telaMenu.transform, "BotaoTreinar", "TREINAMENTO", COR_TREINAR,
            new Vector2(0, -110), new Vector2(560, 130), 52f, controlador, PedirSenha);
        rtTreinar = treinar.GetComponent<RectTransform>();
        AdicionarIcone(treinar.transform, UIFabrica.Estrela(), false, 46f);

        // Acao negativa: menor e na base, para evitar toque acidental
        var sair = UIFabrica.CriarBotao(telaMenu.transform, "BotaoSair", "SAIR", COR_SAIR,
            new Vector2(0, -280), new Vector2(560, 130), 52f, controlador, Sair);
        rtSair = sair.GetComponent<RectTransform>();
        AdicionarIcone(sair.transform, UIFabrica.Xis(), false, 40f);

        ConstruirPainelSenha();

        rtDicaMenu = UIFabrica.CriarTexto(telaMenu.transform, "Dica",
            "Toque no botão ou aponte o dedo por 3 segundos",
            32f, new Color(1f, 1f, 1f, 0.7f), new Vector2(0, -460), new Vector2(1000, 60), false).rectTransform;

        // ── Rodapé: uma única linha com a identificação do autor ──
        // Fundo escuro discreto só para a frase ficar legível sobre a câmera.
        var cartaoCreditos = UIFabrica.CriarImagem(telaMenu.transform, "Creditos",
            new Color(0f, 0f, 0f, 0.28f), new Vector2(0, -700), new Vector2(1600, 92),
            UIFabrica.Arredondado(), true);
        cartaoCreditos.raycastTarget = false;
        rtCreditos = cartaoCreditos.rectTransform;

        // O texto estica junto com o cartão e encolhe a fonte até caber; em
        // telas estreitas (retrato) ele quebra em duas linhas por conta própria.
        var autoria = UIFabrica.CriarTexto(cartaoCreditos.transform, "Autoria",
            "Desenvolvido por: Marcus Vinicius Souza Batista Strabello, especialista em " +
            "Desenvolvimento de Sistemas Computacionais pelo IFTO, mestrando em Computação " +
            "Aplicada pelo IFMA.",
            26f, new Color(1f, 1f, 1f, 0.92f), Vector2.zero, new Vector2(1540, 80), false);
        autoria.enableAutoSizing = true;
        autoria.fontSizeMin = 15f;
        autoria.fontSizeMax = 26f;
        var autoriaRt = autoria.rectTransform;
        autoriaRt.anchorMin        = Vector2.zero;
        autoriaRt.anchorMax        = Vector2.one;
        autoriaRt.sizeDelta        = new Vector2(-44, -10);
        autoriaRt.anchoredPosition = Vector2.zero;
    }

    // Coloca um ícone branco dentro de um botão, preso a uma das laterais para
    // não se deslocar quando o botão muda de tamanho entre retrato e paisagem.
    void AdicionarIcone(Transform botao, Sprite sprite, bool aDireita, float tam)
    {
        if (sprite == null) return;   // sem sprite, nao cria um quadrado branco
        var icone = UIFabrica.CriarImagem(botao, "Icone", Color.white,
            Vector2.zero, new Vector2(tam, tam), sprite);
        icone.raycastTarget = false;
        var rt = icone.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(aDireita ? 1f : 0f, 0.5f);
        rt.pivot     = new Vector2(aDireita ? 1f : 0f, 0.5f);
        rt.anchoredPosition = new Vector2(aDireita ? -30f : 30f, 0f);
    }

    // Estrela com um "X" no centro (icone de "pular esta letra")
    void AdicionarEstrelaX(Transform botao)
    {
        var estrela = UIFabrica.CriarImagem(botao, "Icone", Color.white,
            Vector2.zero, new Vector2(56, 56), UIFabrica.Estrela());
        estrela.raycastTarget = false;
        var rt = estrela.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot     = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(30f, 0f);

        var x = UIFabrica.CriarImagem(estrela.transform, "X",
            new Color(0.102f, 0.137f, 0.494f, 1f), Vector2.zero, new Vector2(24, 24), UIFabrica.Xis());
        x.raycastTarget = false;
        var xr = x.rectTransform;
        xr.anchorMin = Vector2.zero; xr.anchorMax = Vector2.one;
        xr.sizeDelta = new Vector2(-24, -24); xr.anchoredPosition = Vector2.zero;
    }

    // Pinta o rótulo de um botão (texto), sem mexer no resto
    void PintarRotulo(Button b, Color c)
    {
        var t = b.transform.Find("Rotulo");
        if (t != null)
        {
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.color = c;
        }
    }

    // Troca o visual de um botao pela imagem-asset (pilula pronta), escondendo
    // qualquer rotulo antigo. Mantem o Button e o HoverButton funcionando.
    void UsarImagemDeBotao(Button b, string sprite)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>();
        if (img != null)
        {
            var sp = Resources.Load<Sprite>(sprite);
            if (sp != null) { img.sprite = sp; img.type = Image.Type.Simple; img.preserveAspect = true; }
            img.color = Color.white;
        }
        // esconde textos antigos do botao (o rotulo ja esta desenhado na imagem)
        foreach (var t in b.GetComponentsInChildren<TextMeshProUGUI>(true))
            t.gameObject.SetActive(false);
        // remove icones que eu tinha desenhado por codigo
        foreach (Transform ch in b.transform)
            if (ch.name == "Icone" || ch.name == "X") Destroy(ch.gameObject);
    }

    // Texto pequeno do custo (-5 / -10) na parte de baixo do botao
    void AdicionarCusto(Transform botao, string custo, Color cor)
    {
        var t = UIFabrica.CriarTexto(botao, "Custo", custo, 44f, cor,
            new Vector2(0, -46), new Vector2(220, 56));
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 14f);
    }

    // Quando ha jogo pausado, CONTINUAR e RECOMECAR ficam lado a lado (no lugar
    // do JOGAR), alinhados com os botoes de baixo. Continuar leva a seta de
    // play; Recomecar leva a mesma seta invertida (voltar).
    void PosicionarBotoesContinuar(bool pausado)
    {
        bool h = telaHorizontal;
        if (pausado)
        {
            // Paisagem: lado a lado. Retrato: empilhado (coluna), largura cheia.
            if (rtContinuar != null) { rtContinuar.anchoredPosition = h ? new Vector2(-330, 110) : new Vector2(0, 200);
                                       rtContinuar.sizeDelta = h ? new Vector2(610, 130) : new Vector2(820, 120); }
            if (rtJogar != null)     { rtJogar.anchoredPosition = h ? new Vector2(330, 110) : new Vector2(0, 55);
                                       rtJogar.sizeDelta = h ? new Vector2(610, 130) : new Vector2(820, 130); }
            FlipIconeJogar(true);
        }
        else
        {
            if (rtJogar != null) { rtJogar.anchoredPosition = h ? new Vector2(0, 110) : new Vector2(0, 55);
                                   rtJogar.sizeDelta = h ? new Vector2(540, 140) : new Vector2(820, 130); }
            FlipIconeJogar(false);
        }
    }

    // Inverte o icone do JOGAR: play (->) quando "JOGAR/RECOMECAR" avanca,
    // seta invertida (<-) para dar sentido de "voltar" no RECOMECAR pausado.
    void FlipIconeJogar(bool voltar)
    {
        if (rtJogar == null) return;
        var play = rtJogar.Find("Icone");
        var back = rtJogar.Find("IconeVoltar");
        if (play != null) play.gameObject.SetActive(!voltar);
        if (back != null) back.gameObject.SetActive(voltar);
    }

    void CriarLogo()
    {
        if (!mostrarLogo) return;
        var logo = UIFabrica.CriarImagem(transform, "LogoUniverso", Color.white,
            new Vector2(0, -16), new Vector2(150, 129), Resources.Load<Sprite>("ui/logo_universo"));
        logo.preserveAspect = true; logo.raycastTarget = false;
        UIFabrica.Ancorar(logo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        logoUniverso = logo.gameObject;
    }

    void TrazerLogoAFrente() { if (logoUniverso != null) logoUniverso.transform.SetAsLastSibling(); }

    // Ajuste especifico do modo estudo (logo menor e mais no topo, para nao
    // cobrir o titulo APRENDA OS SINAIS) sem afetar os outros modos.
    void AjustarLogo(bool estudo)
    {
        if (logoUniverso == null) return;
        var rt = (RectTransform)logoUniverso.transform;
        rt.sizeDelta        = estudo ? new Vector2(86, 74)  : new Vector2(100, 86);
        rt.anchoredPosition = estudo ? new Vector2(0, -4)   : new Vector2(0, -8);
    }

    void ConstruirHud()
    {
        // Barra única do topo: uma faixa cheia #1A237E atrás de pontos, vidas,
        // tempo, menu e som, unificando tudo num só cabeçalho. Fica ATRÁS de
        // todos (SetAsFirstSibling), então os chips e botões continuam por cima
        // e funcionando; a cor deles virou a mesma da barra, formando uma faixa
        // contínua. A barra só aparece no jogo, junto com o botão MENU.
        var header = UIFabrica.CriarImagem(transform, "HeaderBar",
            new Color(0f, 0f, 0f, 0f), Vector2.zero, new Vector2(0, 200),
            UIFabrica.Arredondado(), true);
        var hr = header.rectTransform;
        hr.anchorMin = new Vector2(0f, 1f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.pivot     = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(0, 200);
        hr.anchoredPosition = Vector2.zero;
        header.raycastTarget = false;
        // A barra vai para o MESMO pai da camera (o Canvas), logo ACIMA dela e
        // ABAIXO dos chips (pontos/vidas/tempo, criados depois). Sem isto a
        // barra ficava atras da imagem da camera e nao aparecia.
        if (controlador != null && controlador.fundoDoEcra != null)
        {
            var camara = controlador.fundoDoEcra.transform;
            header.transform.SetParent(camara.parent, false);
            camara.SetAsFirstSibling();               // camera sempre no fundo
            header.transform.SetSiblingIndex(1);      // barra logo acima da camera
        }
        else
        {
            header.transform.SetAsFirstSibling();
        }
        headerBar = header.gameObject;
        headerBar.SetActive(false);

        // Botão MENU no canto superior direito (visível durante jogo/treinamento)
        var botao = UIFabrica.CriarBotao(transform, "BotaoMenuHud", "MENU", COR_HUD,
            new Vector2(-30, -30), new Vector2(220, 90), 40f, controlador, AbrirMenuComSom);
        UIFabrica.Ancorar(botao, new Vector2(1f, 1f), new Vector2(1f, 1f));
        botaoMenuHud = botao.gameObject;

        // Botão de SOM logo abaixo do MENU - visível SEMPRE (menu e jogo)
        var som = UIFabrica.CriarBotao(transform, "BotaoSom", "", COR_HUD,
            new Vector2(-30, -135), new Vector2(150, 90), 38f, controlador, AlternarSom);
        UIFabrica.Ancorar(som, new Vector2(1f, 1f), new Vector2(1f, 1f));
        botaoSom = som.gameObject;

        // Ícone de alto-falante desenhado por código (na estética do jogo)
        var iconeSom = UIFabrica.CriarImagem(som.transform, "Icone",
            Color.white, Vector2.zero, new Vector2(58, 58), UIFabrica.AltoFalante());
        iconeSom.raycastTarget = false;

        // Risco vermelho na diagonal = música desligada
        var risco = UIFabrica.CriarImagem(som.transform, "Risco",
            new Color(0.9f, 0.2f, 0.2f, 0.95f), Vector2.zero, new Vector2(120, 10),
            UIFabrica.Arredondado(), true);
        risco.rectTransform.localEulerAngles = new Vector3(0, 0, 20f);
        risco.raycastTarget = false;
        riscoSom = risco.gameObject;
        riscoSom.SetActive(false);

        // PULAR LETRA e PULAR PALAVRA usam os assets prontos (a pilula com o
        // rotulo e o icone ja desenhados). O botao vira apenas a imagem; por
        // cima, um texto pequeno mostra o custo (-5 / -10) na parte de baixo.
        var pularLetra = UIFabrica.CriarBotao(transform, "BotaoPularLetra",
            "", Color.white, new Vector2(-250, 175), new Vector2(500, 175), 1f,
            controlador, () => gerenciador.PularLetra());
        UIFabrica.Ancorar(pularLetra, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
        botaoPularLetra = pularLetra.gameObject;
        UsarImagemDeBotao(pularLetra, "ui/pular_letra");
        AdicionarCusto(pularLetra.transform, "-5", new Color(0.42f, 0.30f, 0f, 1f));

        if (botaoPular != null)
        {
            UsarImagemDeBotao(botaoPular.GetComponent<Button>(), "ui/pular_palavra");
            AdicionarCusto(botaoPular.transform, "-10", new Color(0.03f, 0.28f, 0.06f, 1f));
        }

        // Cartão de instruções do modo treinamento
        var painel = UIFabrica.CriarImagem(transform, "DicaTreinamento",
            new Color(1f, 1f, 1f, 0.88f), new Vector2(0, -160), new Vector2(980, 470),
            UIFabrica.Arredondado(), true);
        UIFabrica.Ancorar(painel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        UIFabrica.CriarTexto(painel.transform, "Texto",
            "MODO TREINAMENTO\n\n" +
            "Faça o sinal e pressione a tecla da letra\n" +
            "para gravar (grave várias vezes!)\n" +
            "H J K W X Z Ç gravam o MOVIMENTO por 1,3s\n" +
            "(o Ç grava pela tecla Ç do teclado)\n" +
            "Shift + tecla apaga a letra",
            36f, new Color(0.15f, 0.15f, 0.22f, 1f), new Vector2(0, 80), new Vector2(940, 290), false);

        // Contador ao vivo: quantas amostras cada letra tem no banco
        // (várias linhas, com quebra automática feita pelo ResumoDoBanco)
        textoContagem = UIFabrica.CriarTexto(painel.transform, "Contagem", "",
            34f, new Color(0.1f, 0.35f, 0.7f, 1f), new Vector2(0, -155), new Vector2(940, 150));
        textoContagem.enableAutoSizing = true;
        textoContagem.fontSizeMin = 24f;
        textoContagem.fontSizeMax = 36f;

        dicaTreinamento = painel.gameObject;
    }

    // ── Painel de senha (área do professor) ──────────────────────────────────
    void ConstruirPainelSenha()
    {
        var fundo = UIFabrica.CriarImagem(telaMenu.transform, "PainelSenha",
            new Color(0.07f, 0.09f, 0.25f, 0.97f), Vector2.zero, new Vector2(680, 1000),
            UIFabrica.Arredondado(), true);
        painelSenha = fundo.gameObject;

        UIFabrica.CriarTexto(fundo.transform, "Titulo", "ÁREA DO PROFESSOR",
            48f, COR_TITULO, new Vector2(0, 410), new Vector2(640, 80));
        UIFabrica.CriarTexto(fundo.transform, "Sub", "Digite a senha para o treinamento",
            30f, new Color(1f, 1f, 1f, 0.8f), new Vector2(0, 345), new Vector2(640, 50), false);

        // Visor da senha (mostra bolinhas)
        var visor = UIFabrica.CriarImagem(fundo.transform, "Visor",
            new Color(1f, 1f, 1f, 0.92f), new Vector2(0, 260), new Vector2(420, 90),
            UIFabrica.Arredondado(), true);
        displaySenha = UIFabrica.CriarTexto(visor.transform, "Texto", "",
            52f, new Color(0.1f, 0.1f, 0.2f, 1f), Vector2.zero, new Vector2(400, 90));

        // Teclado numérico 3x4: 1-9, C (limpar), 0, OK
        string[] teclas = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" };
        for (int i = 0; i < teclas.Length; i++)
        {
            string tecla = teclas[i]; // cópia local: cada botão guarda a sua!
            int coluna = i % 3, linha = i / 3;
            Vector2 pos = new Vector2((coluna - 1) * 200f, 130f - linha * 155f);
            Color cor = (tecla == "OK") ? COR_JOGAR : (tecla == "C") ? COR_SAIR : COR_TREINAR;
            UIFabrica.CriarBotao(fundo.transform, "Tecla" + tecla, tecla, cor,
                pos, new Vector2(180, 135), 48f, controlador, () => TeclaSenha(tecla));
        }

        UIFabrica.CriarBotao(fundo.transform, "Voltar", "VOLTAR",
            new Color(0.4f, 0.4f, 0.5f, 1f), new Vector2(0, -420), new Vector2(300, 90),
            36f, controlador, FecharPainelSenha);

        painelSenha.SetActive(false);
    }

    void PedirSenha()
    {
        GerenciadorDeAudio.TocarClique();
        senhaDigitada = "";
        AtualizarVisorSenha();
        painelSenha.SetActive(true);
        painelSenha.transform.SetAsLastSibling(); // por cima dos botões do menu
    }

    void FecharPainelSenha()
    {
        GerenciadorDeAudio.TocarClique();
        painelSenha.SetActive(false);
    }

    void TeclaSenha(string tecla)
    {
        GerenciadorDeAudio.TocarClique();
        if (tecla == "C")
        {
            senhaDigitada = "";
        }
        else if (tecla == "OK")
        {
            if (senhaDigitada == senhaAdmin)
            {
                painelSenha.SetActive(false);
                Treinar();
                return;
            }
            displaySenha.text  = "ERRADA!";
            displaySenha.color = new Color(0.8f, 0.15f, 0.15f, 1f);
            senhaDigitada = "";
            return;
        }
        else if (senhaDigitada.Length < 8)
        {
            senhaDigitada += tecla;
        }
        AtualizarVisorSenha();
    }

    void AtualizarVisorSenha()
    {
        displaySenha.color = new Color(0.1f, 0.1f, 0.2f, 1f);
        displaySenha.text  = new string('*', senhaDigitada.Length);
    }

    public void AlternarSom()
    {
        GerenciadorDeAudio.TocarClique();
        GerenciadorDeAudio.AlternarMusica();
        riscoSom.SetActive(!GerenciadorDeAudio.MusicaLigada);
    }

    // ── Painel de opções (engrenagem) ────────────────────────────────────────

    void ConstruirPainelOpcoes()
    {
        var fundo = UIFabrica.CriarImagem(telaMenu.transform, "PainelOpcoes",
            new Color(0.07f, 0.09f, 0.25f, 0.97f), Vector2.zero, new Vector2(700, 640),
            UIFabrica.Arredondado(), true);
        painelOpcoes = fundo.gameObject;

        UIFabrica.CriarTexto(fundo.transform, "Titulo", "OPÇÕES",
            52f, COR_TITULO, new Vector2(0, 250), new Vector2(640, 80));

        rotuloTela    = CriarLinhaDeOpcao(fundo.transform, new Vector2(0,  140), AlternarTela);
        rotuloPressao = CriarLinhaDeOpcao(fundo.transform, new Vector2(0,   10), AlternarPressao);
        rotuloEspelho = CriarLinhaDeOpcao(fundo.transform, new Vector2(0, -120), AlternarEspelho);

        UIFabrica.CriarBotao(fundo.transform, "Fechar", "FECHAR",
            new Color(0.4f, 0.4f, 0.5f, 1f), new Vector2(0, -250), new Vector2(300, 90),
            36f, controlador, FecharOpcoes);

        painelOpcoes.SetActive(false);
    }

    TextMeshProUGUI CriarLinhaDeOpcao(Transform pai, Vector2 pos,
                                      UnityEngine.Events.UnityAction acao)
    {
        var botao = UIFabrica.CriarBotao(pai, "Opcao", "", COR_TREINAR, pos,
            new Vector2(600, 110), 32f, controlador, acao);
        return botao.transform.Find("Rotulo").GetComponent<TextMeshProUGUI>();
    }

    void AbrirOpcoes()
    {
        GerenciadorDeAudio.TocarClique();
        // Clicar na engrenagem alterna: se o painel ja esta aberto, fecha.
        if (painelOpcoes.activeSelf) { painelOpcoes.SetActive(false); return; }
        AtualizarRotulosOpcoes();
        painelOpcoes.SetActive(true);
        painelOpcoes.transform.SetAsLastSibling();
    }

    void FecharOpcoes()
    {
        GerenciadorDeAudio.TocarClique();
        painelOpcoes.SetActive(false);
    }

    void AtualizarRotulosOpcoes()
    {
        if (rotuloTela != null)
            rotuloTela.text = telaHorizontal ? "TELA:  PC / TABLET" : "TELA:  CELULAR";
        if (rotuloPressao != null && gerenciador != null)
            rotuloPressao.text = gerenciador.modoSemPressao
                                 ? "TEMPO E VIDAS:  NÃO" : "TEMPO E VIDAS:  SIM";
        if (rotuloEspelho != null && controlador != null)
            rotuloEspelho.text = controlador.espelharImagem
                                 ? "ESPELHAR CÂMERA:  SIM" : "ESPELHAR CÂMERA:  NÃO";
    }

    void AlternarTela()
    {
        GerenciadorDeAudio.TocarClique();
        telaHorizontal = !telaHorizontal;
        escolhaDeTelaFeita = true;
        PlayerPrefs.SetInt("telaHorizontal", telaHorizontal ? 1 : 0);
        PlayerPrefs.SetInt("telaEscolhida", 1);
        AplicarOrientacao();
        AtualizarRotulosOpcoes();
    }

    // Liga/desliga o tempo e as vidas (modo tranquilo para iniciantes)
    void AlternarPressao()
    {
        GerenciadorDeAudio.TocarClique();
        gerenciador.modoSemPressao = !gerenciador.modoSemPressao;
        PlayerPrefs.SetInt("semPressao", gerenciador.modoSemPressao ? 1 : 0);
        AtualizarRotulosOpcoes();
    }

    // Espelha (ou não) a imagem da câmera e a leitura da mão junto
    void AlternarEspelho()
    {
        GerenciadorDeAudio.TocarClique();
        AplicarEspelho(!controlador.espelharImagem);
        PlayerPrefs.SetInt("espelharImagem", controlador.espelharImagem ? 1 : 0);
        AtualizarRotulosOpcoes();
    }

    void AplicarEspelho(bool espelhar)
    {
        controlador.espelharImagem = espelhar;
        if (controlador.fundoDoEcra != null)
        {
            var escala = controlador.fundoDoEcra.rectTransform.localScale;
            escala.x = espelhar ? -Mathf.Abs(escala.x) : Mathf.Abs(escala.x);
            controlador.fundoDoEcra.rectTransform.localScale = escala;
        }
    }

    // Reposiciona TUDO conforme a orientação.
    // Retrato: menu em coluna única (como no celular), em pirâmide de tamanhos.
    // Paisagem: título numa linha no topo, JOGAR em destaque, as duas
    // secundárias lado a lado, SAIR na base, e a autoria no rodapé.
    // Nos dois modos o objeto 3D fica DENTRO do cartão da palavra, no espaço
    // à esquerda, ao lado das letras (não cobre o rosto nem as mãos).
    void AplicarOrientacao()
    {
        bool h = telaHorizontal;

        if (escalador == null) escalador = GetComponent<CanvasScaler>();
        if (escalador != null)
            escalador.referenceResolution = h ? new Vector2(1920, 1080)
                                              : new Vector2(1080, 1920);
        if (telaEstudo != null) telaEstudo.AplicarLayout(h);

        void Pos(RectTransform rt, Vector2 retrato, Vector2 paisagem)
        {
            if (rt != null) rt.anchoredPosition = h ? paisagem : retrato;
        }
        void PosTam(RectTransform rt, Vector2 posR, Vector2 tamR,
                                      Vector2 posP, Vector2 tamP)
        {
            if (rt == null) return;
            rt.anchoredPosition = h ? posP : posR;
            rt.sizeDelta        = h ? tamP : tamR;
        }

        // ── Menu ──
        // Na paisagem o título vira UMA linha ("DITADO ESTRELADO") no topo
        if (tmpTitulo1 == null && rtTitulo1 != null)
            tmpTitulo1 = rtTitulo1.GetComponent<TextMeshProUGUI>();
        if (tmpTitulo1 != null) tmpTitulo1.text = h ? "DITADO ESTRELADO" : "DITADO";
        if (rtTitulo1  != null) rtTitulo1.sizeDelta = h ? new Vector2(1800, 150)
                                                        : new Vector2(1000, 150);
        if (rtTitulo2  != null) rtTitulo2.gameObject.SetActive(!h);

        Pos(rtTitulo1,   new Vector2(0, 585), new Vector2(0, 385));
        Pos(rtSubtitulo, new Vector2(0, 345), new Vector2(0, 250));
        Pos(rtRecorde,   new Vector2(0, 295), new Vector2(0, 268));

        // Hierarquia piramidal: JOGAR (primaria) maior no topo; APRENDA e
        // TREINAMENTO (secundarias) no meio, tamanho intermediario - em coluna
        // no retrato, lado a lado na paisagem; SAIR (negativa) menor na base,
        // para reduzir toque acidental. Primeiro par = retrato, segundo = paisagem.
        PosTam(rtContinuar, new Vector2(0,  200), new Vector2(820, 120),
                            new Vector2(0,  235), new Vector2(500,  90));
        PosTam(rtJogar,     new Vector2(0,   55), new Vector2(820, 130),
                            new Vector2(0,  110), new Vector2(540, 140));
        PosTam(rtAprender,  new Vector2(0,  -90), new Vector2(820, 120),
                            new Vector2(-330, -45), new Vector2(610, 120));
        PosTam(rtTreinar,   new Vector2(0, -235), new Vector2(820, 120),
                            new Vector2( 330, -45), new Vector2(610, 120));
        PosTam(rtSair,      new Vector2(0, -380), new Vector2(820, 120),
                            new Vector2(0, -190), new Vector2(380,  90));
        // Bloco do rodape: a linha de autoria colada na base e a dica logo
        // acima dela. Presos a base para continuarem visiveis em qualquer
        // proporcao de janela, inclusive quando ela nao corresponde a
        // orientacao escolhida. A posicao da dica sai da ALTURA REAL do cartao
        // (Ancorar poe o pivo na base, entao a dica fica no topo do cartao mais
        // uma folga), e nao de um numero fixo que quebraria se o cartao mudasse.
        const float MARGEM_DA_BASE = 24f;  // folga entre o cartao e a borda
        const float FOLGA_DA_DICA  = 18f;  // folga entre o cartao e a dica

        float topoDosCreditos = MARGEM_DA_BASE;
        if (rtCreditos != null)
        {
            // Na paisagem a frase cabe numa linha larga e baixa; no retrato ela
            // quebra em duas, entao o cartao fica mais estreito e mais alto.
            rtCreditos.sizeDelta = h ? new Vector2(1700, 92) : new Vector2(1000, 150);
            UIFabrica.Ancorar(rtCreditos, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            rtCreditos.anchoredPosition = new Vector2(0, MARGEM_DA_BASE);
            topoDosCreditos = MARGEM_DA_BASE + rtCreditos.sizeDelta.y;
        }
        if (rtDicaMenu != null)
        {
            UIFabrica.Ancorar(rtDicaMenu, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            rtDicaMenu.anchoredPosition = new Vector2(0, topoDosCreditos + FOLGA_DA_DICA);
        }

        // Luzes acompanham o título; estrelas se espalham na largura nova
        for (int i = 0; i < brilhosTitulo.Count; i++)
        {
            Vector2 b = brilhosBase[i];
            brilhosTitulo[i].anchoredPosition = h
                ? new Vector2(b.x * 2.4f, 390f + (b.y - 425f) * 0.30f)
                : b;
        }
        for (int i = 0; i < estrelasFundo.Count; i++)
            estrelasFundo[i].anchoredPosition = h
                ? new Vector2(estrelasBase[i].x * 1.8f, estrelasBase[i].y * 0.55f)
                : estrelasBase[i];

        // ── Jogo ──
        // Cartão da palavra com "slot" à esquerda para o objeto 3D.
        //
        // Estes elementos sao ancorados a BASE da tela com o pivo no centro,
        // entao a altura informada e a distancia da borda de baixo ate o MEIO
        // do elemento. Valores negativos jogam o cartao para fora da tela, que
        // era o que acontecia na paisagem: so a beirada de cima aparecia.
        //
        // Na horizontal o cartao encolheu de 1200 para 1000. Com 1200, somado
        // aos dois botoes de 390, dava 1980 numa tela de 1920: nao cabiam lado
        // a lado e se invadiam.
        // Foco central: o painel da palavra sobe e fica em destaque; logo abaixo,
        // os dois botoes de acao formam um par centralizado, maiores e proximos
        // um do outro - longe dos cantos, para o dedo alcancar sem toque acidental.
        if (painelPalavra != null)
        {
            PosTam((RectTransform)painelPalavra.transform,
                new Vector2(0, 450), new Vector2(880, 240),
                new Vector2(0, 405), new Vector2(980, 180));

            if (uiControle == null)
                uiControle = painelPalavra.GetComponentInChildren<UIControle>(true);
            if (uiControle != null)
                uiControle.DefinirEspacoDoObjeto(h ? 300f : 260f, h ? 145f : 120f);
        }
        if (botaoPular != null)               // PULAR PALAVRA, a direita do centro
            PosTam((RectTransform)botaoPular.transform,
                new Vector2( 270, 175), new Vector2(500, 178),
                new Vector2( 300, 175), new Vector2(540, 192));
        if (botaoPularLetra != null)          // PULAR LETRA, a esquerda do centro
            PosTam((RectTransform)botaoPularLetra.transform,
                new Vector2(-270, 175), new Vector2(500, 178),
                new Vector2(-300, 175), new Vector2(540, 192));

        larguraDoSlot = h ? 300f : 260f;
        PosicionarObjeto3D();
        PosicionarBotoesContinuar(jogoPausado); // mantem CONTINUAR/RECOMECAR no lugar
    }

    // O objeto 3D vive no mundo e o cartão vive na interface. Se a posição do
    // objeto for fixa, os dois só coincidem numa proporção de tela específica
    // e o objeto "escapa" da caixa em qualquer outra. Aqui a posição é
    // calculada a partir do cartão de verdade, então eles andam sempre juntos.
    void PosicionarObjeto3D()
    {
        if (gerenciador == null || gerenciador.pontoDeExibicao == null) return;
        if (painelPalavra == null) return;

        var camera = Camera.main;
        if (camera == null) return;

        var rt = (RectTransform)painelPalavra.transform;
        var cantos = new Vector3[4];
        rt.GetWorldCorners(cantos); // 0=baixo-esq 1=cima-esq 2=cima-dir 3=baixo-dir

        // Centro do espaço reservado na esquerda do cartão
        float fatia = Mathf.Clamp01(larguraDoSlot / Mathf.Max(1f, rt.rect.width));
        Vector3 meioEsquerdo = Vector3.Lerp(cantos[0], cantos[1], 0.5f);
        Vector3 meioDireito  = Vector3.Lerp(cantos[3], cantos[2], 0.5f);
        Vector3 alvo = Vector3.Lerp(meioEsquerdo, meioDireito, fatia * 0.5f);

        // Leva esse ponto para a distância em que o objeto é exibido
        Vector3 naTela = camera.WorldToScreenPoint(alvo);
        gerenciador.pontoDeExibicao.position =
            camera.ScreenToWorldPoint(new Vector3(naTela.x, naTela.y, DISTANCIA_DO_OBJETO));

        // Tamanho proporcional à altura do cartão, para caber no espaço
        Vector3 topo  = camera.ScreenToWorldPoint(new Vector3(naTela.x,
                        camera.WorldToScreenPoint(cantos[1]).y, DISTANCIA_DO_OBJETO));
        Vector3 baixo = camera.ScreenToWorldPoint(new Vector3(naTela.x,
                        camera.WorldToScreenPoint(cantos[0]).y, DISTANCIA_DO_OBJETO));
        float alturaNoMundo = Vector3.Distance(topo, baixo);
        if (alturaNoMundo > 0.01f)
            gerenciador.tamanhoDoObjeto = alturaNoMundo * 0.78f;
    }

    // ── Ações dos botões ─────────────────────────────────────────────────────

    // Abre a tela de consulta do alfabeto (criada na primeira vez que e usada)
    void AbrirEstudo()
    {
        GerenciadorDeAudio.TocarClique();
        if (telaEstudo == null)
            telaEstudo = ModoEstudo.Criar(transform, controlador, FecharEstudo);
        telaMenu.SetActive(false);
        telaEstudo.Abrir(telaHorizontal);
        TrazerLogoAFrente(); AjustarLogo(true);
    }

    void FecharEstudo()
    {
        telaMenu.SetActive(true);
        telaMenu.transform.SetAsLastSibling();
        if (botaoSom != null) botaoSom.transform.SetAsLastSibling();
    }

    // Retoma o jogo pausado exatamente de onde parou
    void Continuar()
    {
        GerenciadorDeAudio.TocarClique();
        FecharMenu();
        controlador.MODO_TREINAMENTO = false;
        dicaTreinamento.SetActive(false);
        if (painelPalavra   != null) painelPalavra.SetActive(true);
        if (botaoPular      != null) botaoPular.SetActive(true);
        if (botaoPularLetra != null) botaoPularLetra.SetActive(true);
        gerenciador.Retomar();
    }

    void Jogar()
    {
        GerenciadorDeAudio.TocarClique();
        FecharMenu();
        controlador.MODO_TREINAMENTO = false;
        dicaTreinamento.SetActive(false);
        if (painelPalavra    != null) painelPalavra.SetActive(true);
        if (botaoPular       != null) botaoPular.SetActive(true);
        if (botaoPularLetra  != null) botaoPularLetra.SetActive(true);
        gerenciador.IniciarJogo();
    }

    void Treinar()
    {
        GerenciadorDeAudio.TocarClique();
        FecharMenu();
        controlador.MODO_TREINAMENTO = true;
        gerenciador.PararJogo();
        if (painelPalavra   != null) painelPalavra.SetActive(false);
        if (botaoPular      != null) botaoPular.SetActive(false);
        if (botaoPularLetra != null) botaoPularLetra.SetActive(false);
        dicaTreinamento.SetActive(true);
    }

    void Sair()
    {
        GerenciadorDeAudio.TocarClique();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void AbrirMenuComSom()
    {
        GerenciadorDeAudio.TocarClique();
        AbrirMenu();
    }

    public void AbrirMenu()
    {
        menuAberto     = true;
        timerFimDeJogo = 0f;

        // Jogo em andamento? PAUSA (não perde pontos/fase/vidas).
        // Fora isso (fim de jogo, treinamento, início), para de verdade.
        bool podeContinuar = gerenciador != null &&
                             gerenciador.JogoIniciado && !gerenciador.JogoTerminado;
        jogoPausado = podeContinuar;
        if (podeContinuar) gerenciador.Pausar();
        else               gerenciador.PararJogo();
        if (botaoContinuar != null) botaoContinuar.SetActive(podeContinuar);
        if (rotuloJogar    != null) rotuloJogar.text = podeContinuar ? "RECOMEÇAR" : "JOGAR";
        // Com o jogo pausado o CONTINUAR ocupa o topo da pilha; o recorde (uma
        // estatistica de menu ocioso) sai de cena para nao disputar espaco.
        if (rtRecorde != null) rtRecorde.gameObject.SetActive(!podeContinuar);
        PosicionarBotoesContinuar(podeContinuar);

        controlador.MODO_TREINAMENTO = false;

        telaMenu.SetActive(true);
        telaMenu.transform.SetAsLastSibling(); // menu por cima de tudo
        if (painelSenha  != null) painelSenha.SetActive(false);
        if (painelOpcoes != null) painelOpcoes.SetActive(false);
        if (telaEstudo   != null) telaEstudo.gameObject.SetActive(false);

        // No menu, o som sobe para ficar alinhado com a engrenagem
        if (botaoSom != null)
            ((RectTransform)botaoSom.transform).anchoredPosition = new Vector2(-30, -30);

        // Atualiza o recorde exibido (pode ter acabado de bater um novo!)
        if (textoRecorde != null)
        {
            int recorde = PlayerPrefs.GetInt("recorde", 0);
            textoRecorde.text = recorde > 0 ? "RECORDE: " + recorde : "";
        }
        if (botaoSom    != null) botaoSom.transform.SetAsLastSibling(); // som clicável até no menu
        botaoMenuHud.SetActive(false);
        if (headerBar != null) headerBar.SetActive(false);
        if (botaoSom != null) botaoSom.SetActive(true); // som do menu
        dicaTreinamento.SetActive(false);
        if (painelPalavra   != null) painelPalavra.SetActive(false);
        if (botaoPular      != null) botaoPular.SetActive(false);
        if (botaoPularLetra != null) botaoPularLetra.SetActive(false);
        TrazerLogoAFrente(); AjustarLogo(false);
    }

    void FecharMenu()
    {
        menuAberto = false;
        telaMenu.SetActive(false);
        botaoMenuHud.SetActive(false);   // MENU agora faz parte da faixa HUD
        if (botaoSom != null) botaoSom.SetActive(false); // SOM tambem esta na faixa
        if (headerBar != null) headerBar.SetActive(true);
        TrazerLogoAFrente(); AjustarLogo(false);

        // No jogo, o som volta para baixo do botão MENU
        if (botaoSom != null)
            ((RectTransform)botaoSom.transform).anchoredPosition = new Vector2(-30, -135);
    }
}
