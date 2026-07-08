using UnityEngine;
using UnityEngine.UI;
using TMPro;

// MenuPrincipal: constrói o menu inicial e o HUD do jogo por código.
// Fica no Canvas. Controla a troca entre MENU ↔ JOGO ↔ TREINAMENTO.
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

    GameObject telaMenu;
    GameObject botaoMenuHud;
    GameObject dicaTreinamento;
    TextMeshProUGUI textoContagem; // contador de amostras por letra (treinamento)
    bool  menuAberto;
    float timerFimDeJogo;
    float timerContagem;

    void Awake()
    {
        ConstruirMenu();
        ConstruirHud();
    }

    void Start()
    {
        AbrirMenu();
        GerenciadorDeAudio.TocarMusica();
    }

    void Update()
    {
        // Quando o jogo termina ("FIM DO JOGO!"), espera 4s e volta ao menu
        if (!menuAberto && gerenciador != null && gerenciador.JogoTerminado)
        {
            timerFimDeJogo += Time.deltaTime;
            if (timerFimDeJogo >= 4f) AbrirMenu();
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
        // Fundo em gradiente (azul-noite → roxo) cobrindo a tela inteira
        var fundo = UIFabrica.CriarImagem(transform, "TelaMenu", Color.white,
            Vector2.zero, Vector2.zero, UIFabrica.Gradiente(COR_FUNDO_TOPO, COR_FUNDO_BASE));
        var rt = fundo.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        telaMenu = fundo.gameObject;

        // Estrelas decorativas (bolinhas em posições fixas sorteadas com semente)
        var sorteio = new System.Random(7);
        for (int i = 0; i < 45; i++)
        {
            float x    = (float)sorteio.NextDouble() * 1000f - 500f;
            float y    = (float)sorteio.NextDouble() * 1780f - 890f;
            float tam  = 4f + (float)sorteio.NextDouble() * 10f;
            float alfa = 0.25f + (float)sorteio.NextDouble() * 0.6f;
            UIFabrica.CriarImagem(telaMenu.transform, "Estrela", new Color(1f, 1f, 1f, alfa),
                new Vector2(x, y), new Vector2(tam, tam), UIFabrica.Circulo());
        }

        UIFabrica.CriarTexto(telaMenu.transform, "Titulo1", "DITADO",
            130f, COR_TITULO, new Vector2(0, 620), new Vector2(1000, 150));
        UIFabrica.CriarTexto(telaMenu.transform, "Titulo2", "ESTRELADO",
            130f, COR_TITULO, new Vector2(0, 480), new Vector2(1000, 150));
        UIFabrica.CriarTexto(telaMenu.transform, "Subtitulo", "Aprenda o alfabeto em LIBRAS",
            46f, new Color(1f, 1f, 1f, 0.9f), new Vector2(0, 360), new Vector2(1000, 80), false);

        UIFabrica.CriarBotao(telaMenu.transform, "BotaoJogar", "JOGAR", COR_JOGAR,
            new Vector2(0, 60),   new Vector2(560, 130), 56f, controlador, Jogar);
        UIFabrica.CriarBotao(telaMenu.transform, "BotaoTreinar", "TREINAMENTO", COR_TREINAR,
            new Vector2(0, -110), new Vector2(560, 130), 52f, controlador, Treinar);
        UIFabrica.CriarBotao(telaMenu.transform, "BotaoSair", "SAIR", COR_SAIR,
            new Vector2(0, -280), new Vector2(560, 130), 52f, controlador, Sair);

        UIFabrica.CriarTexto(telaMenu.transform, "Dica",
            "Toque no botão ou aponte o dedo por 3 segundos",
            32f, new Color(1f, 1f, 1f, 0.7f), new Vector2(0, -460), new Vector2(1000, 60), false);

        UIFabrica.CriarTexto(telaMenu.transform, "Creditos",
            "Produzido por: Marcus Strabello\n" +
            "Apresentado ao professor Dr. Alex Martins Santos\n" +
            "da disciplina de Visão Computacional do Programa de\n" +
            "Pós-Graduação em Computação Aplicada (PPGCA) do\n" +
            "Instituto Federal do Maranhão (IFMA)\n" +
            "Campus São Luís - Monte Castelo",
            26f, new Color(1f, 1f, 1f, 0.6f), new Vector2(0, -680), new Vector2(980, 220), false);
    }

    void ConstruirHud()
    {
        // Botão MENU no canto superior direito (visível durante jogo/treinamento)
        var botao = UIFabrica.CriarBotao(transform, "BotaoMenuHud", "MENU", COR_HUD,
            new Vector2(-30, -30), new Vector2(220, 90), 40f, controlador, AbrirMenuComSom);
        UIFabrica.Ancorar(botao, new Vector2(1f, 1f), new Vector2(1f, 1f));
        botaoMenuHud = botao.gameObject;

        // Cartão de instruções do modo treinamento
        var painel = UIFabrica.CriarImagem(transform, "DicaTreinamento",
            new Color(1f, 1f, 1f, 0.88f), new Vector2(0, -160), new Vector2(980, 400),
            UIFabrica.Arredondado(), true);
        UIFabrica.Ancorar(painel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        UIFabrica.CriarTexto(painel.transform, "Texto",
            "MODO TREINAMENTO\n\n" +
            "Faça o sinal e pressione a tecla da letra\n" +
            "para gravar (grave várias vezes!)\n" +
            "H J K X Z gravam o MOVIMENTO por 1,3s\n" +
            "Shift + tecla apaga a letra",
            36f, new Color(0.15f, 0.15f, 0.22f, 1f), new Vector2(0, 45), new Vector2(940, 290), false);

        // Contador ao vivo: quantas amostras cada letra tem no banco
        textoContagem = UIFabrica.CriarTexto(painel.transform, "Contagem", "",
            34f, new Color(0.1f, 0.35f, 0.7f, 1f), new Vector2(0, -150), new Vector2(940, 80));
        textoContagem.enableAutoSizing = true;
        textoContagem.fontSizeMin = 20f;
        textoContagem.fontSizeMax = 34f;

        dicaTreinamento = painel.gameObject;
    }

    // ── Ações dos botões ─────────────────────────────────────────────────────

    void Jogar()
    {
        GerenciadorDeAudio.TocarClique();
        FecharMenu();
        controlador.MODO_TREINAMENTO = false;
        dicaTreinamento.SetActive(false);
        if (painelPalavra != null) painelPalavra.SetActive(true);
        if (botaoPular    != null) botaoPular.SetActive(true);
        gerenciador.IniciarJogo();
    }

    void Treinar()
    {
        GerenciadorDeAudio.TocarClique();
        FecharMenu();
        controlador.MODO_TREINAMENTO = true;
        gerenciador.PararJogo();
        if (painelPalavra != null) painelPalavra.SetActive(false);
        if (botaoPular    != null) botaoPular.SetActive(false);
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

        gerenciador.PararJogo();
        controlador.MODO_TREINAMENTO = false;

        telaMenu.SetActive(true);
        telaMenu.transform.SetAsLastSibling(); // menu por cima de tudo
        botaoMenuHud.SetActive(false);
        dicaTreinamento.SetActive(false);
        if (painelPalavra != null) painelPalavra.SetActive(false);
        if (botaoPular    != null) botaoPular.SetActive(false);
    }

    void FecharMenu()
    {
        menuAberto = false;
        telaMenu.SetActive(false);
        botaoMenuHud.SetActive(true);
    }
}
