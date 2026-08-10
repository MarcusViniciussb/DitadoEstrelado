using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// ConstrutorAndroid: gera o APK sem precisar abrir as janelas do editor.
//
// Pelo editor:  menu  Ditado Estrelado > Gerar APK para Android
// Pela linha de comando:
//   Unity.exe -quit -batchmode -projectPath <pasta> -executeMethod ConstrutorAndroid.Construir
//
// O arquivo sai em Builds/DitadoEstrelado.apk
public static class ConstrutorAndroid
{
    const string DESTINO = "Builds/DitadoEstrelado.apk";

    [MenuItem("Ditado Estrelado/Gerar APK para Android")]
    public static void Construir()
    {
        // Ajustes que precisam valer no momento da compilacao
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait           = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft      = true;
        PlayerSettings.allowedAutorotateToLandscapeRight     = true;

        var opcoes = new BuildPlayerOptions
        {
            scenes           = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = DESTINO,
            target           = BuildTarget.Android,
            targetGroup      = BuildTargetGroup.Android,
            options          = BuildOptions.None
        };

        BuildReport relatorio = BuildPipeline.BuildPlayer(opcoes);
        var resumo = relatorio.summary;

        if (resumo.result == BuildResult.Succeeded)
            Debug.Log("APK gerado com sucesso: " + DESTINO +
                      "  (" + (resumo.totalSize / (1024 * 1024)) + " MB)");
        else
            Debug.LogError("A geracao do APK falhou: " + resumo.result +
                           "  erros: " + resumo.totalErrors);
    }
}
