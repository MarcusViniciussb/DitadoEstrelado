using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ReconhecedorLibras : MonoBehaviour
{
    [Header("Conecte o 'MeuAlfabeto' aqui!")]
    public AlfabetoData bancoDeDados;

    [Header("Aceitacao do sinal")]
    // Limite de seguranca: acima disso a mao nao parece com NADA do banco.
    // Nao e mais o criterio principal - so evita aceitar uma mao qualquer.
    public float limiteDeSeguranca = 12f;

    // Criterio principal, e RELATIVO: a letra vencedora precisa estar este
    // tanto mais perto do que a segunda colocada. Como compara duas distancias
    // entre si, aguenta o dia ruim - quando a leitura piora, todas as
    // distancias sobem juntas e a comparacao continua valendo. O limite fixo
    // antigo simplesmente recusava tudo nessa situacao.
    [Range(0.5f, 1f)] public float vantagemNecessaria = 0.95f;

    // Abaixo desta distancia o sinal e obvio e o jogo aceita mais rapido
    public float distanciaDeCerteza = 3f;

    [Header("Giro da mao")]
    // Endireita a palma antes de comparar, ate este limite. Absorve a
    // inclinacao natural de quem sinaliza sem confundir letras que se
    // distinguem justamente PELA orientacao: D e G tem quase a mesma forma,
    // giradas cerca de 90 graus uma da outra, e por isso o giro e limitado.
    [Range(0f, 90f)] public float giroMaximoCorrigido = 40f;

    [Header("Classificacao kNN")]
    [Range(1, 9)]   public int   vizinhosK      = 5;     // quantas amostras próximas votam
    [Range(0f, 3f)] public float pesoDosAngulos = 0.25f; // importância dos ângulos vs posições

    // ── Compatibilidade do celular (nao afeta o computador) ─────────────────
    //
    // O banco de sinais foi gravado no computador, com o rastreador completo:
    // quadro 4:3 e com profundidade. No celular o quadro fica em pe e o
    // rastreador nao fornece profundidade, entao os pontos precisam ser
    // levados ao mesmo formato do banco antes de comparar.
    //
    // Em qualquer plataforma que nao seja o celular, os metodos abaixo
    // devolvem o ponto sem tocar em nada: o computador roda exatamente o
    // mesmo caminho de antes desta compatibilizacao existir.
    const float ASPECTO_DO_BANCO = 4f / 3f;

    [HideInInspector] public float aspectoDaCamera = ASPECTO_DO_BANCO;
    [HideInInspector] public bool  temProfundidade = true;

    Vector3 DaCamera(Vector3 p)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // A profundidade tem as mesmas unidades do eixo X, entao recebe o
        // mesmo fator. Sem isso ela ficaria com peso exagerado e passaria a
        // atrapalhar a comparacao em vez de ajudar.
        float fator = aspectoDaCamera / ASPECTO_DO_BANCO;
        return new Vector3(p.x * fator, p.y, temProfundidade ? p.z * fator : 0f);
#else
        return p;
#endif
    }

    Vector3 DoBanco(Vector3 p)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return temProfundidade ? p : new Vector3(p.x, p.y, 0f);
#else
        return p;
