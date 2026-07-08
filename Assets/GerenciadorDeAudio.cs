using UnityEngine;

// GerenciadorDeAudio: todos os sons do jogo são GERADOS POR CÓDIGO (síntese de
// ondas senoidais) — não precisa de nenhum arquivo de áudio no projeto.
// Também não precisa colocar nada na cena: ele se cria sozinho na primeira
// vez que algum script chama GerenciadorDeAudio.TocarClique() etc.
public class GerenciadorDeAudio : MonoBehaviour
{
    static GerenciadorDeAudio instancia;
    const int TAXA = 44100; // amostras de áudio por segundo (qualidade CD)

    AudioSource fonteSfx;    // efeitos sonoros
    AudioSource fonteMusica; // música de fundo

    AudioClip somClique;
    AudioClip somAcerto;
    AudioClip somVitoria;
    AudioClip somErro;

    // Várias músicas tocando em rodízio (uma termina, entra a próxima)
    readonly System.Collections.Generic.List<AudioClip> musicas =
        new System.Collections.Generic.List<AudioClip>();
    AudioClip musicaVitoria;
    int  musicaAtual  = 0;
    int  faixaFixa    = -1; // >= 0: repete sempre a mesma faixa (música da fase)
    bool musicaLigada = true;

    [Range(0f, 1f)] public float volumeSfx    = 0.9f;
    [Range(0f, 1f)] public float volumeMusica = 0.3f;

    // ── Acesso global (cria o objeto na primeira chamada) ────────────────────
    static GerenciadorDeAudio Obter()
    {
        if (instancia == null)
        {
            var go = new GameObject("GerenciadorDeAudio");
            instancia = go.AddComponent<GerenciadorDeAudio>();
        }
        return instancia;
    }

    void Awake()
    {
        instancia = this;

        fonteSfx = gameObject.AddComponent<AudioSource>();
        fonteSfx.playOnAwake = false;

        fonteMusica = gameObject.AddComponent<AudioSource>();
        fonteMusica.playOnAwake = false;
        fonteMusica.loop = false; // sem loop: quando acaba, o Update troca de faixa

        // Frequências em Hz: C5=523.25  E5=659.25  G5=783.99  C6=1046.5
        somClique  = ClipDeNotas(new[] { 880f },                            0.07f, 0.05f);
        somAcerto  = ClipDeNotas(new[] { 523.25f, 783.99f },                0.09f, 0.18f);
        somVitoria = ClipDeNotas(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f, 0.4f);
        somErro    = ClipDeNotas(new[] { 196f, 130.81f },                   0.16f, 0.2f); // descendo = "ops!"

        GerarMusicas();
        fonteMusica.volume = volumeMusica;

        // Lembra a escolha do jogador entre sessões (1 = ligada)
        musicaLigada = PlayerPrefs.GetInt("musicaLigada", 1) == 1;
    }

    // Quando uma faixa termina: repete a faixa da fase (se fixada) ou rodízio
    void Update()
    {
        if (musicaLigada && !fonteMusica.isPlaying && musicas.Count > 0)
        {
            musicaAtual = (faixaFixa >= 0 && faixaFixa < musicas.Count)
                          ? faixaFixa
                          : (musicaAtual + 1) % musicas.Count;
            fonteMusica.clip = musicas[musicaAtual];
            fonteMusica.Play();
        }
    }

    // ── Métodos públicos (chame de qualquer script) ──────────────────────────
    public static void TocarClique()  { var g = Obter(); g.fonteSfx.PlayOneShot(g.somClique,  g.volumeSfx); }
    public static void TocarAcerto()  { var g = Obter(); g.fonteSfx.PlayOneShot(g.somAcerto,  g.volumeSfx); }
    public static void TocarVitoria() { var g = Obter(); g.fonteSfx.PlayOneShot(g.somVitoria, g.volumeSfx); }
    public static void TocarErro()    { var g = Obter(); g.fonteSfx.PlayOneShot(g.somErro,    g.volumeSfx); }

    public static bool MusicaLigada => Obter().musicaLigada;

