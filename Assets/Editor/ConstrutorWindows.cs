using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// ConstrutorWindows: gera o executavel para computador.
//
// Pelo editor:  menu  Ditado Estrelado > Gerar executavel para Windows
// Pela linha de comando:
//   Unity.exe -quit -batchmode -projectPath <pasta> -executeMethod ConstrutorWindows.Construir
//
// O resultado sai em Builds/Windows/, com o executavel e a pasta do rastreador
// em Python ao lado dele. O jogo procura essa pasta ao lado do proprio
// executavel, entao ela precisa viajar junto na hora de distribuir.
public static class ConstrutorWindows
{
    const string PASTA_SAIDA = "Builds/Windows";
    const string EXECUTAVEL  = PASTA_SAIDA + "/DitadoEstrelado.exe";

    [MenuItem("Ditado Estrelado/Gerar executavel para Windows")]
    public static void Construir()
    {
        ConfigurarIcones.Aplicar();

        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,
                                           ScriptingImplementation.Mono2x);
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth  = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow     = true;

        var opcoes = new BuildPlayerOptions
        {
            scenes           = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = EXECUTAVEL,
            target           = BuildTarget.StandaloneWindows64,
            targetGroup      = BuildTargetGroup.Standalone,
            options          = BuildOptions.None
        };

        BuildReport relatorio = BuildPipeline.BuildPlayer(opcoes);
        var resumo = relatorio.summary;

        if (resumo.result != BuildResult.Succeeded)
        {
            Debug.LogError("Falhou a geracao do executavel: " + resumo.result +
                           " (" + resumo.totalErrors + " erros)");
            EditorApplication.Exit(1);
            return;
        }

        CopiarRastreador();

        Debug.Log("Executavel gerado com sucesso: " + EXECUTAVEL +
                  "  (" + (resumo.totalSize / (1024 * 1024)) + " MB)");
    }

    // O rastreador em Python precisa ficar ao lado do executavel: e ali que o
    // jogo o procura. Sem ele o jogo ainda abre, com o rastreador reserva
    // embarcado, porem com menos precisao.
    static void CopiarRastreador()
    {
        string origem  = "RastreadorPython";
        string destino = Path.Combine(PASTA_SAIDA, "RastreadorPython");

        if (!Directory.Exists(origem))
        {
            Debug.LogWarning("Pasta " + origem + " nao encontrada - o executavel " +
                             "vai depender do rastreador reserva.");
            return;
        }

        Directory.CreateDirectory(destino);
        foreach (string caminho in Directory.GetFiles(origem))
        {
            string nome = Path.GetFileName(caminho);
            // As figuras da pasta servem a documentacao, nao ao jogo
            if (nome.StartsWith("fig_")) continue;
            File.Copy(caminho, Path.Combine(destino, nome), true);
        }
        Debug.Log("Rastreador em Python copiado para " + destino);
    }
}