#endif
    }

    [Header("Letras dinamicas (gravadas como MOVIMENTO, nao como foto)")]
    // (campo renomeado para o Unity aplicar a lista nova - W e Ç também se movem)
    public string[] letrasComMovimento = { "H", "J", "K", "W", "X", "Z", "Ç" };
    // Mesmo criterio das letras paradas: limite de seguranca largo + a folga
    // para a rival e que decide (ver Aceita)
    public float limiteDinamicoDeSeguranca = 9f;

    public bool EhLetraDinamica(string letra)
    {
        foreach (var l in letrasComMovimento)
            if (l == letra) return true;
        return false;
    }

    // ── Ângulos das articulações ─────────────────────────────────────────────
    // Cada trio (a, b, c) mede o ângulo NO ponto b entre os vetores (a-b) e (c-b).
    // Ângulo não muda quando a mão gira ou muda de tamanho -> complementa as posições.
    private static readonly int[,] TRIOS =
    {
        // Polegar
        {0,1,2},   {1,2,3},   {2,3,4},
        // Indicador
        {0,5,6},   {5,6,7},   {6,7,8},
        // Médio
        {0,9,10},  {9,10,11}, {10,11,12},
        // Anelar
        {0,13,14}, {13,14,15},{14,15,16},
        // Mínimo
        {0,17,18}, {17,18,19},{18,19,20},
    };

    // Abertura entre dedos vizinhos: ângulo entre os vetores pulso->base de cada dedo
    private static readonly int[,] PARES_ABERTURA =
    {
        {1,5}, {5,9}, {9,13}, {13,17}
    };

    // Extrai todos os ângulos (em radianos) de uma mão
    static float[] ExtrairAngulos(Vector3[] p)
    {
        int nTrios = TRIOS.GetLength(0);
        int nPares = PARES_ABERTURA.GetLength(0);
        float[] angulos = new float[nTrios + nPares];

        for (int i = 0; i < nTrios; i++)
        {
            Vector3 v1 = p[TRIOS[i, 0]] - p[TRIOS[i, 1]];
            Vector3 v2 = p[TRIOS[i, 2]] - p[TRIOS[i, 1]];
            angulos[i] = Vector3.Angle(v1, v2) * Mathf.Deg2Rad;
        }
        for (int i = 0; i < nPares; i++)
        {
            Vector3 v1 = p[PARES_ABERTURA[i, 0]] - p[0];
            Vector3 v2 = p[PARES_ABERTURA[i, 1]] - p[0];
            angulos[nTrios + i] = Vector3.Angle(v1, v2) * Mathf.Deg2Rad;
        }
        return angulos;
    }

    // Distância combinada: formato (posições) + curvatura dos dedos (ângulos)
    float DistanciaEntre(Vector3[] posA, float[] angA, Vector3[] posB, float[] angB)
    {
        float distPosicoes = 0f;
        for (int i = 0; i < 21; i++)
            distPosicoes += Vector3.Distance(posA[i], posB[i]);

        float distAngulos = 0f;
        for (int i = 0; i < angA.Length; i++)
            distAngulos += Mathf.Abs(angA[i] - angB[i]);

        return distPosicoes + pesoDosAngulos * distAngulos;
    }

    [Header("Liga o log 'Mais parecido: X (distancia Y)' no Console")]
    public bool mostrarDebug = false;
    private float tempoUltimoDebug = 0f;

    // Distância da última classificação - quanto MENOR, mais parecido o sinal
    // está com o gravado (o ControladorCamera usa para aceitar mais rápido)
    public float UltimaDistancia { get; private set; } = float.MaxValue;

    // Letra mais parecida da ultima comparacao, mesmo quando recusada.
    // Serve para descobrir, no proprio aparelho, se o problema esta em
    // reconhecer a letra errada ou em recusar a letra certa.
    public string UltimaLetra { get; private set; } = "-";

    // Resumo tipo "A: 7   B: 5   C: 4" - mostrado na tela de treinamento
    public string ResumoDoBanco()
    {
        if (bancoDeDados == null || bancoDeDados.letrasGravadas.Count == 0)
            return "Nenhuma letra gravada ainda.";

        var contagem = new SortedDictionary<string, int>();
        foreach (var l in bancoDeDados.letrasGravadas)
            contagem[l.nome] = contagem.ContainsKey(l.nome) ? contagem[l.nome] + 1 : 1;

        // Letras dinâmicas ganham um * para diferenciar (ex: "H*: 3")
        foreach (var s in bancoDeDados.sinaisDinamicos)
        {
            string chave = s.nome + "*";
            contagem[chave] = contagem.ContainsKey(chave) ? contagem[chave] + 1 : 1;
        }

        // Monta em linhas de 6 itens para caber legível no cartão
        var sb = new System.Text.StringBuilder();
        int itensNaLinha = 0;
        foreach (var par in contagem)
        {
            sb.Append(par.Key).Append(": ").Append(par.Value);
            itensNaLinha++;
            if (itensNaLinha % 6 == 0) sb.Append('\n');
            else                       sb.Append("    ");
        }
        return sb.ToString().TrimEnd();
    }

    // Normaliza pontos (relativos ao pulso) pelo TAMANHO da mão - a distância
    // do pulso até a base do dedo médio (ponto 9). Assim a mesma letra é
    // reconhecida perto OU longe da câmera, pois a escala deixa de importar.
    // Os dados já gravados continuam válidos: eles também passam por aqui.
    // Endireita a palma: gira os pontos para que o eixo pulso -> base do dedo
    // médio aponte para cima, mas nunca mais que 'giroMaximoCorrigido'.
    // Sem isso, inclinar a mão 20 graus já era suficiente para o sistema
    // deixar de reconhecer a letra.
    Vector3[] EndireitarPalma(Vector3[] relativosAoPulso)
    {
        if (giroMaximoCorrigido <= 0f) return relativosAoPulso;

        Vector2 eixo = new Vector2(relativosAoPulso[9].x, relativosAoPulso[9].y);
        if (eixo.sqrMagnitude < 1e-10f) return relativosAoPulso;

        // Quanto a palma está torta em relação ao "para cima" (0 = já está em pé)
        float desvio = Mathf.Atan2(eixo.x, eixo.y);
        float limite = giroMaximoCorrigido * Mathf.Deg2Rad;
        desvio = Mathf.Clamp(desvio, -limite, limite);

        float co = Mathf.Cos(desvio), se = Mathf.Sin(desvio);
        var girados = new Vector3[21];
        for (int i = 0; i < 21; i++)
        {
            Vector3 p = relativosAoPulso[i];
            girados[i] = new Vector3(p.x * co - p.y * se,
                                     p.x * se + p.y * co,
                                     p.z);
        }
        return girados;
    }

    static Vector3[] NormalizarEscala(Vector3[] relativosAoPulso)
    {
        float tamanhoMao = relativosAoPulso[9].magnitude;
        if (tamanhoMao < 0.0001f) tamanhoMao = 1f; // proteção contra divisão por zero

        var resultado = new Vector3[21];
        for (int i = 0; i < 21; i++)
            resultado[i] = relativosAoPulso[i] / tamanhoMao;
        return resultado;
    }

    // Grava UMA amostra da letra. Pode chamar várias vezes para a mesma letra
    // - quanto mais amostras, melhor o reconhecimento.
    public void GravarLetra(string nomeDaLetra, Vector3[] pontosAtuais)
    {
        if (bancoDeDados == null)
        {
            Debug.LogError("Banco de dados nao conectado no Inspector!");
            return;
        }

#if UNITY_EDITOR
        Undo.RecordObject(bancoDeDados, "Gravar Letra LIBRAS");
#endif

        var novaLetra = new AlfabetoData.LetraPadrao();
        novaLetra.nome = nomeDaLetra;
        novaLetra.pontosNormalizados = new Vector3[21];

        // Normaliza pela posição do pulso (ponto 0)
        Vector3 pulso = pontosAtuais[0];
        for (int i = 0; i < 21; i++)
            novaLetra.pontosNormalizados[i] = pontosAtuais[i] - pulso;

        bancoDeDados.letrasGravadas.Add(novaLetra);

        // Conta quantas amostras desta letra já existem
        int total = 0;
        foreach (var l in bancoDeDados.letrasGravadas)
            if (l.nome == nomeDaLetra) total++;

#if UNITY_EDITOR
        EditorUtility.SetDirty(bancoDeDados);
        AssetDatabase.SaveAssets();
#endif

        Debug.Log("Letra [" + nomeDaLetra + "] gravada! Total de amostras desta letra: " + total);
    }

    // Chamado automaticamente quando o jogador acerta uma letra no jogo.
    // Acumula até 30 amostras por letra sem logs excessivos.
    // O aprendizado automatico foi retirado.
    //
    // Cada acerto durante o jogo virava uma nova amostra do banco. Parecia
    // bom, mas gravava a mao como ela estava no instante do acerto - inclinada,
    // meio fechada, com a leitura ruim - e o banco ia se enchendo de amostras
    // deformadas. Quinze das vinte letras chegaram ao teto de 30 amostras por
    // esse caminho, sem ninguem conferir nenhuma delas.
    //
    // O banco agora so muda no treinamento, com quem grava vendo o que gravou.

    // Grava um MOVIMENTO completo (sequência de quadros) para letra dinâmica
    public void GravarSinalDinamico(string nome, List<Vector3[]> quadrosAbsolutos)
    {
        if (bancoDeDados == null) return;

#if UNITY_EDITOR
        Undo.RecordObject(bancoDeDados, "Gravar Sinal Dinamico");
#endif

        var sinal = new AlfabetoData.SinalDinamico
        {
            nome    = nome,
            quadros = new List<AlfabetoData.QuadroDeMao>()
        };
        foreach (var absoluto in quadrosAbsolutos)
        {
            // Cada quadro fica relativo ao pulso (igual às letras estáticas)
            var relativo = new Vector3[21];
            Vector3 pulso = absoluto[0];
            for (int i = 0; i < 21; i++) relativo[i] = absoluto[i] - pulso;
            sinal.quadros.Add(new AlfabetoData.QuadroDeMao { pontos = relativo });
        }
        bancoDeDados.sinaisDinamicos.Add(sinal);

        int total = 0;
        foreach (var s in bancoDeDados.sinaisDinamicos)
            if (s.nome == nome) total++;

#if UNITY_EDITOR
        EditorUtility.SetDirty(bancoDeDados);
        AssetDatabase.SaveAssets();
#endif
        Debug.Log("Movimento [" + nome + "] gravado com " + sinal.quadros.Count +
                  " quadros! Amostras deste movimento: " + total);
    }

    // Apaga TODAS as amostras de uma letra (use Shift+Tecla no treinamento)
    public void ApagarLetra(string nomeDaLetra)
    {
        if (bancoDeDados == null) return;

#if UNITY_EDITOR
        Undo.RecordObject(bancoDeDados, "Apagar Letra LIBRAS");
#endif

        int removidos = bancoDeDados.letrasGravadas.RemoveAll(l => l.nome == nomeDaLetra);
        removidos += bancoDeDados.sinaisDinamicos.RemoveAll(s => s.nome == nomeDaLetra);

#if UNITY_EDITOR
        EditorUtility.SetDirty(bancoDeDados);
        AssetDatabase.SaveAssets();
#endif

        Debug.Log("Letra [" + nomeDaLetra + "] apagada. " + removidos + " amostra(s) removida(s).");
    }

    public string ClassificarLetra(Vector3[] pontosAtuais)
    {
        if (bancoDeDados == null || bancoDeDados.letrasGravadas.Count == 0) return "Nenhuma";

        // Características da mão atual: posições normalizadas + ângulos
        Vector3[] corrigidos = new Vector3[21];
        for (int i = 0; i < 21; i++) corrigidos[i] = DaCamera(pontosAtuais[i]);

        Vector3 pulso = corrigidos[0];
        Vector3[] rel = new Vector3[21];
        for (int i = 0; i < 21; i++) rel[i] = corrigidos[i] - pulso;
        Vector3[] atualPos = NormalizarEscala(EndireitarPalma(rel));
        float[]   atualAng = ExtrairAngulos(corrigidos);

        // Distância da mão atual até TODAS as amostras gravadas
        var candidatos = new List<KeyValuePair<float, string>>();
        var amostra = new Vector3[21];
        foreach (var padrao in bancoDeDados.letrasGravadas)
        {
            for (int i = 0; i < 21; i++) amostra[i] = DoBanco(padrao.pontosNormalizados[i]);
            Vector3[] padraoPos = NormalizarEscala(EndireitarPalma(amostra));
            float[]   padraoAng = ExtrairAngulos(amostra);
            float dist = DistanciaEntre(atualPos, atualAng, padraoPos, padraoAng);
            candidatos.Add(new KeyValuePair<float, string>(dist, padrao.nome));
        }
        candidatos.Sort((a, b) => a.Key.CompareTo(b.Key));

        // Votação kNN com peso: cada uma das K amostras mais próximas vota com
        // força 1/distância, então uma amostra bem parecida pesa mais que outra
        // que só entrou na lista por falta de concorrência.
        int k = Mathf.Min(vizinhosK, candidatos.Count);
        var forca             = new Dictionary<string, float>();
        var melhorDistDaLetra = new Dictionary<string, float>();
        for (int i = 0; i < k; i++)
        {
            string nome  = candidatos[i].Value;
            float  peso  = 1f / (candidatos[i].Key + 0.5f);
            forca[nome]  = forca.ContainsKey(nome) ? forca[nome] + peso : peso;
            if (!melhorDistDaLetra.ContainsKey(nome))
                melhorDistDaLetra[nome] = candidatos[i].Key; // lista ordenada -> 1ª é a menor
        }

        string vencedora = "";
        float  maiorForca = -1f;
        foreach (var par in forca)
            if (par.Value > maiorForca) { maiorForca = par.Value; vencedora = par.Key; }

        float menorDistancia = melhorDistDaLetra[vencedora];

        // Melhor distância de uma letra DIFERENTE: a rival mais próxima
        float distanciaDaRival = float.MaxValue;
        foreach (var c in candidatos)
            if (c.Value != vencedora) { distanciaDaRival = c.Key; break; }

        UltimaDistancia = menorDistancia;
        UltimaLetra     = vencedora;

        // Debug: mostra a cada 0.5s a eleição, a distância e a da rival.
        // O que importa é a FOLGA entre as duas: quanto maior, mais seguro.
        if (mostrarDebug && Time.time - tempoUltimoDebug > 0.5f)
        {
            tempoUltimoDebug = Time.time;
            Debug.Log("Mais parecido: [" + vencedora + "] entre " + k +
                      " vizinhos, distancia " + menorDistancia.ToString("F2") +
                      ", rival a " + distanciaDaRival.ToString("F2") +
                      " -> " + (Aceita(menorDistancia, distanciaDaRival) ? "ACEITA" : "recusada"));
        }

        return Aceita(menorDistancia, distanciaDaRival) ? vencedora : "Desconhecido";
    }

    // Duas condições para dar a letra por feita:
    //   1. a mão parece com alguma coisa do banco (limite de segurança);
    //   2. a vencedora ganha da rival com folga.
    // A segunda é o critério de verdade, e é relativa: se a leitura piorar,
    // as duas distâncias sobem juntas e a comparação continua honesta. Era
    // exatamente aí que o limite fixo falhava - bastava a mão inclinar um
    // pouco para TUDO passar do limite e nada mais ser reconhecido.
    bool Aceita(float distanciaDaVencedora, float distanciaDaRival)
    {
        return distanciaDaVencedora < limiteDeSeguranca &&
               distanciaDaVencedora < vantagemNecessaria * distanciaDaRival;
    }

    // ── Letras dinâmicas: comparação de MOVIMENTOS via DTW ───────────────────

    private float tempoUltimoDebugDinamico = 0f;

    // Compara a janela de movimento atual com os movimentos gravados.
    // Retorna a letra vencedora ou "Desconhecido".
    public string ClassificarSinalDinamico(List<Vector3[]> janelaAbsoluta)
    {
        if (bancoDeDados == null || bancoDeDados.sinaisDinamicos == null ||
            bancoDeDados.sinaisDinamicos.Count == 0 ||
            janelaAbsoluta == null || janelaAbsoluta.Count < 6)
            return "Desconhecido";

        // Normaliza a janela atual uma única vez (pulso + tamanho da mão)
        var janela = new List<Vector3[]>(janelaAbsoluta.Count);
        foreach (var absoluto in janelaAbsoluta)
        {
            var relativo = new Vector3[21];
            Vector3 pulso = DaCamera(absoluto[0]);
            for (int i = 0; i < 21; i++) relativo[i] = DaCamera(absoluto[i]) - pulso;
            janela.Add(NormalizarEscala(EndireitarPalma(relativo)));
        }

        string melhor = "Desconhecido";
        float  menor  = float.MaxValue;
        // Melhor custo de um movimento de OUTRA letra, para medir a folga
        var menorPorLetra = new Dictionary<string, float>();

        foreach (var sinal in bancoDeDados.sinaisDinamicos)
        {
            var amostra = new List<Vector3[]>(sinal.quadros.Count);
            foreach (var quadro in sinal.quadros)
            {
                var q = new Vector3[21];
                for (int i = 0; i < 21; i++) q[i] = DoBanco(quadro.pontos[i]);
                amostra.Add(NormalizarEscala(EndireitarPalma(q)));
            }

            float custo = CustoDTW(janela, amostra);
            if (!menorPorLetra.ContainsKey(sinal.nome) || custo < menorPorLetra[sinal.nome])
                menorPorLetra[sinal.nome] = custo;
            if (custo < menor)
            {
                menor  = custo;
                melhor = sinal.nome;
            }
        }

        float custoDaRival = float.MaxValue;
        foreach (var par in menorPorLetra)
            if (par.Key != melhor && par.Value < custoDaRival) custoDaRival = par.Value;

        if (mostrarDebug && menor < float.MaxValue &&
            Time.time - tempoUltimoDebugDinamico > 0.6f)
        {
            tempoUltimoDebugDinamico = Time.time;
            Debug.Log("Movimento mais parecido: [" + melhor + "] custo " +
                      menor.ToString("F2") + ", rival a " + custoDaRival.ToString("F2"));
        }

        bool aceita = menor < limiteDinamicoDeSeguranca &&
                      menor < vantagemNecessaria * custoDaRival;
        return aceita ? melhor : "Desconhecido";
    }

    static float DistanciaEntreQuadros(Vector3[] a, Vector3[] b)
    {
        float d = 0f;
        for (int i = 0; i < 21; i++) d += Vector3.Distance(a[i], b[i]);
        return d;
    }

    // DTW (Dynamic Time Warping): alinha duas sequências no tempo antes de
    // comparar - o MESMO gesto feito mais rápido ou mais devagar ainda casa.
    // Retorna o custo médio por passo (independe do tamanho das sequências).
    static float CustoDTW(List<Vector3[]> a, List<Vector3[]> b)
    {
        int n = a.Count, m = b.Count;
        float[,] D = new float[n + 1, m + 1];
        for (int i = 0; i <= n; i++)
            for (int j = 0; j <= m; j++)
                D[i, j] = float.PositiveInfinity;
        D[0, 0] = 0f;

        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                float custo = DistanciaEntreQuadros(a[i - 1], b[j - 1]);
                float menorCaminho = Mathf.Min(D[i - 1, j],
                                     Mathf.Min(D[i, j - 1], D[i - 1, j - 1]));
                D[i, j] = custo + menorCaminho;
            }

        return D[n, m] / (n + m);
    }
}
