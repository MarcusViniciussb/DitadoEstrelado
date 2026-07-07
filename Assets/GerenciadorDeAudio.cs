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
    AudioSource fonteMusica; // música de fundo em loop

    AudioClip somClique;
    AudioClip somAcerto;
    AudioClip somVitoria;

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
        fonteMusica.loop = true;

        // Frequências em Hz: C5=523.25  E5=659.25  G5=783.99  C6=1046.5
        somClique  = ClipDeNotas(new[] { 880f },                            0.07f, 0.05f);
        somAcerto  = ClipDeNotas(new[] { 523.25f, 783.99f },                0.09f, 0.18f);
        somVitoria = ClipDeNotas(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f, 0.4f);

        fonteMusica.clip   = GerarMusica();
        fonteMusica.volume = volumeMusica;
    }

    // ── Métodos públicos (chame de qualquer script) ──────────────────────────
    public static void TocarClique()  { var g = Obter(); g.fonteSfx.PlayOneShot(g.somClique,  g.volumeSfx); }
    public static void TocarAcerto()  { var g = Obter(); g.fonteSfx.PlayOneShot(g.somAcerto,  g.volumeSfx); }
    public static void TocarVitoria() { var g = Obter(); g.fonteSfx.PlayOneShot(g.somVitoria, g.volumeSfx); }

    public static void TocarMusica()
    {
        var g = Obter();
        if (!g.fonteMusica.isPlaying) g.fonteMusica.Play();
    }

    public static void PararMusica() { Obter().fonteMusica.Stop(); }

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

    // Música de fundo: melodia calma na escala pentatônica de Dó, em loop.
    // Cada número é a frequência da nota em Hz; 0 = pausa.
    AudioClip GerarMusica()
    {
        const float passo = 0.3f; // duração de cada célula rítmica (s)

        // C4=261.63 D4=293.66 E4=329.63 G4=392 A4=440 C5=523.25 D5=587.33
        float[] melodia =
        {
            523.25f, 0, 392f,    0, 440f,    0, 392f, 0,
            329.63f, 0, 392f,    0, 440f,    0, 0,    0,
            523.25f, 0, 587.33f, 0, 523.25f, 0, 440f, 0,
            392f,    0, 329.63f, 0, 293.66f, 0, 0,    0,
            329.63f, 392f, 440f, 0, 523.25f, 0, 440f, 0,
            392f,    0, 329.63f, 0, 261.63f, 0, 0,    0,
        };

        // Uma nota grave a cada 8 células: C3=130.81 G2=98 A2=110
        float[] baixo = { 130.81f, 98f, 110f, 98f, 130.81f, 98f };

        int totalAmostras = Mathf.CeilToInt(melodia.Length * passo * TAXA);
        float[] dados = new float[totalAmostras];

        for (int i = 0; i < melodia.Length; i++)
            if (melodia[i] > 0f)
                EscreverNota(dados, melodia[i], i * passo, passo * 3.2f, 0.22f);

        for (int b = 0; b < baixo.Length; b++)
            EscreverNota(dados, baixo[b], b * 8 * passo, passo * 7f, 0.16f);

        return CriarClip("musica", dados);
    }
}
