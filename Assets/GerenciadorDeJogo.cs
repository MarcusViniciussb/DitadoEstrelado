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
        public string caminhoPrefab; // caminho dentro de Assets/Resources (sem extensão)
        public float  escala = 0f;   // 0 = tamanho automático (recomendado)
    }

    [System.Serializable]
    public class FaseDoJogo
    {
        public string nome;
        public float  segundosPorLetra = 10f; // tempo da palavra = isto × nº de letras
        public List<ItemDePalavra> itens = new List<ItemDePalavra>();
    }

    [Header("Fases (vazio = usa as fases padrao do codigo)")]
    public List<FaseDoJogo> fases = new List<FaseDoJogo>();

    [Header("Ponto de exibicao do objeto 3D")]
    public Transform pontoDeExibicao;

    [Header("Aparencia do objeto 3D")]
    public float   tamanhoDoObjeto = 4.2f; // tamanho no mundo (auto-ajuste mede e aplica)
    public Vector3 rotacaoObjeto   = new Vector3(-20f, 160f, 0f);

    [Header("Pontuacao e economia")]
    public int pontosPorLetra    = 10;
    public int bonusPorPalavra   = 50;
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

    // A letra que o jogador precisa fazer AGORA ("" fora do jogo).
    // O ControladorCamera usa isto para saber se espera um sinal parado
    // ou um MOVIMENTO (letras dinâmicas).
    public string LetraEsperada
    {
        get
        {
            string p = PalavraAtual;
            return (p.Length > 0 && indiceLetra < p.Length) ? p[indiceLetra].ToString() : "";
        }
    }

    void Awake()
    {
        if (fases == null || fases.Count == 0)
            fases = CriarFasesPadrao();
    }

    // ── Relógio da palavra ───────────────────────────────────────────────────
    void Update()
    {
        if (!JogoIniciado || fimDeJogo || aguardandoCelebracao) return;
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
        if (objetoAtual != null) Destroy(objetoAtual);
    }

    void ComecarFase()
    {
        var fase = fases[faseAtual];
        listaEmbaralhada = new List<ItemDePalavra>(fase.itens);
        Embaralhar(listaEmbaralhada);
        indicePalavra = 0;
        OnNovaFase?.Invoke("FASE " + (faseAtual + 1) + "\n" + fase.nome);
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
        objetoAtual = Instantiate(prefab, pos, Quaternion.Euler(rotacaoObjeto));
        AjustarTamanhoECentro(objetoAtual, item.escala, pos);
        objetoAtual.AddComponent<Flutuar>(); // balanço suave, como se boiasse

        indiceLetra   = 0;
        tempoRestante = Mathf.Max(12f, fases[faseAtual].segundosPorLetra * item.palavra.Length);
        Debug.Log("Palavra: [" + item.palavra + "] — " +
                  Mathf.RoundToInt(tempoRestante) + "s para soletrar");
    }

    // Auto-ajuste: mede o modelo (bounds) e o redimensiona para
    // 'tamanhoDoObjeto'; depois centraliza — muitos modelos têm a origem
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
        if (!JogoIniciado || fimDeJogo || aguardandoCelebracao) return false;
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
            if (objetoAtual != null) Destroy(objetoAtual);
            GerenciadorDeAudio.TocarVitoria();
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
        GerenciadorDeAudio.TocarErro();

        if (vidas <= 0)
        {
            fimDeJogo = true;
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
        if (!JogoIniciado || fimDeJogo || aguardandoCelebracao) return;
        if (pontuacao < custoPularPalavra)
        {
            GerenciadorDeAudio.TocarErro();
            Debug.Log("Pontos insuficientes para pular a palavra (" + custoPularPalavra + ")");
            return;
        }
        AdicionarPontos(-custoPularPalavra);
        GerenciadorDeAudio.TocarClique();
        StopAllCoroutines();
        aguardandoCelebracao = false;
        indicePalavra++;
        ExibirItemAtual();
    }

    // Avança para a próxima letra (custa pontos)
    public void PularLetra()
    {
        if (!JogoIniciado || fimDeJogo || aguardandoCelebracao) return;
        if (string.IsNullOrEmpty(PalavraAtual)) return;
        if (pontuacao < custoPularLetra)
        {
            GerenciadorDeAudio.TocarErro();
            Debug.Log("Pontos insuficientes para pular a letra (" + custoPularLetra + ")");
            return;
        }
        AdicionarPontos(-custoPularLetra);
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

    // Volta uma letra (de graça)
    public void VoltarLetra()
    {
        if (!JogoIniciado || fimDeJogo || aguardandoCelebracao) return;
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
                Debug.Log("VIDA EXTRA! Vidas: " + vidas);
            }
        }
    }

    // ── As fases padrão do jogo ──────────────────────────────────────────────
    static List<FaseDoJogo> CriarFasesPadrao()
    {
        ItemDePalavra Item(string palavra, string caminho) =>
            new ItemDePalavra { palavra = palavra, caminhoPrefab = caminho };

        const string FRUTAS     = "Low Poly Fruits/Prefabs/";
        const string FAZENDA    = "Farm Animals by @Quaternius/FBX/";
        const string SELVAGEM   = "Ultimate Animated Animals - July 2021/FBX/";
        const string TRANSPORTE = "Public Transport Pack - Feb 2017/FBX/";
        const string COMIDA     = "Junk Food Pack - Apr 2017/FBX/";

        return new List<FaseDoJogo>
        {
            new FaseDoJogo
            {
                nome = "FRUTAS", segundosPorLetra = 12f,
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
                nome = "ANIMAIS", segundosPorLetra = 10f,
                itens = new List<ItemDePalavra>
                {
                    Item("VACA",     FAZENDA  + "Cow"),
                    Item("PORCO",    FAZENDA  + "Pig"),
                    Item("OVELHA",   FAZENDA  + "Sheep"),
                    Item("CAVALO",   FAZENDA  + "Horse"),
                    Item("ZEBRA",    FAZENDA  + "Zebra"),
                    Item("LHAMA",    FAZENDA  + "Llama"),
                    Item("TOURO",    SELVAGEM + "Bull"),
                    Item("CERVO",    SELVAGEM + "Deer"),
                    Item("BURRO",    SELVAGEM + "Donkey"),
                    Item("RAPOSA",   SELVAGEM + "Fox"),
                    Item("LOBO",     SELVAGEM + "Wolf"),
                    Item("CACHORRO", SELVAGEM + "Husky"),
                }
            },
            new FaseDoJogo
            {
                nome = "TRANSPORTE", segundosPorLetra = 9f,
                itens = new List<ItemDePalavra>
                {
                    Item("ONIBUS",     TRANSPORTE + "Bus"),
                    Item("TREM",       TRANSPORTE + "Train"),
                    Item("TAXI",       TRANSPORTE + "Taxi"),
                    Item("AMBULANCIA", TRANSPORTE + "Ambulance"),
                    Item("BICICLETA",  TRANSPORTE + "Bicycle"),
                    Item("SEMAFORO",   TRANSPORTE + "TrafficLight"),
                }
            },
            new FaseDoJogo
            {
                nome = "COMIDAS", segundosPorLetra = 8f,
                itens = new List<ItemDePalavra>
                {
                    Item("PIZZA",        COMIDA + "Pizza"),
                    Item("BOLO",         COMIDA + "Cake"),
                    Item("BISCOITO",     COMIDA + "Cookie"),
                    Item("SORVETE",      COMIDA + "Icecream"),
                    Item("ROSQUINHA",    COMIDA + "Donut"),
                    Item("HAMBURGUER",   COMIDA + "Burger"),
                    Item("REFRIGERANTE", COMIDA + "Soda"),
                }
            },
        };
    }
}
