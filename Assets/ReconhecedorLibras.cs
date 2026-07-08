using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ReconhecedorLibras : MonoBehaviour
{
    [Header("Conecte o 'MeuAlfabeto' aqui!")]
    public AlfabetoData bancoDeDados;

    [Header("Aparece 'Desconhecido' demais? AUMENTE. Confunde letras? DIMINUA.")]
    public float toleranciaDeErro = 6f;

    [Header("Classificacao kNN")]
    [Range(1, 7)]   public int   vizinhosK      = 3;    // quantas amostras próximas votam
    [Range(0f, 3f)] public float pesoDosAngulos = 0.5f; // importância dos ângulos vs posições

    [Header("Letras dinamicas (gravadas como MOVIMENTO, nao como foto)")]
    public string[] letrasDinamicas = { "H", "J", "K", "X", "Z" };
    public float toleranciaDinamica = 4.5f; // mesmo esquema: sobe = aceita mais facil

    public bool EhLetraDinamica(string letra)
    {
        foreach (var l in letrasDinamicas)
            if (l == letra) return true;
        return false;
    }

    // ── Ângulos das articulações ─────────────────────────────────────────────
    // Cada trio (a, b, c) mede o ângulo NO ponto b entre os vetores (a-b) e (c-b).
    // Ângulo não muda quando a mão gira ou muda de tamanho → complementa as posições.
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

    // Abertura entre dedos vizinhos: ângulo entre os vetores pulso→base de cada dedo
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

    // Distância da última classificação — quanto MENOR, mais parecido o sinal
    // está com o gravado (o ControladorCamera usa para aceitar mais rápido)
    public float UltimaDistancia { get; private set; } = float.MaxValue;

    // Resumo tipo "A: 7   B: 5   C: 4" — mostrado na tela de treinamento
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

        var sb = new System.Text.StringBuilder();
        foreach (var par in contagem)
            sb.Append(par.Key).Append(": ").Append(par.Value).Append("   ");
        return sb.ToString();
    }

    // Normaliza pontos (relativos ao pulso) pelo TAMANHO da mão — a distância
    // do pulso até a base do dedo médio (ponto 9). Assim a mesma letra é
    // reconhecida perto OU longe da câmera, pois a escala deixa de importar.
    // Os dados já gravados continuam válidos: eles também passam por aqui.
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
    // — quanto mais amostras, melhor o reconhecimento.
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
    public void AprendizagemAutomatica(string nomeDaLetra, Vector3[] pontosAtuais)
    {
        if (bancoDeDados == null) return;

        int total = 0;
        foreach (var l in bancoDeDados.letrasGravadas)
            if (l.nome == nomeDaLetra) total++;

        if (total >= 30) return; // limite para não crescer indefinidamente

        var novaLetra = new AlfabetoData.LetraPadrao();
        novaLetra.nome = nomeDaLetra;
        novaLetra.pontosNormalizados = new Vector3[21];
        Vector3 pulso = pontosAtuais[0];
        for (int i = 0; i < 21; i++)
            novaLetra.pontosNormalizados[i] = pontosAtuais[i] - pulso;

        bancoDeDados.letrasGravadas.Add(novaLetra);

#if UNITY_EDITOR
        EditorUtility.SetDirty(bancoDeDados);
        AssetDatabase.SaveAssets();
#endif
        // Loga apenas a cada 5 novas amostras para não poluir o Console
        if ((total + 1) % 5 == 0)
            Debug.Log("Aprendizado [" + nomeDaLetra + "]: " + (total + 1) + " amostras.");
    }

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
        Vector3 pulso = pontosAtuais[0];
        Vector3[] rel = new Vector3[21];
        for (int i = 0; i < 21; i++) rel[i] = pontosAtuais[i] - pulso;
        Vector3[] atualPos = NormalizarEscala(rel);
        float[]   atualAng = ExtrairAngulos(pontosAtuais);

        // Distância da mão atual até TODAS as amostras gravadas
        var candidatos = new List<KeyValuePair<float, string>>();
        foreach (var padrao in bancoDeDados.letrasGravadas)
        {
            Vector3[] padraoPos = NormalizarEscala(padrao.pontosNormalizados);
            float[]   padraoAng = ExtrairAngulos(padrao.pontosNormalizados);
            float dist = DistanciaEntre(atualPos, atualAng, padraoPos, padraoAng);
            candidatos.Add(new KeyValuePair<float, string>(dist, padrao.nome));
        }
        candidatos.Sort((a, b) => a.Key.CompareTo(b.Key));

        // Votação kNN: as K amostras mais próximas votam, a maioria vence.
        // Uma amostra ruim isolada perde a eleição em vez de decidir sozinha.
        int k = Mathf.Min(vizinhosK, candidatos.Count);
        var votos             = new Dictionary<string, int>();
        var melhorDistDaLetra = new Dictionary<string, float>();
        for (int i = 0; i < k; i++)
        {
            string nome = candidatos[i].Value;
            votos[nome] = votos.ContainsKey(nome) ? votos[nome] + 1 : 1;
            if (!melhorDistDaLetra.ContainsKey(nome))
                melhorDistDaLetra[nome] = candidatos[i].Key; // lista ordenada → 1ª é a menor
        }

        string vencedora = "";
        int maisVotos = 0;
        foreach (var par in votos)
        {
            bool ganha = par.Value > maisVotos ||
                         (par.Value == maisVotos && vencedora != "" &&
                          melhorDistDaLetra[par.Key] < melhorDistDaLetra[vencedora]);
            if (ganha) { vencedora = par.Key; maisVotos = par.Value; }
        }

        float menorDistancia = melhorDistDaLetra[vencedora];
        UltimaDistancia = menorDistancia;

        // Debug: mostra a cada 0.5s a eleição e a distância.
        // Use para calibrar a tolerância: faça o sinal correto, veja a distância
        // típica, e deixe a tolerância um pouco ACIMA desse valor.
        if (mostrarDebug && Time.time - tempoUltimoDebug > 0.5f)
        {
            tempoUltimoDebug = Time.time;
            string veredito = (menorDistancia < toleranciaDeErro) ? "ACEITA" : "recusada";
            Debug.Log("Mais parecido: [" + vencedora + "] " + maisVotos + "/" + k +
                      " votos, distancia " + menorDistancia.ToString("F2") +
                      " / tolerancia " + toleranciaDeErro + " -> " + veredito);
        }

        return (menorDistancia < toleranciaDeErro) ? vencedora : "Desconhecido";
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
            Vector3 pulso = absoluto[0];
            for (int i = 0; i < 21; i++) relativo[i] = absoluto[i] - pulso;
            janela.Add(NormalizarEscala(relativo));
        }

        string melhor = "Desconhecido";
        float  menor  = float.MaxValue;

        foreach (var sinal in bancoDeDados.sinaisDinamicos)
        {
            var amostra = new List<Vector3[]>(sinal.quadros.Count);
            foreach (var quadro in sinal.quadros)
                amostra.Add(NormalizarEscala(quadro.pontos));

            float custo = CustoDTW(janela, amostra);
            if (custo < menor)
            {
                menor  = custo;
                melhor = sinal.nome;
            }
        }

        if (mostrarDebug && menor < float.MaxValue &&
            Time.time - tempoUltimoDebugDinamico > 0.6f)
        {
            tempoUltimoDebugDinamico = Time.time;
            Debug.Log("Movimento mais parecido: [" + melhor + "] custo " +
                      menor.ToString("F2") + " / tolerancia " + toleranciaDinamica);
        }

        return (menor < toleranciaDinamica) ? melhor : "Desconhecido";
    }

    static float DistanciaEntreQuadros(Vector3[] a, Vector3[] b)
    {
        float d = 0f;
        for (int i = 0; i < 21; i++) d += Vector3.Distance(a[i], b[i]);
        return d;
    }

    // DTW (Dynamic Time Warping): alinha duas sequências no tempo antes de
    // comparar — o MESMO gesto feito mais rápido ou mais devagar ainda casa.
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