    // Liga/desliga a música de fundo (o botão de som chama isto)
    public static void AlternarMusica()
    {
        var g = Obter();
        g.musicaLigada = !g.musicaLigada;
        PlayerPrefs.SetInt("musicaLigada", g.musicaLigada ? 1 : 0);
        if (g.musicaLigada) g.fonteMusica.Play();
        else                g.fonteMusica.Pause();
    }

    public static void TocarMusica()
    {
        var g = Obter();
        if (g.musicaLigada && !g.fonteMusica.isPlaying && g.musicas.Count > 0)
        {
            g.fonteMusica.clip = g.musicas[g.musicaAtual];
            g.fonteMusica.Play();
        }
    }

    public static void PararMusica() { Obter().fonteMusica.Stop(); }

    // Fixa uma faixa (0 = mais calma ... 4 = mais agitada) — usada pelas
    // fases do jogo para acelerar a música conforme a dificuldade sobe.
    // Passe -1 para voltar ao rodízio livre.
    public static void TocarFaixa(int indice)
    {
        var g = Obter();
        g.faixaFixa = indice;
        if (indice < 0 || indice >= g.musicas.Count) return;
        g.musicaAtual = indice;
        if (g.musicaLigada)
        {
            g.fonteMusica.clip = g.musicas[indice];
            g.fonteMusica.Play();
        }
    }

    // Fanfarra de vitória (toca uma vez; depois volta a faixa atual)
    public static void TocarMusicaVitoria()
    {
        var g = Obter();
        if (!g.musicaLigada) return;
        g.fonteMusica.clip = g.musicaVitoria;
        g.fonteMusica.Play();
    }

    // ── Síntese de som ───────────────────────────────────────────────────────

    // Cria um clipe tocando as notas em sequência (uma após a outra)
    AudioClip ClipDeNotas(float[] frequencias, float duracaoNota, float cauda)
    {
        int totalAmostras = Mathf.CeilToInt((frequencias.Length * duracaoNota + cauda) * TAXA);
        float[] dados = new float[totalAmostras];

        for (int n = 0; n < frequencias.Length; n++)
            EscreverNota(dados, frequencias[n], n * duracaoNota, duracaoNota + cauda, 0.5f);

        return CriarClip("sfx", dados);
    }

