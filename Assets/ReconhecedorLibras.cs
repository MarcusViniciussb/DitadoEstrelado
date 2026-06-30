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

    public void GravarLetra(string nomeDaLetra, Vector3[] pontosAtuais)
    {
        if (bancoDeDados == null)
        {
            Debug.LogError("❌ ERRO: Banco de dados não conectado no Inspector!");
            return;
        }

        // DEDUPLICAÇÃO: Checa se a letra já existe na memória
        for (int i = 0; i < bancoDeDados.letrasGravadas.Count; i++)
        {
            if (bancoDeDados.letrasGravadas[i].nome == nomeDaLetra)
            {
                Debug.LogWarning("⚠️ AVISO: A letra [" + nomeDaLetra + "] já está na memória! O sistema ignorou para não duplicar.");
                return;
            }
        }

#if UNITY_EDITOR
        Undo.RecordObject(bancoDeDados, "Gravar Letra LIBRAS");
#endif

        AlfabetoData.LetraPadrao novaLetra = new AlfabetoData.LetraPadrao();
        novaLetra.nome = nomeDaLetra;
        novaLetra.pontosNormalizados = new Vector3[21];

        Vector3 pulso = pontosAtuais[0];
        for (int i = 0; i < 21; i++)
        {
            novaLetra.pontosNormalizados[i] = pontosAtuais[i] - pulso;
        }

        bancoDeDados.letrasGravadas.Add(novaLetra);

#if UNITY_EDITOR
        EditorUtility.SetDirty(bancoDeDados);
        AssetDatabase.SaveAssets();
#endif

        Debug.Log("✅ SUCESSO: Letra [" + nomeDaLetra + "] gravada permanentemente no Banco de Dados!");
    }

    public string ClassificarLetra(Vector3[] pontosAtuais)
    {
        if (bancoDeDados == null || bancoDeDados.letrasGravadas.Count == 0) return "Nenhuma";

        string melhorLetra = "Desconhecido";
        float menorDistancia = float.MaxValue;

        Vector3 pulso = pontosAtuais[0];
        Vector3[] pontosNormalizados = new Vector3[21];
        for (int i = 0; i < 21; i++) pontosNormalizados[i] = pontosAtuais[i] - pulso;

        foreach (var padrao in bancoDeDados.letrasGravadas)
        {
            float distanciaTotal = 0;
            for (int i = 0; i < 21; i++)
            {
                distanciaTotal += Vector3.Distance(pontosNormalizados[i], padrao.pontosNormalizados[i]);
            }
            if (distanciaTotal < menorDistancia)
            {
                menorDistancia = distanciaTotal;
                melhorLetra = padrao.nome;
            }
        }

        return (menorDistancia < toleranciaDeErro) ? melhorLetra : "Desconhecido";
    }
}