using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// ModoEstudo: tela de consulta do alfabeto em LIBRAS.
//
// Mostra uma letra por vez com o desenho de uma mao reproduzindo o sinal.
// Nas letras com movimento (H, J, K, W, X, Z e C-cedilha) a sequencia
// gravada e reproduzida em laco, para o usuario ver o gesto completo.
//
// A mao e montada a partir dos 21 pontos do banco de sinais do jogo
// (MeuAlfabeto.asset): a palma e os dedos sao desenhados como faixas
// arredondadas com contorno, formando uma silhueta em vez de um traco.
//
// O usuario tambem pode praticar: se ele fizer o sinal da letra exibida
// diante da camera, a tela avisa com som e destaque verde.
public class ModoEstudo : MonoBehaviour
{
    const string ALFABETO = "ABCDEFGHIJKLMNOPQRSTUVWXYZÇ";

    // Ossos finos (dedos)
    static readonly int[,] OSSOS_DEDOS =
    {
        {1,2},{2,3},{3,4},          // polegar
        {5,6},{6,7},{7,8},          // indicador
        {9,10},{10,11},{11,12},     // medio
        {13,14},{14,15},{15,16},    // anelar
        {17,18},{18,19},{19,20}     // minimo
    };

    // Ossos grossos: preenchem a palma da mao
    static readonly int[,] OSSOS_PALMA =
    {
        {0,1},{0,5},{0,9},{0,13},{0,17},
        {5,9},{9,13},{13,17},{5,17}
    };

    // Juntas que fazem parte da palma (recebem circulos maiores)
    static readonly int[] JUNTAS_PALMA = { 0, 1, 5, 9, 13, 17 };

    static readonly Color COR_FUNDO    = new Color(0.07f, 0.09f, 0.25f, 0.86f);
    static readonly Color COR_PELE     = new Color(0.98f, 0.80f, 0.64f, 1f);
    static readonly Color COR_CONTORNO = new Color(0.32f, 0.19f, 0.13f, 1f);
    static readonly Color COR_TITULO   = new Color(1f,    0.85f, 0.25f, 1f);
    static readonly Color COR_BOTAO    = new Color(0.15f, 0.50f, 0.90f, 1f);
    static readonly Color COR_ACERTO   = new Color(0.15f, 0.85f, 0.35f, 1f);

    ControladorCamera  controlador;
    ReconhecedorLibras reconhecedor;
    System.Action      aoVoltar;

    RectTransform rtTitulo, rtLetra, rtSubtitulo, rtArea, rtContador, rtPratica;
    TextMeshProUGUI letraGrande, subtitulo, contador, aviso, textoPratica;
    Image fundoDaTela, brilhoAcerto;
    RectTransform areaDaMao;

    // Duas camadas de desenho: contorno (atras, mais grosso) e pele (na frente)
    readonly List<RectTransform> ossosContorno = new List<RectTransform>();
    readonly List<RectTransform> ossosPele     = new List<RectTransform>();
    readonly List<RectTransform> juntasContorno = new List<RectTransform>();
    readonly List<RectTransform> juntasPele     = new List<RectTransform>();

    int indiceLetra = 0;
    bool horizontal = false;

    readonly List<Vector3[]> quadros = new List<Vector3[]>();
    int   quadroAtual = 0;
    float tempoDoProximoQuadro = 0f;
    Vector2 centroDoDesenho = Vector2.zero;
    float   escalaDoDesenho = 1f;
    float   tamanhoAlvo     = 430f;
    float   grossuraDedo    = 34f;
    float   grossuraPalma   = 60f;

    // Pratica: o usuario reproduz o sinal diante da camera
    bool  letraAtualDinamica = false;
    float tempoSinalCerto    = 0f;
    float tempoDoProximoAviso = 0f;
    bool  comemorando = false;

    // ── Criacao da tela ─────────────────────────────────────────────────────