    // Escreve uma nota no buffer: senoide com ataque rápido e decaimento suave
    // (parecido com uma nota de xilofone/marimba)
    void EscreverNota(float[] dados, float freq, float inicio, float duracao, float volume)
    {
        int primeiraAmostra = Mathf.FloorToInt(inicio * TAXA);
        int totalAmostras   = Mathf.FloorToInt(duracao * TAXA);

        for (int i = 0; i < totalAmostras && primeiraAmostra + i < dados.Length; i++)
        {
            float t          = (float)i / TAXA;
            float ataque     = Mathf.Clamp01(t / 0.008f);           // evita "clique" no início
            float decaimento = Mathf.Exp(-4.5f * t / duracao);      // volume cai suavemente
            float amostra    = Mathf.Sin(2f * Mathf.PI * freq * t)
                             + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t); // harmônico: timbre mais rico

            dados[primeiraAmostra + i] += amostra * ataque * decaimento * volume * 0.5f;
        }
    }

    AudioClip CriarClip(string nome, float[] dados)
    {
        // Limita o volume para não estourar quando notas se sobrepõem
        for (int i = 0; i < dados.Length; i++)
            dados[i] = Mathf.Clamp(dados[i], -1f, 1f);

        var clip = AudioClip.Create(nome, dados.Length, 1, TAXA, false);
        clip.SetData(dados, 0);
        return clip;
    }

    // Gera as músicas de fundo. Cada número é a frequência da nota em Hz;
    // 0 = pausa. Notas usadas (escala pentatônica, sempre soa bem):
    // C4=261.63 D4=293.66 E4=329.63 G4=392 A4=440 C5=523.25 D5=587.33 E5=659.25
    void GerarMusicas()
    {
        // Faixa 1: calma, a original
        musicas.Add(RenderizarMusica(0.3f, new[]
        {
            523.25f, 0, 392f,    0, 440f,    0, 392f, 0,
            329.63f, 0, 392f,    0, 440f,    0, 0,    0,
            523.25f, 0, 587.33f, 0, 523.25f, 0, 440f, 0,
            392f,    0, 329.63f, 0, 293.66f, 0, 0,    0,
            329.63f, 392f, 440f, 0, 523.25f, 0, 440f, 0,
            392f,    0, 329.63f, 0, 261.63f, 0, 0,    0,
        },
        new[] { 130.81f, 98f, 110f, 98f, 130.81f, 98f }));

        // Faixa 2: mais animada (arpejos subindo, andamento mais rápido)
        musicas.Add(RenderizarMusica(0.24f, new[]
        {
            261.63f, 329.63f, 392f, 523.25f, 392f, 329.63f, 261.63f, 0,
            293.66f, 392f, 440f, 587.33f, 440f, 392f, 293.66f, 0,
            329.63f, 392f, 523.25f, 659.25f, 523.25f, 392f, 329.63f, 0,
            261.63f, 329.63f, 392f, 440f, 523.25f, 0, 523.25f, 0,
        },
        new[] { 130.81f, 110f, 98f, 130.81f }));

        // Faixa 3: bem suave, tipo canção de ninar (notas longas)
        musicas.Add(RenderizarMusica(0.4f, new[]
        {
            440f, 0, 0, 523.25f, 0, 0, 440f, 0,
            392f, 0, 0, 329.63f, 0, 0, 0,    0,
            440f, 0, 0, 587.33f, 0, 0, 523.25f, 0,
            440f, 0, 0, 392f,    0, 0, 0,    0,
        },
        new[] { 110f, 98f, 110f, 130.81f }));

        // Faixa 4: BEM animada! Andamento rápido, melodia saltitante
        musicas.Add(RenderizarMusica(0.17f, new[]
        {
            523.25f, 523.25f, 0, 659.25f, 0, 523.25f, 783.99f, 0,
            659.25f, 0, 523.25f, 0, 440f, 440f, 523.25f, 0,
            587.33f, 587.33f, 0, 659.25f, 0, 587.33f, 523.25f, 0,
            440f, 0, 392f, 0, 440f, 523.25f, 0, 0,
            659.25f, 659.25f, 0, 783.99f, 0, 659.25f, 1046.5f, 0,
            783.99f, 0, 659.25f, 0, 587.33f, 523.25f, 0, 0,
        },
        new[] { 130.81f, 130.81f, 98f, 110f, 130.81f, 98f }));

        // Faixa 5: festiva, com saltos de oitava (energia de festa junina!)
        musicas.Add(RenderizarMusica(0.2f, new[]
        {
            261.63f, 523.25f, 261.63f, 523.25f, 329.63f, 659.25f, 0, 0,
            293.66f, 587.33f, 293.66f, 587.33f, 392f, 783.99f, 0, 0,
            440f, 523.25f, 587.33f, 659.25f, 587.33f, 523.25f, 440f, 0,
            392f, 440f, 392f, 329.63f, 261.63f, 0, 261.63f, 0,
        },
        new[] { 130.81f, 110f, 98f, 130.81f }));

        // Fanfarra de vitória: escala subindo + acordes triunfais
        musicaVitoria = RenderizarMusica(0.16f, new[]
        {
            523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f, 0, 1046.5f,
            0, 1046.5f, 0, 0, 783.99f, 880f, 1046.5f, 0,
            1046.5f, 0, 880f, 0, 1046.5f, 0, 0, 0,
            523.25f, 659.25f, 783.99f, 1046.5f, 0, 1046.5f, 1046.5f, 0,
        },
        new[] { 130.81f, 98f, 130.81f, 130.81f });
    }

    // Transforma um padrão de notas + baixo num clipe de áudio
    AudioClip RenderizarMusica(float passo, float[] melodia, float[] baixo)
    {
        int celulasPorBaixo = Mathf.Max(1, melodia.Length / baixo.Length);
        int totalAmostras = Mathf.CeilToInt(melodia.Length * passo * TAXA);
        float[] dados = new float[totalAmostras];

        for (int i = 0; i < melodia.Length; i++)
            if (melodia[i] > 0f)
                EscreverNota(dados, melodia[i], i * passo, passo * 3.2f, 0.22f);

        for (int b = 0; b < baixo.Length; b++)
            EscreverNota(dados, baixo[b], b * celulasPorBaixo * passo,
                         passo * celulasPorBaixo * 0.9f, 0.16f);

        return CriarClip("musica", dados);
    }
}
