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

    // A mao e montada em 6 pecas independentes (palma e os cinco dedos).
    // Cada peca tem contorno proprio e as pecas sao reordenadas por
    // profundidade a cada quadro, entao um dedo que passa na frente da palma
    // aparece destacado em vez de se fundir com ela.
    class ParteDaMao
    {
        public RectTransform raiz;
        public int[,] ossos;              // ligacoes desenhadas
        public int[]  juntas;             // pontos que recebem circulo
        public float[] escalaDoOsso;      // afinamento ao longo do dedo
        public int   pontaDaUnha = -1;    // ponto da ponta (so nos dedos)
        public Image unha;
        public readonly List<Image> contorno = new List<Image>();
        public readonly List<Image> pele     = new List<Image>();
    }

    static readonly Color COR_FUNDO    = new Color(0.07f, 0.09f, 0.25f, 0.88f);
    static readonly Color COR_PELE     = new Color(0.99f, 0.82f, 0.66f, 1f);
    static readonly Color COR_PELE_FUNDO = new Color(0.72f, 0.53f, 0.40f, 1f); // partes ao fundo
    static readonly Color COR_UNHA     = new Color(1f,    0.92f, 0.88f, 1f);
    static readonly Color COR_CONTORNO = new Color(0.26f, 0.15f, 0.10f, 1f);
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

    readonly List<ParteDaMao> partes = new List<ParteDaMao>();

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

        MontarPartesDaMao();

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

    // ── Montagem das pecas da mao ───────────────────────────────────────────

    void MontarPartesDaMao()
    {
        // Palma: leque do pulso ate os nos dos dedos, com a borda fechada,
        // para o preenchimento nao deixar falhas
        CriarParte("Palma",
            new int[,] { {0,1},{1,5},{5,9},{9,13},{13,17},{17,0},
                         {0,5},{0,9},{0,13} },
            new int[] { 0, 1, 5, 9, 13, 17 },
            new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f }, -1);

        // Dedos: cada segmento afina em direcao a ponta
        float[] afinamento = { 0.92f, 0.82f, 0.72f };
        CriarParte("Polegar",   new int[,] { {1,2},{2,3},{3,4} },
                   new int[] { 2, 3, 4 },     afinamento, 4);
        CriarParte("Indicador", new int[,] { {5,6},{6,7},{7,8} },
                   new int[] { 6, 7, 8 },     afinamento, 8);
        CriarParte("Medio",     new int[,] { {9,10},{10,11},{11,12} },
                   new int[] { 10, 11, 12 },  afinamento, 12);
        CriarParte("Anelar",    new int[,] { {13,14},{14,15},{15,16} },
                   new int[] { 14, 15, 16 },  afinamento, 16);
        CriarParte("Minimo",    new int[,] { {17,18},{18,19},{19,20} },
                   new int[] { 18, 19, 20 },  afinamento, 20);
    }

    void CriarParte(string nome, int[,] ossos, int[] juntas,
                    float[] escalaDoOsso, int pontaDaUnha)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = 5;
        var raiz = go.GetComponent<RectTransform>();
        raiz.SetParent(areaDaMao, false);
        raiz.anchorMin = raiz.anchorMax = new Vector2(0.5f, 0.5f);
        raiz.sizeDelta = Vector2.zero;

        var parte = new ParteDaMao
        {
            raiz = raiz, ossos = ossos, juntas = juntas,
            escalaDoOsso = escalaDoOsso, pontaDaUnha = pontaDaUnha
        };

        // Primeiro TODO o contorno, depois TODA a pele: assim a peca ganha um
        // contorno continuo, sem emendas visiveis por dentro
        for (int i = 0; i < ossos.GetLength(0); i++)
            parte.contorno.Add(CriarForma(raiz, COR_CONTORNO, true));
        for (int i = 0; i < juntas.Length; i++)
            parte.contorno.Add(CriarForma(raiz, COR_CONTORNO, false));
        for (int i = 0; i < ossos.GetLength(0); i++)
            parte.pele.Add(CriarForma(raiz, COR_PELE, true));
        for (int i = 0; i < juntas.Length; i++)
            parte.pele.Add(CriarForma(raiz, COR_PELE, false));

        if (pontaDaUnha >= 0)
            parte.unha = CriarForma(raiz, COR_UNHA, false);

        partes.Add(parte);
    }

    Image CriarForma(Transform pai, Color cor, bool alongada)
    {
        var img = UIFabrica.CriarImagem(pai, alongada ? "Osso" : "Junta", cor,
            Vector2.zero, new Vector2(10, 10),
            alongada ? UIFabrica.Arredondado() : UIFabrica.Circulo(), alongada);
        img.raycastTarget = false;
        return img;
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
        grossuraDedo  = tamanhoAlvo * 0.088f;
        grossuraPalma = tamanhoAlvo * 0.125f;

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
        foreach (var parte in partes) parte.raiz.gameObject.SetActive(visivel);
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

        // Profundidade da cena: serve para ordenar as pecas e escurecer o que
        // esta mais longe. Em z menor a parte esta MAIS PERTO da camera.
        float zMin = float.MaxValue, zMax = float.MinValue;
        for (int i = 0; i < 21; i++)
        {
            if (quadro[i].z < zMin) zMin = quadro[i].z;
            if (quadro[i].z > zMax) zMax = quadro[i].z;
        }
        bool temProfundidade = (zMax - zMin) > 0.0001f;

        // Ordena as pecas: o que esta atras e desenhado primeiro
        var ordem = new List<KeyValuePair<float, ParteDaMao>>();
        for (int i = 0; i < partes.Count; i++)
        {
            float z = ProfundidadeDaParte(partes[i], quadro);
            // Sem eixo z (amostras antigas): mantem a ordem natural da mao
            if (!temProfundidade) z = -i;
            ordem.Add(new KeyValuePair<float, ParteDaMao>(z, partes[i]));
        }
        ordem.Sort((a, b) => b.Key.CompareTo(a.Key));

        for (int i = 0; i < ordem.Count; i++)
        {
            var parte = ordem[i].Value;
            parte.raiz.SetSiblingIndex(i);
            // O brilho de acerto fica atras da mao; o aviso, na frente
            brilhoAcerto.transform.SetAsFirstSibling();
            aviso.transform.SetAsLastSibling();

            // Quanto mais longe, mais escura fica a pele (nocao de volume)
            float tom = temProfundidade
                ? Mathf.InverseLerp(zMin, zMax, ordem[i].Key) : 0.25f;
            Color corDaPele = Color.Lerp(COR_PELE, COR_PELE_FUNDO, tom * 0.75f);
            DesenharParte(parte, quadro, corDaPele);
        }
    }

    float ProfundidadeDaParte(ParteDaMao parte, Vector3[] quadro)
    {
        float soma = 0f;
        for (int i = 0; i < parte.juntas.Length; i++) soma += quadro[parte.juntas[i]].z;
        return soma / Mathf.Max(1, parte.juntas.Length);
    }

    void DesenharParte(ParteDaMao parte, Vector3[] quadro, Color corDaPele)
    {
        bool ehPalma = (parte.pontaDaUnha < 0);
        float baseDaParte = ehPalma ? grossuraPalma : grossuraDedo;
        float bordaExtra  = grossuraDedo * 0.30f; // espessura do contorno

        int nOssos = parte.ossos.GetLength(0);
        for (int i = 0; i < nOssos; i++)
        {
            float g = baseDaParte * parte.escalaDoOsso[Mathf.Min(i, parte.escalaDoOsso.Length - 1)];
            Vector3 de  = quadro[parte.ossos[i, 0]];
            Vector3 ate = quadro[parte.ossos[i, 1]];
            PosicionarFaixa(parte.contorno[i].rectTransform, de, ate, g + bordaExtra);
            PosicionarFaixa(parte.pele[i].rectTransform,     de, ate, g);
            parte.pele[i].color = corDaPele;
        }

        for (int i = 0; i < parte.juntas.Length; i++)
        {
            int indice = parte.juntas[i];
            float g = ehPalma
                ? grossuraPalma
                : grossuraDedo * parte.escalaDoOsso[Mathf.Min(i, parte.escalaDoOsso.Length - 1)];

            var contorno = parte.contorno[nOssos + i].rectTransform;
            var pele     = parte.pele[nOssos + i].rectTransform;
            Vector2 pos  = ParaTela(quadro[indice]);

            contorno.anchoredPosition = pos;
            contorno.sizeDelta        = new Vector2(g + bordaExtra, g + bordaExtra);
            pele.anchoredPosition     = pos;
            pele.sizeDelta            = new Vector2(g, g);
            parte.pele[nOssos + i].color = corDaPele;
        }

        // Unha na ponta do dedo: ajuda a identificar qual dedo e para onde aponta
        if (parte.unha != null)
        {
            int ponta = parte.pontaDaUnha;
            int antes = parte.ossos[nOssos - 1, 0];
            Vector2 p = ParaTela(quadro[ponta]);
            Vector2 direcao = p - ParaTela(quadro[antes]);
            float g = grossuraDedo * 0.72f;

            var rt = parte.unha.rectTransform;
            rt.anchoredPosition = p + direcao.normalized * (g * 0.10f);
            rt.sizeDelta        = new Vector2(g * 0.70f, g * 0.52f);
            rt.localEulerAngles = new Vector3(0, 0,
                Mathf.Atan2(direcao.y, direcao.x) * Mathf.Rad2Deg);
            parte.unha.color = Color.Lerp(COR_UNHA, COR_PELE_FUNDO,
                                          1f - corDaPele.r / COR_PELE.r);
        }
    }

    void PosicionarFaixa(RectTransform faixa, Vector3 de, Vector3 ate, float grossura)
    {
        Vector2 a = ParaTela(de), b = ParaTela(ate);
        Vector2 direcao = b - a;

        faixa.anchoredPosition = (a + b) * 0.5f;
        faixa.sizeDelta        = new Vector2(direcao.magnitude + grossura, grossura);
        faixa.localEulerAngles = new Vector3(0, 0,
            Mathf.Atan2(direcao.y, direcao.x) * Mathf.Rad2Deg);
    }

    // Espelha o X para a mao aparecer como o usuario ve a propria mao na tela
    Vector2 ParaTela(Vector3 ponto)
    {
        return new Vector2(-(ponto.x - centroDoDesenho.x),
                            (ponto.y - centroDoDesenho.y)) * escalaDoDesenho;
    }
}