    public static ModoEstudo Criar(Transform canvas, ControladorCamera controlador,
                                   System.Action aoVoltar)
    {
        var fundo = UIFabrica.CriarImagem(canvas, "TelaEstudo", COR_FUNDO,
            Vector2.zero, Vector2.zero);
        var rt = fundo.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var tela = fundo.gameObject.AddComponent<ModoEstudo>();
        tela.fundoDaTela  = fundo;
        tela.controlador  = controlador;
        tela.reconhecedor = controlador.reconhecedor;
        tela.aoVoltar     = aoVoltar;
        tela.Construir();
        return tela;
    }

    void Construir()
    {
        var titulo = UIFabrica.CriarTexto(transform, "Titulo", "APRENDA OS SINAIS",
            42f, COR_TITULO, Vector2.zero, new Vector2(900, 60));
        UIFabrica.Ancorar(titulo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        rtTitulo = titulo.rectTransform;

        letraGrande = UIFabrica.CriarTexto(transform, "Letra", "A",
            200f, Color.white, Vector2.zero, new Vector2(420, 220));
        rtLetra = letraGrande.rectTransform;

        subtitulo = UIFabrica.CriarTexto(transform, "Subtitulo", "",
            32f, new Color(1f, 1f, 1f, 0.85f), Vector2.zero, new Vector2(560, 46), false);
        rtSubtitulo = subtitulo.rectTransform;

        // Area onde a mao e desenhada
        var area = UIFabrica.CriarImagem(transform, "AreaDaMao",
            new Color(1f, 1f, 1f, 0.06f), Vector2.zero, new Vector2(560, 560),
            UIFabrica.Arredondado(), true);
        area.raycastTarget = false;
        areaDaMao = area.rectTransform;
        rtArea    = areaDaMao;

        // Brilho verde que pisca quando o usuario acerta o sinal
        brilhoAcerto = UIFabrica.CriarImagem(areaDaMao, "BrilhoAcerto",
            new Color(0.15f, 0.85f, 0.35f, 0f), Vector2.zero, new Vector2(560, 560),
            UIFabrica.Arredondado(), true);
        brilhoAcerto.raycastTarget = false;

        // Camada de contorno primeiro (fica atras), depois a pele
        for (int i = 0; i < OSSOS_PALMA.GetLength(0) + OSSOS_DEDOS.GetLength(0); i++)
            ossosContorno.Add(CriarFaixa(COR_CONTORNO));
        for (int i = 0; i < 21; i++) juntasContorno.Add(CriarJunta(COR_CONTORNO));
        for (int i = 0; i < OSSOS_PALMA.GetLength(0) + OSSOS_DEDOS.GetLength(0); i++)
            ossosPele.Add(CriarFaixa(COR_PELE));
        for (int i = 0; i < 21; i++) juntasPele.Add(CriarJunta(COR_PELE));

        aviso = UIFabrica.CriarTexto(areaDaMao, "Aviso", "",
            34f, new Color(1f, 1f, 1f, 0.8f), Vector2.zero, new Vector2(500, 220), false);

        CriarSeta("SetaEsquerda", new Vector2(0f, 0.5f), 180f, LetraAnterior);
        CriarSeta("SetaDireita",  new Vector2(1f, 0.5f), 0f,   ProximaLetra);

        textoPratica = UIFabrica.CriarTexto(transform, "Pratica", "",
            34f, new Color(1f, 1f, 1f, 0.85f), Vector2.zero, new Vector2(900, 50), false);
        rtPratica = textoPratica.rectTransform;

        contador = UIFabrica.CriarTexto(transform, "Contador", "",
            30f, new Color(1f, 1f, 1f, 0.7f), Vector2.zero, new Vector2(500, 44), false);
        UIFabrica.Ancorar(contador, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        rtContador = contador.rectTransform;

        var voltar = UIFabrica.CriarBotao(transform, "Voltar", "VOLTAR AO MENU",
            new Color(0.4f, 0.4f, 0.5f, 1f), new Vector2(0, 85), new Vector2(440, 100),
            36f, controlador, Voltar);
        UIFabrica.Ancorar(voltar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

        AplicarLayout(false);
    }

    void CriarSeta(string nome, Vector2 ancora, float giro,
                   UnityEngine.Events.UnityAction acao)
    {
        var botao = UIFabrica.CriarBotao(transform, nome, "", COR_BOTAO,
            new Vector2(ancora.x < 0.5f ? 75 : -75, 0), new Vector2(120, 190),
            30f, controlador, acao);
        UIFabrica.Ancorar(botao, ancora, new Vector2(0.5f, 0.5f));

        var icone = UIFabrica.CriarImagem(botao.transform, "Icone", Color.white,
            Vector2.zero, new Vector2(66, 66), UIFabrica.Seta());
        icone.rectTransform.localEulerAngles = new Vector3(0, 0, giro);
        icone.raycastTarget = false;
    }

    RectTransform CriarFaixa(Color cor)
    {
        var img = UIFabrica.CriarImagem(areaDaMao, "Osso", cor, Vector2.zero,
            new Vector2(10, 10), UIFabrica.Arredondado(), true);
        img.raycastTarget = false;
        return img.rectTransform;
    }

    RectTransform CriarJunta(Color cor)
    {
        var img = UIFabrica.CriarImagem(areaDaMao, "Junta", cor, Vector2.zero,
            new Vector2(10, 10), UIFabrica.Circulo());
        img.raycastTarget = false;
        return img.rectTransform;
    }

    // ── Layout: muda conforme a orientacao da tela ──────────────────────────

    public void AplicarLayout(bool telaHorizontal)
    {
        horizontal = telaHorizontal;

        if (horizontal)
        {
            // Deitado: texto a esquerda, mao a direita
            rtTitulo.anchoredPosition    = new Vector2(0, -48);
            UIFabrica.Ancorar(letraGrande, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rtLetra.anchoredPosition     = new Vector2(-470, 55);
            UIFabrica.Ancorar(subtitulo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rtSubtitulo.anchoredPosition = new Vector2(-470, -90);
            rtArea.anchoredPosition      = new Vector2(430, -25);
            rtArea.sizeDelta             = new Vector2(520, 520);
            rtPratica.anchoredPosition   = new Vector2(-470, -175);
            rtContador.anchoredPosition  = new Vector2(0, 205);
            tamanhoAlvo = 380f;
        }
        else
        {
            // Em pe: tudo empilhado, com espaco entre os blocos
            rtTitulo.anchoredPosition    = new Vector2(0, -58);
            UIFabrica.Ancorar(letraGrande, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rtLetra.anchoredPosition     = new Vector2(0, -215);
            UIFabrica.Ancorar(subtitulo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rtSubtitulo.anchoredPosition = new Vector2(0, -365);
            rtArea.anchoredPosition      = new Vector2(0, -40);
            rtArea.sizeDelta             = new Vector2(600, 600);
            UIFabrica.Ancorar(textoPratica, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            rtPratica.anchoredPosition   = new Vector2(0, 280);
            rtContador.anchoredPosition  = new Vector2(0, 205);
            tamanhoAlvo = 430f;
        }

        if (horizontal)
            UIFabrica.Ancorar(textoPratica, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        brilhoAcerto.rectTransform.sizeDelta = rtArea.sizeDelta;
        grossuraDedo  = tamanhoAlvo * 0.085f;
        grossuraPalma = tamanhoAlvo * 0.150f;

        if (quadros.Count > 0)
        {
            PrepararEscala();
            DesenharQuadro(quadros[quadroAtual]);
        }
    }

    // ── Navegacao ───────────────────────────────────────────────────────────

    public void Abrir(bool telaHorizontal)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        AplicarLayout(telaHorizontal);
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
        if (Input.GetKeyDown(KeyCode.RightArrow)) ProximaLetra();
        if (Input.GetKeyDown(KeyCode.LeftArrow))  LetraAnterior();

        // Reproduz a sequencia de movimento em laco, com pausa no fim
        if (quadros.Count > 1 && Time.unscaledTime >= tempoDoProximoQuadro)
        {
            quadroAtual = (quadroAtual + 1) % quadros.Count;
            tempoDoProximoQuadro = Time.unscaledTime +
                                   ((quadroAtual == 0) ? 0.8f : 1f / 12f);
            DesenharQuadro(quadros[quadroAtual]);
        }

        VerificarPratica();
    }

    // ── Pratica: confere se o usuario esta fazendo o sinal ──────────────────

    void VerificarPratica()
    {
        if (comemorando || quadros.Count == 0) return;
        if (controlador == null || reconhecedor == null) return;
        if (Time.unscaledTime < tempoDoProximoAviso) return;

        if (!controlador.MaoDetectada || controlador.PontosDaMaoAtuais == null)
        {
            tempoSinalCerto = 0f;
            textoPratica.text  = "Faça o sinal para praticar";
            textoPratica.color = new Color(1f, 1f, 1f, 0.6f);
            return;
        }

        string letra = ALFABETO[indiceLetra].ToString();
        string feita = letraAtualDinamica
            ? reconhecedor.ClassificarSinalDinamico(controlador.JanelaDeMovimento)
            : reconhecedor.ClassificarLetra(controlador.PontosDaMaoAtuais);

        if (feita == letra)
        {
            if (tempoSinalCerto <= 0f) tempoSinalCerto = Time.unscaledTime;
            textoPratica.text  = "quase la...";
            textoPratica.color = new Color(1f, 1f, 1f, 0.9f);

            if (Time.unscaledTime - tempoSinalCerto >= 0.35f)
                StartCoroutine(Comemorar());
        }
        else
        {
            tempoSinalCerto = 0f;
            textoPratica.text  = "Faça o sinal para praticar";
            textoPratica.color = new Color(1f, 1f, 1f, 0.6f);
        }
    }

    IEnumerator Comemorar()
    {
        comemorando = true;
        GerenciadorDeAudio.TocarVitoria();

        textoPratica.text  = "MUITO BEM! VOCÊ FEZ O SINAL";
        textoPratica.color = COR_ACERTO;
        letraGrande.color  = COR_ACERTO;

        // O brilho verde cresce e some atras da mao
        float duracao = 1.4f;
        for (float t = 0f; t < duracao; t += Time.unscaledDeltaTime)
        {
            float p = t / duracao;
            var cor = brilhoAcerto.color;
            cor.a = Mathf.Sin(p * Mathf.PI) * 0.45f;
            brilhoAcerto.color = cor;
            yield return null;
        }

        brilhoAcerto.color = new Color(0.15f, 0.85f, 0.35f, 0f);
        letraGrande.color  = Color.white;
        textoPratica.text  = "Faça o sinal para praticar";
        textoPratica.color = new Color(1f, 1f, 1f, 0.6f);

        tempoSinalCerto     = 0f;
        tempoDoProximoAviso = Time.unscaledTime + 1.2f;
        comemorando = false;
    }

    // ── Carregamento e desenho da mao ───────────────────────────────────────

    void CarregarLetra()
    {
        string letra = ALFABETO[indiceLetra].ToString();
        letraGrande.text  = letra;
        letraGrande.color = Color.white;
        contador.text     = (indiceLetra + 1) + " de " + ALFABETO.Length;

        quadros.Clear();
        quadroAtual = 0;
        tempoSinalCerto = 0f;
        tempoDoProximoQuadro = Time.unscaledTime + 0.8f;

        var banco = (reconhecedor != null) ? reconhecedor.bancoDeDados : null;
        letraAtualDinamica = reconhecedor != null && reconhecedor.EhLetraDinamica(letra);

        if (banco != null && banco.sinaisDinamicos != null)
            foreach (var sinal in banco.sinaisDinamicos)
                if (sinal.nome == letra && sinal.quadros != null && sinal.quadros.Count > 1)
                {
                    foreach (var q in sinal.quadros) quadros.Add(q.pontos);
                    break;
                }

        if (quadros.Count == 0 && banco != null && banco.letrasGravadas != null)
            foreach (var padrao in banco.letrasGravadas)
                if (padrao.nome == letra)
                {
                    quadros.Add(padrao.pontosNormalizados);
                    break;
                }

        bool temSinal = quadros.Count > 0;
        MostrarMao(temSinal);
        aviso.gameObject.SetActive(!temSinal);
        textoPratica.gameObject.SetActive(temSinal);

        if (!temSinal)
        {
            subtitulo.text = "";
            aviso.text = "Sinal ainda não cadastrado.\n\n" +
                         "Grave esta letra no modo\ntreinamento para vê-la aqui.";
            return;
        }

        subtitulo.text = (quadros.Count > 1) ? "sinal com movimento" : "sinal parado";
        textoPratica.text  = "Faça o sinal para praticar";
        textoPratica.color = new Color(1f, 1f, 1f, 0.6f);

        PrepararEscala();
        DesenharQuadro(quadros[0]);
    }

    void MostrarMao(bool visivel)
    {
        for (int i = 0; i < ossosContorno.Count;  i++) ossosContorno[i].gameObject.SetActive(visivel);
        for (int i = 0; i < ossosPele.Count;      i++) ossosPele[i].gameObject.SetActive(visivel);
        for (int i = 0; i < juntasContorno.Count; i++) juntasContorno[i].gameObject.SetActive(visivel);
        for (int i = 0; i < juntasPele.Count;     i++) juntasPele[i].gameObject.SetActive(visivel);
    }

    // Escala unica para TODOS os quadros, senao a mao mudaria de tamanho
    // durante a animacao
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

        float maior = Mathf.Max(maxX - minX, maxY - minY);
        escalaDoDesenho = (maior > 0.0001f) ? tamanhoAlvo / maior : 1f;
        centroDoDesenho = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
    }

    void DesenharQuadro(Vector3[] quadro)
    {
        if (quadro == null || quadro.Length < 21) return;

        // Contorno usa faixas mais grossas e fica atras da pele
        DesenharCamada(quadro, ossosContorno, juntasContorno, 9f);
        DesenharCamada(quadro, ossosPele,     juntasPele,     0f);
    }

    void DesenharCamada(Vector3[] quadro, List<RectTransform> ossos,
                        List<RectTransform> juntas, float extra)
    {
        int n = 0;

        for (int i = 0; i < OSSOS_PALMA.GetLength(0); i++, n++)
            PosicionarFaixa(ossos[n], quadro[OSSOS_PALMA[i, 0]], quadro[OSSOS_PALMA[i, 1]],
                            grossuraPalma + extra);

        for (int i = 0; i < OSSOS_DEDOS.GetLength(0); i++, n++)
            PosicionarFaixa(ossos[n], quadro[OSSOS_DEDOS[i, 0]], quadro[OSSOS_DEDOS[i, 1]],
                            grossuraDedo + extra);

        for (int i = 0; i < 21; i++)
        {
            bool naPalma = System.Array.IndexOf(JUNTAS_PALMA, i) >= 0;
            float d = (naPalma ? grossuraPalma : grossuraDedo) + extra;
            juntas[i].anchoredPosition = ParaTela(quadro[i]);
            juntas[i].sizeDelta        = new Vector2(d, d);
        }
    }

    void PosicionarFaixa(RectTransform faixa, Vector3 de, Vector3 ate, float grossura)
    {
        Vector2 a = ParaTela(de), b = ParaTela(ate);
        Vector2 direcao = b - a;

        faixa.anchoredPosition = (a + b) * 0.5f;
        faixa.sizeDelta        = new Vector2(direcao.magnitude + grossura * 0.5f, grossura);
        faixa.localEulerAngles = new Vector3(0, 0,
            Mathf.Atan2(direcao.y, direcao.x) * Mathf.Rad2Deg);
    }

    Vector2 ParaTela(Vector3 ponto)
    {
        return (new Vector2(ponto.x, ponto.y) - centroDoDesenho) * escalaDoDesenho;
    }
}
