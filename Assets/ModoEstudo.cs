using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// ModoEstudo: tela de consulta do alfabeto em LIBRAS.
//
// Mostra uma letra por vez em tela cheia, com a letra escrita em destaque e
// o desenho da mao correspondente. Nas letras com movimento (H, J, K, W, X,
// Z e C-cedilha) a sequencia gravada e reproduzida em laco, para o usuario
// ver o gesto completo.
//
// O desenho vem do proprio banco de sinais do jogo (MeuAlfabeto.asset), ou
// seja, a referencia de estudo e exatamente o padrao que o reconhecedor usa.
public class ModoEstudo : MonoBehaviour
{
    const string ALFABETO = "ABCDEFGHIJKLMNOPQRSTUVWXYZÇ";

    // Ligacoes entre os 21 pontos da mao (as mesmas do esqueleto do jogo)
    static readonly int[,] OSSOS =
    {
        {0,1},{1,2},{2,3},{3,4},          // polegar
        {0,5},{5,6},{6,7},{7,8},          // indicador
        {0,9},{9,10},{10,11},{11,12},     // medio
        {0,13},{13,14},{14,15},{15,16},   // anelar
        {0,17},{17,18},{18,19},{19,20},   // minimo
        {5,9},{9,13},{13,17}              // palma
    };

    static readonly Color COR_FUNDO   = new Color(0.07f, 0.09f, 0.25f, 0.96f);
    static readonly Color COR_LINHA   = new Color(0f,    0.85f, 0.85f, 0.95f);
    static readonly Color COR_PONTO   = new Color(1f,    1f,    1f,    1f);
    static readonly Color COR_PULSO   = new Color(1f,    0.8f,  0.1f,  1f);
    static readonly Color COR_TITULO  = new Color(1f,    0.85f, 0.25f, 1f);
    static readonly Color COR_BOTAO   = new Color(0.15f, 0.50f, 0.90f, 1f);

    ReconhecedorLibras reconhecedor;
    System.Action aoVoltar;

    TextMeshProUGUI letraGrande, subtitulo, contador, aviso;
    RectTransform   areaDaMao;
    readonly List<RectTransform> linhas = new List<RectTransform>();
    readonly List<RectTransform> pontos = new List<RectTransform>();

    int indiceLetra = 0;

    // Quadros da letra atual (uma pose so, se a letra for parada)
    readonly List<Vector3[]> quadros = new List<Vector3[]>();
    int   quadroAtual = 0;
    float tempoDoProximoQuadro = 0f;
    Vector2 centroDoDesenho = Vector2.zero;
    float   escalaDoDesenho = 1f;

    // ── Criacao da tela ─────────────────────────────────────────────────────

    public static ModoEstudo Criar(Transform canvas, ReconhecedorLibras reconhecedor,
                                   ControladorCamera controlador, System.Action aoVoltar)
    {
        var fundo = UIFabrica.CriarImagem(canvas, "TelaEstudo", COR_FUNDO,
            Vector2.zero, Vector2.zero);
        var rt = fundo.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var tela = fundo.gameObject.AddComponent<ModoEstudo>();
        tela.reconhecedor = reconhecedor;
        tela.aoVoltar     = aoVoltar;
        tela.Construir(controlador);
        return tela;
    }

