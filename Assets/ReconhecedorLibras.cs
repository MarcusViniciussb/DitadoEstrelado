using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ReconhecedorLibras : MonoBehaviour
{
    [Header("Conecte o 'MeuAlfabeto' aqui!")]
    public AlfabetoData bancoDeDados;
    public float toleranciaDeErro = 2.5f;

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

    // Apaga TODAS as amostras de uma letra (use Shift+Tecla no treinamento)
    public void ApagarLetra(string nomeDaLetra)
    {
        if (bancoDeDados == null) return;

#if UNITY_EDITOR
        Undo.RecordObject(bancoDeDados, "Apagar Letra LIBRAS");
#endif

        int removidos = bancoDeDados.letrasGravadas.RemoveAll(l => l.nome == nomeDaLetra);

#if UNITY_EDITOR
        EditorUtility.SetDirty(bancoDeDados);
        AssetDatabase.SaveAssets();
#endif

        Debug.Log("Letra [" + nomeDaLetra + "] apagada. " + removidos + " amostra(s) removida(s).");
    }

    public string ClassificarLetra(Vector3[] pontosAtuais)
    {
        if (bancoDeDados == null || bancoDeDados.letrasGravadas.Count == 0) return "Nenhuma";

        string melhorLetra  = "Desconhecido";
        float menorDistancia = float.MaxValue;

        Vector3 pulso = pontosAtuais[0];
        Vector3[] norm = new Vector3[21];
        for (int i = 0; i < 21; i++) norm[i] = pontosAtuais[i] - pulso;

        foreach (var padrao in bancoDeDados.letrasGravadas)
        {
            float dist = 0;
            for (int i = 0; i < 21; i++)
                dist += Vector3.Distance(norm[i], padrao.pontosNormalizados[i]);

            if (dist < menorDistancia)
            {
                menorDistancia = dist;
                melhorLetra    = padrao.nome;
            }
        }

        return (menorDistancia < toleranciaDeErro) ? melhorLetra : "Desconhecido";
    }
}
