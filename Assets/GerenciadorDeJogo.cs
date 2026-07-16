using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// GerenciadorDeJogo: controla fases, palavras, pontos, vidas e tempo.
//
// FASES: 1-Frutas, 2-Animais, 3-Transporte, 4-Comidas (dificuldade crescente:
// menos tempo por letra a cada fase). Os modelos 3D são carregados pelo nome
// de dentro de Assets/Resources e redimensionados automaticamente.
//
// ECONOMIA: +10 por letra, +50 por palavra; pular letra custa 5, pular
// palavra custa 10. VIDAS: começa com 3, perde ao estourar o tempo,
// ganha 1 a cada 100 pontos (até o máximo).
public class GerenciadorDeJogo : MonoBehaviour
{
    // ── Estrutura das fases ──────────────────────────────────────────────────
    [System.Serializable]
    public class ItemDePalavra
    {
        public string palavra;
        public string caminhoPrefab;  // caminho dentro de Assets/Resources (sem extensão)
        public string caminhoTextura; // opcional: textura a aplicar no modelo
        public float  escala = 0f;    // 0 = tamanho automático (recomendado)
    }

    [System.Serializable]
    public class FaseDoJogo
    {
        public string nome;
        public float  tempoPorPalavra = 20f; // segundos para soletrar cada palavra
        public List<ItemDePalavra> itens = new List<ItemDePalavra>();
    }

    [Header("Fases (vazio = usa as fases padrao do codigo)")]
    public List<FaseDoJogo> fases = new List<FaseDoJogo>();

    // Quando as fases padrão do código mudam, este número sobe e a cena
    // esquece a versão antiga que tinha memorizado
    const int VERSAO_DAS_FASES = 3;
    public int versaoDasFases = 0;

    // Música por fase: índice da faixa (0 = calma ... 4 = agitada)
    public int[] faixaMusicalPorFase = { 0, 1, 3, 4 };

    [Header("Ponto de exibicao do objeto 3D")]
    public Transform pontoDeExibicao;

    [Header("Aparencia do objeto 3D")]
    public float   tamanhoDoObjeto = 4.2f; // tamanho no mundo (auto-ajuste mede e aplica)
    public Vector3 rotacaoObjeto   = new Vector3(0f, 160f, 0f); // só gira no eixo Y

    [Header("Pontuacao e economia")]
    public int pontosPorLetra    = 0;  // letras nao dao pontos...
    public int bonusPorPalavra   = 10; // ...a PALAVRA completa da 10!
    public int custoPularLetra   = 5;
    public int custoPularPalavra = 10;

    [Header("Vidas")]
    public int vidasIniciais      = 3;
    public int pontosPorVidaExtra = 100;
    public int maximoDeVidas      = 5;

    [Header("Tempo de celebracao (s)")]
    public float tempoCelebracao = 2.5f;

    // ── Eventos ──────────────────────────────────────────────────────────────
    public System.Action<string> OnPalavraCompleta;
    public System.Action<int>    OnPontuacaoAtualizada;
    public System.Action<int>    OnVidasAtualizadas;
    public System.Action<string> OnNovaFase;
    public System.Action<int>    OnPontosGastos;  // animação "-5"/"-10" na tela
    public System.Action         OnLetraCorreta;  // flash verde (feedback visual)
    public System.Action         OnVidaPerdida;   // flash vermelho
    public System.Action         OnVidaGanha;     // animação "+1 coracao" na tela
    public System.Action         OnSemSaldo;      // tremida vermelha na pontuação

    // ── Estado interno ───────────────────────────────────────────────────────
    private List<ItemDePalavra> listaEmbaralhada;
    private int   indicePalavra = 0;
    private int   indiceLetra   = 0;
    private int   pontuacao     = 0;
    private int   vidas         = 0;
    private int   faseAtual     = 0;
    private int   proximaVidaEm = 100;
    private float tempoRestante = 0f;
    private bool  aguardandoCelebracao = false;
    private bool  fimDeJogo = false;
    private GameObject objetoAtual;

