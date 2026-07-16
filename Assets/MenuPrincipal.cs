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
    static readonly Color COR_TITULO     = new Color(1f,    0.85f, 0.25f, 1f); // amarelo-estrela
    static readonly Color COR_JOGAR      = new Color(0.18f, 0.72f, 0.35f, 1f); // verde
    static readonly Color COR_TREINAR    = new Color(0.15f, 0.50f, 0.90f, 1f); // azul
    static readonly Color COR_SAIR       = new Color(0.85f, 0.30f, 0.30f, 1f); // vermelho
    static readonly Color COR_HUD        = new Color(0.08f, 0.10f, 0.30f, 0.75f);

    [Header("Senha da area do professor (modo treinamento)")]
    public string senhaAdmin = "1234";

    GameObject telaMenu;
    GameObject botaoMenuHud;
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
    float timerFimDeJogo;
    float timerContagem;

    // Orientacao da tela: false = celular (retrato), true = PC/tablet (paisagem)
    bool telaHorizontal;
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
    RectTransform rtContinuar, rtJogar, rtTreinar, rtSair, rtDicaMenu, rtCreditos;
    readonly List<RectTransform> estrelasFundo = new List<RectTransform>();
    readonly List<Vector2>       estrelasBase  = new List<Vector2>();
    readonly List<RectTransform> brilhosTitulo = new List<RectTransform>();
    readonly List<Vector2>       brilhosBase   = new List<Vector2>();

    void Awake()
    {
        ConstruirMenu();
        ConstruirHud();
    }

    void Start()
    {
        // Recupera as escolhas da última vez (tela, tempo/vidas, espelho)
        telaHorizontal = PlayerPrefs.GetInt("telaHorizontal", 0) == 1;
        if (gerenciador != null)
            gerenciador.modoSemPressao = PlayerPrefs.GetInt("semPressao", 0) == 1;
        AplicarEspelho(PlayerPrefs.GetInt("espelharImagem", 1) == 1);
        AplicarOrientacao();

        AbrirMenu();
        GerenciadorDeAudio.TocarMusica();
        // O jogador pode ter desligado a música na última vez - reflete no ícone
        riscoSom.SetActive(!GerenciadorDeAudio.MusicaLigada);
    }

    void Update()
    {
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

        var jogar = UIFabrica.CriarBotao(telaMenu.transform, "BotaoJogar", "JOGAR", COR_JOGAR,
            new Vector2(0, 60),   new Vector2(560, 130), 56f, controlador, Jogar);
        rotuloJogar = jogar.transform.Find("Rotulo").GetComponent<TextMeshProUGUI>();
        rtJogar = jogar.GetComponent<RectTransform>();
        rtTreinar = UIFabrica.CriarBotao(telaMenu.transform, "BotaoTreinar", "TREINAMENTO", COR_TREINAR,
            new Vector2(0, -110), new Vector2(560, 130), 52f, controlador, PedirSenha)
            .GetComponent<RectTransform>();
        rtSair = UIFabrica.CriarBotao(telaMenu.transform, "BotaoSair", "SAIR", COR_SAIR,
            new Vector2(0, -280), new Vector2(560, 130), 52f, controlador, Sair)
            .GetComponent<RectTransform>();

        ConstruirPainelSenha();

        rtDicaMenu = UIFabrica.CriarTexto(telaMenu.transform, "Dica",
            "Toque no botão ou aponte o dedo por 3 segundos",
            32f, new Color(1f, 1f, 1f, 0.7f), new Vector2(0, -460), new Vector2(1000, 60), false).rectTransform;

        // ── Créditos: cartão elegante com hierarquia visual ──
        var cartaoCreditos = UIFabrica.CriarImagem(telaMenu.transform, "Creditos",
            new Color(1f, 1f, 1f, 0.07f), new Vector2(0, -700), new Vector2(880, 260),
            UIFabrica.Arredondado(), true);
        cartaoCreditos.raycastTarget = false;
        rtCreditos = cartaoCreditos.rectTransform;
        Transform cred = cartaoCreditos.transform;

        // Linha separadora dourada no topo
        UIFabrica.CriarImagem(cred, "Linha", new Color(1f, 0.85f, 0.25f, 0.5f),
            new Vector2(0, 108), new Vector2(320, 4), UIFabrica.Arredondado(), true);

        UIFabrica.CriarTexto(cred, "L1", "produzido por",
            24f, new Color(1f, 1f, 1f, 0.55f), new Vector2(0, 74), new Vector2(840, 34), false);
        UIFabrica.CriarTexto(cred, "L2", "MARCUS STRABELLO",
            40f, Color.white, new Vector2(0, 34), new Vector2(840, 52));
        UIFabrica.CriarTexto(cred, "L3", "Orientação: Prof. Dr. Alex Martins Santos",
            27f, new Color(1f, 1f, 1f, 0.8f), new Vector2(0, -18), new Vector2(840, 40), false);
        UIFabrica.CriarTexto(cred, "L4", "Visão Computacional  •  PPGCA  •  IFMA",
            26f, new Color(1f, 0.85f, 0.25f, 0.85f), new Vector2(0, -58), new Vector2(840, 38), false);
        UIFabrica.CriarTexto(cred, "L5", "Campus São Luís - Monte Castelo",
            23f, new Color(1f, 1f, 1f, 0.55f), new Vector2(0, -94), new Vector2(840, 34), false);
    }

    void ConstruirHud()
    {
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

        // Botão PULAR LETRA (o PULAR PALAVRA já existe na cena, à direita)
        var pularLetra = UIFabrica.CriarBotao(transform, "BotaoPularLetra",
            "PULAR LETRA  -5", new Color(0.55f, 0.4f, 0.85f, 1f),
            new Vector2(-250, 240), new Vector2(460, 110), 34f, controlador,
            () => gerenciador.PularLetra());
        UIFabrica.Ancorar(pularLetra, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
        botaoPularLetra = pularLetra.gameObject;

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

    void AlternarSom()
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
        PlayerPrefs.SetInt("telaHorizontal", telaHorizontal ? 1 : 0);
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
    // Retrato: menu em coluna única (como no celular).
    // Paisagem: título numa linha só no topo, botões lado a lado no centro
    // e créditos no rodapé.
    // Nos dois modos o objeto 3D fica DENTRO do cartão da palavra, no espaço
    // à esquerda, ao lado das letras (não cobre o rosto nem as mãos).
    void AplicarOrientacao()
    {
        bool h = telaHorizontal;

        if (escalador == null) escalador = GetComponent<CanvasScaler>();
        if (escalador != null)
            escalador.referenceResolution = h ? new Vector2(1920, 1080)
                                              : new Vector2(1080, 1920);

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

        Pos(rtTitulo1,   new Vector2(0, 620), new Vector2(0, 425));
        Pos(rtSubtitulo, new Vector2(0, 360), new Vector2(0, 322));
        Pos(rtRecorde,   new Vector2(0, 295), new Vector2(0, 268));

        PosTam(rtContinuar, new Vector2(0,  195), new Vector2(560, 130),
                            new Vector2(0,  165), new Vector2(460, 110));
        PosTam(rtJogar,     new Vector2(0,   60), new Vector2(560, 130),
                            new Vector2(0,   30), new Vector2(430, 145));
        PosTam(rtTreinar,   new Vector2(0, -110), new Vector2(560, 130),
                            new Vector2(-500, 30), new Vector2(400, 125));
        PosTam(rtSair,      new Vector2(0, -280), new Vector2(560, 130),
                            new Vector2(500,  30), new Vector2(400, 125));
        Pos(rtDicaMenu,  new Vector2(0, -460), new Vector2(0, -125));
        Pos(rtCreditos,  new Vector2(0, -700), new Vector2(0, -350));

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
        // Cartão da palavra com "slot" à esquerda para o objeto 3D
        if (painelPalavra != null)
        {
            PosTam((RectTransform)painelPalavra.transform,
                new Vector2(0, 530), new Vector2(960, 250),
                new Vector2(0, 210), new Vector2(1200, 240));

            if (uiControle == null)
                uiControle = painelPalavra.GetComponentInChildren<UIControle>(true);
            if (uiControle != null)
                uiControle.DefinirEspacoDoObjeto(h ? 300f : 260f, h ? 145f : 120f);
        }
        if (botaoPular != null)
            PosTam((RectTransform)botaoPular.transform,
                new Vector2(250, 240), new Vector2(460, 110),
                new Vector2(775, 210), new Vector2(330, 110));
        if (botaoPularLetra != null)
            PosTam((RectTransform)botaoPularLetra.transform,
                new Vector2(-250, 240), new Vector2(460, 110),
                new Vector2(-775, 210), new Vector2(330, 110));

        // Objeto 3D posicionado sobre o slot esquerdo do cartão
        if (gerenciador != null && gerenciador.pontoDeExibicao != null)
        {
            gerenciador.pontoDeExibicao.position =
                h ? new Vector3(-7.2f, -4.3f, 5f) : new Vector3(-3.05f, -2.9f, 5f);
            gerenciador.tamanhoDoObjeto = h ? 3.1f : 2.0f;
        }
    }

    // ── Ações dos botões ─────────────────────────────────────────────────────

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

    void AbrirMenuComSom()
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
        if (podeContinuar) gerenciador.Pausar();
        else               gerenciador.PararJogo();
        if (botaoContinuar != null) botaoContinuar.SetActive(podeContinuar);
        if (rotuloJogar    != null) rotuloJogar.text = podeContinuar ? "RECOMEÇAR" : "JOGAR";

        controlador.MODO_TREINAMENTO = false;

        telaMenu.SetActive(true);
        telaMenu.transform.SetAsLastSibling(); // menu por cima de tudo
        if (painelSenha  != null) painelSenha.SetActive(false);
        if (painelOpcoes != null) painelOpcoes.SetActive(false);

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
        dicaTreinamento.SetActive(false);
        if (painelPalavra   != null) painelPalavra.SetActive(false);
        if (botaoPular      != null) botaoPular.SetActive(false);
        if (botaoPularLetra != null) botaoPularLetra.SetActive(false);
    }

    void FecharMenu()
    {
        menuAberto = false;
        telaMenu.SetActive(false);
        botaoMenuHud.SetActive(true);

        // No jogo, o som volta para baixo do botão MENU
        if (botaoSom != null)
            ((RectTransform)botaoSom.transform).anchoredPosition = new Vector2(-30, -135);
    }
}