    void Construir(ControladorCamera controlador)
    {
        // Titulo da tela, preso ao topo (funciona em pe e deitado)
        var titulo = UIFabrica.CriarTexto(transform, "Titulo", "APRENDA OS SINAIS",
            44f, COR_TITULO, new Vector2(0, -55), new Vector2(900, 70));
        UIFabrica.Ancorar(titulo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        // Letra em destaque
        letraGrande = UIFabrica.CriarTexto(transform, "Letra", "A",
            190f, Color.white, new Vector2(0, -175), new Vector2(400, 210));
        UIFabrica.Ancorar(letraGrande, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        subtitulo = UIFabrica.CriarTexto(transform, "Subtitulo", "",
            34f, new Color(1f, 1f, 1f, 0.85f), new Vector2(0, -300), new Vector2(900, 50), false);
        UIFabrica.Ancorar(subtitulo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        // Area central onde a mao e desenhada
        var area = UIFabrica.CriarImagem(transform, "AreaDaMao",
            new Color(1f, 1f, 1f, 0.05f), new Vector2(0, -40), new Vector2(620, 620),
            UIFabrica.Arredondado(), true);
        area.raycastTarget = false;
        areaDaMao = area.rectTransform;

        for (int i = 0; i < OSSOS.GetLength(0); i++) linhas.Add(CriarLinha());
        for (int i = 0; i < 21; i++)                 pontos.Add(CriarPonto(i));

        aviso = UIFabrica.CriarTexto(areaDaMao, "Aviso", "",
            36f, new Color(1f, 1f, 1f, 0.75f), Vector2.zero, new Vector2(560, 200), false);

        // Setas laterais: esquerda volta, direita avanca
        CriarSeta(controlador, "SetaEsquerda", new Vector2(0f, 0.5f),
                  new Vector2(70, -40), 180f, LetraAnterior);
        CriarSeta(controlador, "SetaDireita",  new Vector2(1f, 0.5f),
                  new Vector2(-70, -40), 0f,  ProximaLetra);

        // Rodape: contador e botao de voltar
        contador = UIFabrica.CriarTexto(transform, "Contador", "",
            32f, new Color(1f, 1f, 1f, 0.7f), new Vector2(0, 170), new Vector2(500, 50), false);
        UIFabrica.Ancorar(contador, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

        var voltar = UIFabrica.CriarBotao(transform, "Voltar", "VOLTAR AO MENU",
            new Color(0.4f, 0.4f, 0.5f, 1f), new Vector2(0, 90), new Vector2(460, 110),
            38f, controlador, Voltar);
        UIFabrica.Ancorar(voltar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

        CarregarLetra();
    }

    void CriarSeta(ControladorCamera controlador, string nome, Vector2 ancora,
                   Vector2 pos, float giro, UnityEngine.Events.UnityAction acao)
    {
        var botao = UIFabrica.CriarBotao(transform, nome, "", COR_BOTAO,
            pos, new Vector2(130, 200), 30f, controlador, acao);
        UIFabrica.Ancorar(botao, ancora, new Vector2(0.5f, 0.5f));

        var icone = UIFabrica.CriarImagem(botao.transform, "Icone", Color.white,
            Vector2.zero, new Vector2(70, 70), UIFabrica.Seta());
        icone.rectTransform.localEulerAngles = new Vector3(0, 0, giro);
        icone.raycastTarget = false;
    }

    RectTransform CriarLinha()
    {
        var go = new GameObject("Linha", typeof(Image));
        go.layer = 5;
        go.transform.SetParent(areaDaMao, false);
        var img = go.GetComponent<Image>();
        img.color = COR_LINHA;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 8);
        return rt;
    }

    RectTransform CriarPonto(int indice)
    {
        var go = new GameObject("Ponto", typeof(Image));
        go.layer = 5;
        go.transform.SetParent(areaDaMao, false);
        var img = go.GetComponent<Image>();
        img.color  = (indice == 0) ? COR_PULSO : COR_PONTO;
        img.sprite = UIFabrica.Circulo();
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(24, 24);
        return rt;
    }

    // ── Navegacao ───────────────────────────────────────────────────────────

    public void Abrir()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        CarregarLetra();
    }

    void Voltar()
    {
        GerenciadorDeAudio.TocarClique();
        gameObject.SetActive(false);
        if (aoVoltar != null) aoVoltar();
    }

    void ProximaLetra()
    {
        GerenciadorDeAudio.TocarClique();
        indiceLetra = (indiceLetra + 1) % ALFABETO.Length;
        CarregarLetra();
    }

    void LetraAnterior()
    {
        GerenciadorDeAudio.TocarClique();
        indiceLetra = (indiceLetra - 1 + ALFABETO.Length) % ALFABETO.Length;
        CarregarLetra();
    }

    void Update()
    {
        // As setas do teclado tambem navegam
        if (Input.GetKeyDown(KeyCode.RightArrow)) ProximaLetra();
        if (Input.GetKeyDown(KeyCode.LeftArrow))  LetraAnterior();

        if (quadros.Count == 0) return;

        // Sequencia de movimento reproduzida em laco, com uma pausa no fim
        if (quadros.Count > 1 && Time.unscaledTime >= tempoDoProximoQuadro)
        {
            quadroAtual = (quadroAtual + 1) % quadros.Count;
            float espera = (quadroAtual == 0) ? 0.7f : 1f / 12f;
            tempoDoProximoQuadro = Time.unscaledTime + espera;
            DesenharQuadro(quadros[quadroAtual]);
        }
    }

    // ── Carregamento e desenho da mao ───────────────────────────────────────

    void CarregarLetra()
    {
        string letra = ALFABETO[indiceLetra].ToString();
        letraGrande.text = letra;
        contador.text    = (indiceLetra + 1) + " de " + ALFABETO.Length;

        quadros.Clear();
        quadroAtual = 0;
        tempoDoProximoQuadro = Time.unscaledTime + 0.7f;

        var banco = (reconhecedor != null) ? reconhecedor.bancoDeDados : null;
        bool ehDinamica = reconhecedor != null && reconhecedor.EhLetraDinamica(letra);

        // Letras com movimento: procura a sequencia gravada
        if (banco != null && banco.sinaisDinamicos != null)
            foreach (var sinal in banco.sinaisDinamicos)
                if (sinal.nome == letra && sinal.quadros != null && sinal.quadros.Count > 1)
                {
                    foreach (var q in sinal.quadros) quadros.Add(q.pontos);
                    break;
                }

        // Letras paradas: uma pose basta
        if (quadros.Count == 0 && banco != null && banco.letrasGravadas != null)
            foreach (var padrao in banco.letrasGravadas)
                if (padrao.nome == letra)
                {
                    quadros.Add(padrao.pontosNormalizados);
                    break;
                }

        bool temSinal = quadros.Count > 0;
        MostrarEsqueleto(temSinal);
        aviso.gameObject.SetActive(!temSinal);

        if (!temSinal)
        {
            subtitulo.text = "";
            aviso.text = "Este sinal ainda nao foi cadastrado.\n" +
                         "Grave a letra no modo treinamento\n" +
                         "para que ela apareca aqui.";
            return;
        }

        subtitulo.text = (quadros.Count > 1)
            ? "sinal com movimento"
            : (ehDinamica ? "sinal com movimento (grave a sequencia)" : "sinal parado");

        PrepararEscala();
        DesenharQuadro(quadros[0]);
    }

    void MostrarEsqueleto(bool visivel)
    {
        for (int i = 0; i < linhas.Count; i++) linhas[i].gameObject.SetActive(visivel);
        for (int i = 0; i < pontos.Count; i++) pontos[i].gameObject.SetActive(visivel);
    }

    // Calcula uma escala unica para TODOS os quadros, senao a mao "pularia"
    // de tamanho durante a animacao
    void PrepararEscala()
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var quadro in quadros)
            for (int i = 0; i < 21 && i < quadro.Length; i++)
            {
                if (quadro[i].x < minX) minX = quadro[i].x;
                if (quadro[i].x > maxX) maxX = quadro[i].x;
                if (quadro[i].y < minY) minY = quadro[i].y;
                if (quadro[i].y > maxY) maxY = quadro[i].y;
            }

        float largura = maxX - minX, altura = maxY - minY;
        float maior   = Mathf.Max(largura, altura);
        escalaDoDesenho = (maior > 0.0001f) ? 430f / maior : 1f;
        centroDoDesenho = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
    }

    void DesenharQuadro(Vector3[] quadro)
    {
        if (quadro == null || quadro.Length < 21) return;

        for (int i = 0; i < 21; i++)
            pontos[i].anchoredPosition = ParaTela(quadro[i]);

        for (int i = 0; i < OSSOS.GetLength(0); i++)
        {
            Vector2 a = ParaTela(quadro[OSSOS[i, 0]]);
            Vector2 b = ParaTela(quadro[OSSOS[i, 1]]);
            Vector2 direcao = b - a;

            linhas[i].anchoredPosition = (a + b) * 0.5f;
            linhas[i].sizeDelta        = new Vector2(direcao.magnitude, 8f);
            linhas[i].localEulerAngles = new Vector3(0, 0,
                Mathf.Atan2(direcao.y, direcao.x) * Mathf.Rad2Deg);
        }
    }

    Vector2 ParaTela(Vector3 ponto)
    {
        return (new Vector2(ponto.x, ponto.y) - centroDoDesenho) * escalaDoDesenho;
    }
}