    // ── Propriedades públicas ────────────────────────────────────────────────
    public string PalavraAtual =>
        (JogoIniciado && !fimDeJogo && listaEmbaralhada != null &&
         indicePalavra < listaEmbaralhada.Count)
        ? listaEmbaralhada[indicePalavra].palavra : "";

    public int    IndiceLetraAtual => indiceLetra;
    public int    Pontuacao        => pontuacao;
    public int    Vidas            => vidas;
    public float  TempoRestante    => tempoRestante;
    public bool   Venceu           { get; private set; }
    public string NomeDaFase       =>
        (fases != null && faseAtual < fases.Count) ? fases[faseAtual].nome : "";

    public bool JogoIniciado  { get; private set; }
    public bool JogoTerminado => JogoIniciado && !aguardandoCelebracao && fimDeJogo;

    // Pausa (menu aberto no meio do jogo): congela tempo e reconhecimento
    // SEM perder pontos, vidas nem a fase
    public bool Pausado { get; private set; }
    public void Pausar()  { if (JogoIniciado && !JogoTerminado) Pausado = true; }
    public void Retomar() { Pausado = false; }

    // A letra que o jogador precisa fazer AGORA ("" fora do jogo).
    // O ControladorCamera usa isto para saber se espera um sinal parado
    // ou um MOVIMENTO (letras dinâmicas).
    public string LetraEsperada
    {
        get
        {
            if (Pausado) return ""; // pausado = reconhecimento dorme
            string p = PalavraAtual;
            return (p.Length > 0 && indiceLetra < p.Length) ? p[indiceLetra].ToString() : "";
        }
    }

    void Awake()
    {
        if (fases == null || fases.Count == 0 || versaoDasFases != VERSAO_DAS_FASES)
        {
            fases = CriarFasesPadrao();
            versaoDasFases = VERSAO_DAS_FASES;
        }
    }

    // ── Relógio da palavra ───────────────────────────────────────────────────
    void Update()
    {
        if (!JogoIniciado || Pausado || fimDeJogo || aguardandoCelebracao) return;
        if (string.IsNullOrEmpty(PalavraAtual)) return;

        tempoRestante -= Time.deltaTime;
        if (tempoRestante <= 0f)
        {
            tempoRestante = 0f;
            Debug.Log("Tempo esgotado! Perdeu uma vida.");
            PerderVida();
        }
    }

    // ── Controle do jogo (chamado pelo MenuPrincipal) ────────────────────────
    public void IniciarJogo()
    {
        if (fases == null || fases.Count == 0) fases = CriarFasesPadrao();

        faseAtual     = 0;
        pontuacao     = 0;
        vidas         = vidasIniciais;
        proximaVidaEm = pontosPorVidaExtra;
        fimDeJogo     = false;
        Venceu        = false;
        Pausado       = false;
        aguardandoCelebracao = false;
        JogoIniciado  = true;

        OnPontuacaoAtualizada?.Invoke(pontuacao);
        OnVidasAtualizadas?.Invoke(vidas);
        ComecarFase();
    }

    public void PararJogo()
    {
        StopAllCoroutines();
        aguardandoCelebracao = false;
        JogoIniciado = false;
        Pausado = false;
        if (objetoAtual != null) Destroy(objetoAtual);
    }

    void ComecarFase()
    {
        var fase = fases[faseAtual];
        listaEmbaralhada = new List<ItemDePalavra>(fase.itens);
        Embaralhar(listaEmbaralhada);
        indicePalavra = 0;
        OnNovaFase?.Invoke("FASE " + (faseAtual + 1));

        // Música fica mais agitada a cada fase
        int faixa = faixaMusicalPorFase[Mathf.Min(faseAtual, faixaMusicalPorFase.Length - 1)];
        GerenciadorDeAudio.TocarFaixa(faixa);

        ExibirItemAtual();
    }

    void Embaralhar(List<ItemDePalavra> lista)
    {
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = lista[i]; lista[i] = lista[j]; lista[j] = tmp;
        }
    }

    void ExibirItemAtual()
    {
        if (objetoAtual != null) Destroy(objetoAtual);

        // Acabaram as palavras desta fase? Vai para a próxima
        if (indicePalavra >= listaEmbaralhada.Count)
        {
            AvancarFase();
            return;
        }

        var item = listaEmbaralhada[indicePalavra];
        var prefab = Resources.Load<GameObject>(item.caminhoPrefab);
        if (prefab == null)
        {
            Debug.LogError("Modelo nao encontrado em Resources: " + item.caminhoPrefab);
            indicePalavra++;
            ExibirItemAtual();
            return;
        }

        Vector3 pos = pontoDeExibicao != null ? pontoDeExibicao.position : Vector3.zero;

        // IMPORTANTE: multiplica pela rotação ORIGINAL do modelo - muitos FBX
        // trazem uma correção de eixo embutida (ignorá-la deixava alguns
        // modelos de cabeça para baixo!)
        Quaternion rotacao = Quaternion.Euler(rotacaoObjeto) * prefab.transform.rotation;
        objetoAtual = Instantiate(prefab, pos, rotacao);

        CorrigirMateriaisParaURP(objetoAtual);                    // sem materiais rosas/brancos
        PintarPorNomeDeMaterial(objetoAtual, prefab.name);        // devolve as cores perdidas
        AplicarTexturaSeDefinida(objetoAtual, item.caminhoTextura);
        AjustarTamanhoECentro(objetoAtual, item.escala, pos);
        objetoAtual.AddComponent<Flutuar>();                      // balanço suave, como se boiasse

        // Modelo animado toca a animação em loop; estático ganha "respiração"
        bool animou = ReproduzirAnimacao.Tocar(objetoAtual, item.caminhoPrefab);
        if (!animou) objetoAtual.AddComponent<RespirarSuave>();

        indiceLetra   = 0;
        tempoRestante = fases[faseAtual].tempoPorPalavra; // reinicia a cada palavra
        Debug.Log("Palavra: [" + item.palavra + "] - " +
                  Mathf.RoundToInt(tempoRestante) + "s para soletrar");
    }

    // Materiais dos pacotes vêm no shader antigo ("Standard"), que o URP
    // não renderiza (fica rosa/branco). Troca pelo URP Lit preservando a
    // cor e a textura que o material tinha.
    static void CorrigirMateriaisParaURP(GameObject go)
    {
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) return;

        foreach (var renderizador in go.GetComponentsInChildren<Renderer>())
            foreach (var material in renderizador.materials)
            {
                if (material == null || material.shader == null) continue;
                if (material.shader.name.Contains("Universal")) continue; // já é URP

                Color   cor = material.HasProperty("_Color")   ? material.color       : Color.white;
                Texture tex = material.HasProperty("_MainTex") ? material.mainTexture : null;

                material.shader = urp;
                material.SetColor("_BaseColor", cor);
                if (tex != null) material.SetTexture("_BaseMap", tex);
            }
    }

    // O pacote de transporte foi exportado SEM as cores (todos os materiais
    // vieram cinza 0.64) - mas os NOMES dos materiais revelam a cor que o
    // artista queria ("Red", "Windows", "Wheel"...). Pintamos por nome.
    // Chave "Modelo:Material" tem prioridade sobre a chave só do material.
    static readonly Dictionary<string, Color> CORES_POR_NOME = new Dictionary<string, Color>
    {
        // Genéricos (qualquer modelo)
        { "Red",     new Color(0.85f, 0.15f, 0.15f) },
        { "White",   new Color(0.95f, 0.95f, 0.95f) },
        { "Green",   new Color(0.15f, 0.75f, 0.25f) },
        { "Yellow",  new Color(1f,    0.80f, 0.10f) },
        { "Black",   new Color(0.08f, 0.08f, 0.08f) },
        { "Grey",    new Color(0.55f, 0.55f, 0.55f) },
        { "Lights",  new Color(1f,    0.95f, 0.60f) }, // faróis
        { "Windows", new Color(0.55f, 0.80f, 0.95f) }, // vidro azulado
        { "Bumper",  new Color(0.30f, 0.30f, 0.32f) },
        { "Wheel",   new Color(0.12f, 0.12f, 0.12f) }, // pneu
        { "Details", new Color(0.40f, 0.40f, 0.42f) },
        { "Handle",  new Color(0.15f, 0.15f, 0.15f) },

        // Específicos por modelo
        { "Bicycle:Bike",          new Color(0.80f, 0.20f, 0.20f) }, // quadro vermelho
        { "Bicycle:Material.003",  new Color(0.50f, 0.50f, 0.52f) },
        { "Bus:Top",               new Color(0.95f, 0.95f, 0.95f) },
        { "Bus:Bottom",            new Color(0.20f, 0.45f, 0.85f) }, // ônibus azul
        { "Bus:Material",          new Color(0.35f, 0.35f, 0.38f) },
        { "Train:Outside",         new Color(0.75f, 0.20f, 0.20f) }, // trem vermelho
        { "Train:Top",             new Color(0.85f, 0.85f, 0.88f) },
        { "Ambulance:Material",    new Color(0.95f, 0.95f, 0.95f) },
        { "TrafficLight:TrafficLight", new Color(0.20f, 0.20f, 0.22f) }, // poste
    };

    static void PintarPorNomeDeMaterial(GameObject go, string nomeModelo)
    {
        foreach (var renderizador in go.GetComponentsInChildren<Renderer>())
            foreach (var material in renderizador.materials)
            {
                if (material == null) continue;
                string nome = material.name.Replace(" (Instance)", "").Trim();

                Color cor;
                if (CORES_POR_NOME.TryGetValue(nomeModelo + ":" + nome, out cor) ||
                    CORES_POR_NOME.TryGetValue(nome, out cor))
                {
                    material.color = cor;
                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", cor);
                }
            }
    }

    // Alguns modelos (comidas) têm a textura num arquivo separado que o
    // importador não conecta sozinho - aplicamos manualmente
    static void AplicarTexturaSeDefinida(GameObject go, string caminhoTextura)
    {
        if (string.IsNullOrEmpty(caminhoTextura)) return;

        var textura = Resources.Load<Texture2D>(caminhoTextura);
        if (textura == null)
        {
            Debug.LogWarning("Textura nao encontrada em Resources: " + caminhoTextura);
            return;
        }

        foreach (var renderizador in go.GetComponentsInChildren<Renderer>())
            foreach (var material in renderizador.materials)
            {
                material.SetTexture("_BaseMap", textura);
                material.mainTexture = textura;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", Color.white);
            }
    }

    // Auto-ajuste: mede o modelo (bounds) e o redimensiona para
    // 'tamanhoDoObjeto'; depois centraliza - muitos modelos têm a origem
    // nos "pés", o que os deixaria flutuando alto na tela
    void AjustarTamanhoECentro(GameObject go, float escalaManual, Vector3 centroDesejado)
    {
        var renderizadores = go.GetComponentsInChildren<Renderer>();

        if (escalaManual > 0f)
        {
            go.transform.localScale = Vector3.one * escalaManual;
        }
        else if (renderizadores.Length > 0)
        {
            Bounds b = renderizadores[0].bounds;
            for (int i = 1; i < renderizadores.Length; i++)
                b.Encapsulate(renderizadores[i].bounds);

            float maiorLado = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (maiorLado > 0.0001f)
                go.transform.localScale *= tamanhoDoObjeto / maiorLado;
        }

        if (renderizadores.Length > 0)
        {
            // Recalcula os bounds já na escala nova e centraliza de verdade
            Bounds b2 = renderizadores[0].bounds;
            for (int i = 1; i < renderizadores.Length; i++)
                b2.Encapsulate(renderizadores[i].bounds);
            go.transform.position += centroDesejado - b2.center;
        }
    }

    // ── Tentativa de letra (chamado pelo ControladorCamera) ──────────────────
    public bool TentarLetra(string letraFeita)
    {
        if (!JogoIniciado || Pausado || fimDeJogo || aguardandoCelebracao) return false;
        string palavra = PalavraAtual;
        if (string.IsNullOrEmpty(palavra)) return false;

        if (letraFeita != palavra[indiceLetra].ToString()) return false;

        AdicionarPontos(pontosPorLetra);
        indiceLetra++;
        Debug.Log("ACERTOU [" + letraFeita + "] +" + pontosPorLetra + " pts");

        if (indiceLetra >= palavra.Length)
        {
            AdicionarPontos(bonusPorPalavra);
            aguardandoCelebracao = true;
            GerenciadorDeAudio.TocarVitoria();
            OnPalavraCompleta?.Invoke(palavra);
            StartCoroutine(AvancarAposCelebracao());
        }
        else
        {
            GerenciadorDeAudio.TocarAcerto();
        }
        OnLetraCorreta?.Invoke(); // flash verde na tela
        return true;
    }

    IEnumerator AvancarAposCelebracao()
    {
        yield return new WaitForSeconds(tempoCelebracao);
        aguardandoCelebracao = false;
        indicePalavra++;
        ExibirItemAtual();
    }

    void AvancarFase()
    {
        faseAtual++;
        if (faseAtual >= fases.Count)
        {
            // Completou TODAS as fases: vitória!
            Venceu    = true;
            fimDeJogo = true;
            SalvarRecorde();
            if (objetoAtual != null) Destroy(objetoAtual);
            GerenciadorDeAudio.TocarMusicaVitoria(); // fanfarra completa!
            Debug.Log("VENCEU O JOGO! Pontuacao final: " + pontuacao);
            return;
        }
        GerenciadorDeAudio.TocarVitoria();
        ComecarFase();
    }

    void PerderVida()
    {
        vidas--;
        OnVidasAtualizadas?.Invoke(vidas);
        OnVidaPerdida?.Invoke(); // flash vermelho na tela
        GerenciadorDeAudio.TocarErro();

        if (vidas <= 0)
        {
            fimDeJogo = true;
            SalvarRecorde();
            if (objetoAtual != null) Destroy(objetoAtual);
            Debug.Log("Fim de jogo (acabaram as vidas). Pontuacao: " + pontuacao);
            return;
        }

        // Perdeu a vida mas o jogo segue: troca a palavra
        indicePalavra++;
        ExibirItemAtual();
    }

    // ── Ações do jogador (custam pontos!) ────────────────────────────────────

    // Pula a palavra inteira (custa pontos)
    public void PularPalavra()
    {
        if (!JogoIniciado || Pausado || fimDeJogo || aguardandoCelebracao) return;
        if (pontuacao < custoPularPalavra)
        {
            GerenciadorDeAudio.TocarErro();
            OnSemSaldo?.Invoke(); // feedback VISUAL também (público surdo!)
            Debug.Log("Pontos insuficientes para pular a palavra (" + custoPularPalavra + ")");
            return;
        }
        AdicionarPontos(-custoPularPalavra);
        OnPontosGastos?.Invoke(custoPularPalavra); // animação "-10" na tela
        GerenciadorDeAudio.TocarClique();
        StopAllCoroutines();
        aguardandoCelebracao = false;
        indicePalavra++;
        ExibirItemAtual();
    }

    // Avança para a próxima letra (custa pontos)
    public void PularLetra()
    {
        if (!JogoIniciado || Pausado || fimDeJogo || aguardandoCelebracao) return;
        if (string.IsNullOrEmpty(PalavraAtual)) return;
        if (pontuacao < custoPularLetra)
        {
            GerenciadorDeAudio.TocarErro();
            OnSemSaldo?.Invoke(); // feedback VISUAL também (público surdo!)
            Debug.Log("Pontos insuficientes para pular a letra (" + custoPularLetra + ")");
            return;
        }
        AdicionarPontos(-custoPularLetra);
        OnPontosGastos?.Invoke(custoPularLetra); // animação "-5" na tela
        GerenciadorDeAudio.TocarClique();

        if (indiceLetra < PalavraAtual.Length - 1)
        {
            indiceLetra++;
        }
        else
        {
            // Era a última letra: a palavra acaba SEM bônus nem celebração
            indicePalavra++;
            ExibirItemAtual();
        }
    }

    // ── Supercontrole do apresentador (setas do teclado, SEM custo) ──────────
    // Útil em demonstrações: navega pelas palavras sem gastar pontos.

    public void AvancarPalavraGratis()
    {
        if (!JogoIniciado || Pausado || fimDeJogo || aguardandoCelebracao) return;
        GerenciadorDeAudio.TocarClique();
        indicePalavra++;
        ExibirItemAtual();
    }

    public void VoltarPalavraGratis()
    {
        if (!JogoIniciado || Pausado || fimDeJogo || aguardandoCelebracao) return;
        if (indicePalavra == 0) return; // já está na primeira palavra da fase
        GerenciadorDeAudio.TocarClique();
        indicePalavra--;
        ExibirItemAtual();
    }

    // Volta uma letra (de graça)
    public void VoltarLetra()
    {
        if (!JogoIniciado || Pausado || fimDeJogo || aguardandoCelebracao) return;
        if (indiceLetra > 0)
        {
            GerenciadorDeAudio.TocarClique();
            indiceLetra--;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    void AdicionarPontos(int valor)
    {
        pontuacao = Mathf.Max(0, pontuacao + valor);
        OnPontuacaoAtualizada?.Invoke(pontuacao);

        // Vida extra a cada 'pontosPorVidaExtra' pontos alcançados
        while (pontuacao >= proximaVidaEm)
        {
            proximaVidaEm += pontosPorVidaExtra;
            if (vidas < maximoDeVidas)
            {
                vidas++;
                OnVidasAtualizadas?.Invoke(vidas);
                OnVidaGanha?.Invoke(); // animação "+1 coração"
                GerenciadorDeAudio.TocarVidaExtra();
                Debug.Log("VIDA EXTRA! Vidas: " + vidas);
            }
        }
    }

    // Guarda o recorde entre sessões (mostrado no menu)
    void SalvarRecorde()
    {
        int recorde = PlayerPrefs.GetInt("recorde", 0);
        if (pontuacao > recorde)
        {
            PlayerPrefs.SetInt("recorde", pontuacao);
            Debug.Log("NOVO RECORDE: " + pontuacao);
        }
    }

    // ── As fases padrão do jogo ──────────────────────────────────────────────
    static List<FaseDoJogo> CriarFasesPadrao()
    {
        ItemDePalavra Item(string palavra, string caminho, string textura = null) =>
            new ItemDePalavra { palavra = palavra, caminhoPrefab = caminho,
                                caminhoTextura = textura };

        const string FRUTAS     = "Low Poly Fruits/Prefabs/";
        const string FAZENDA    = "Farm Animals by @Quaternius/FBX/";
        const string SELVAGEM   = "Ultimate Animated Animals - July 2021/FBX/";
        // Transporte usa os OBJ: as CORES dos veículos vêm nos materiais .mtl
        // (a exportação FBX deste pacote perde as cores)
        const string TRANSPORTE = "Public Transport Pack - Feb 2017/OBJ/";
        const string COMIDA     = "Junk Food Pack - Apr 2017/FBX/";
        const string TEXTURAS   = "Junk Food Pack - Apr 2017/Blender/Textures/";

        return new List<FaseDoJogo>
        {
            new FaseDoJogo
            {
                nome = "FRUTAS", tempoPorPalavra = 25f,
                itens = new List<ItemDePalavra>
                {
                    Item("MAÇA",     FRUTAS + "apple"),
                    Item("BANANA",   FRUTAS + "banana"),
                    Item("ABACATE",  FRUTAS + "avocado"),
                    Item("LIMAO",    FRUTAS + "lemon"),
                    Item("PERA",     FRUTAS + "pear"),
                    Item("PESSEGO",  FRUTAS + "peach"),
                    Item("MELANCIA", FRUTAS + "watermelon"),
                    Item("MORANGO",  FRUTAS + "strawberry"),
                    Item("AMENDOIM", FRUTAS + "peanut"),
                    Item("CEREJA",   FRUTAS + "cherries"),
                }
            },
            new FaseDoJogo
            {
                nome = "ANIMAIS", tempoPorPalavra = 20f,
                itens = new List<ItemDePalavra>
                {
                    // Animados (pacote Ultimate): VACA, CAVALO e LHAMA trocados
                    // pelas versões COM animação
                    Item("VACA",     SELVAGEM + "Cow"),
                    Item("CAVALO",   SELVAGEM + "Horse"),
                    Item("LHAMA",    SELVAGEM + "Alpaca"),
                    Item("TOURO",    SELVAGEM + "Bull"),
                    Item("CERVO",    SELVAGEM + "Deer"),
                    Item("BURRO",    SELVAGEM + "Donkey"),
                    Item("RAPOSA",   SELVAGEM + "Fox"),
                    Item("LOBO",     SELVAGEM + "Wolf"),
                    Item("CACHORRO", SELVAGEM + "Husky"),
                    // Estáticos (Farm, sem esqueleto): ganham "respiração"
                    Item("PORCO",    FAZENDA  + "Pig"),
                    Item("OVELHA",   FAZENDA  + "Sheep"),
                    Item("ZEBRA",    FAZENDA  + "Zebra"),
                }
            },
            new FaseDoJogo
            {
                nome = "TRANSPORTE", tempoPorPalavra = 15f,
                itens = new List<ItemDePalavra>
                {
                    Item("ONIBUS",     TRANSPORTE + "Bus"),
                    Item("TREM",       TRANSPORTE + "Train"),
                    Item("TAXI",       TRANSPORTE + "Taxi",
                         "Public Transport Pack - Feb 2017/Blends/TaxiTexture"),
                    Item("AMBULANCIA", TRANSPORTE + "Ambulance"),
                    Item("BICICLETA",  TRANSPORTE + "Bicycle"),
                    Item("SEMAFORO",   TRANSPORTE + "TrafficLight"),
                }
            },
            new FaseDoJogo
            {
                nome = "COMIDAS", tempoPorPalavra = 10f,
                itens = new List<ItemDePalavra>
                {
                    Item("PIZZA",        COMIDA + "Pizza",    TEXTURAS + "PizzaTexture"),
                    Item("BOLO",         COMIDA + "Cake",     TEXTURAS + "CakeTexture"),
                    Item("BISCOITO",     COMIDA + "Cookie",   TEXTURAS + "CookieTexture"),
                    Item("SORVETE",      COMIDA + "Icecream", TEXTURAS + "Icecream"),
                    Item("ROSQUINHA",    COMIDA + "Donut",    TEXTURAS + "DonutTexture"),
                    Item("HAMBURGUER",   COMIDA + "Burger",   TEXTURAS + "BurgerTexture"),
                    Item("REFRIGERANTE", COMIDA + "Soda",     TEXTURAS + "SodaTexture"),
                }
            },
        };
    }
}
