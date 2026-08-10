using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// ConfigurarIcones: aplica a marca do Ditado Estrelado como icone do aplicativo,
// tanto no Android quanto no executavel de computador.
//
// Pelo editor:  menu  Ditado Estrelado > Aplicar icones
// Tambem roda sozinho antes de cada APK, para o icone nunca ficar para tras.
//
// O Android moderno usa o icone "adaptativo", montado em duas camadas: a marca
// na frente e a cor de fundo atras. O sistema recorta esse conjunto no formato
// que o aparelho usa (circulo, quadrado arredondado...). Por isso a marca da
// frente e desenhada menor: as bordas podem ser cortadas.
public static class ConfigurarIcones
{
    const string PASTA  = "Assets/Icones/";
    const string COMUM  = PASTA + "icone.png";
    const string FRENTE = PASTA + "icone_frente.png";
    const string FUNDO  = PASTA + "icone_fundo.png";

    [MenuItem("Ditado Estrelado/Aplicar icones")]
    public static void Aplicar()
    {
        Texture2D comum  = Preparar(COMUM);
        Texture2D frente = Preparar(FRENTE);
        Texture2D fundo  = Preparar(FUNDO);

        if (comum == null)
        {
            Debug.LogWarning("Icone nao encontrado em " + COMUM + " - nada a aplicar.");
            return;
        }

        // Android: icone antigo (aparelhos mais velhos), redondo e adaptativo
        Definir(NamedBuildTarget.Android, comum,  IconKind.Application);
        Definir(NamedBuildTarget.Android, comum,  IconKind.Round);
        if (frente != null) Definir(NamedBuildTarget.Android, frente, IconKind.AdaptiveForeground);
        if (fundo  != null) Definir(NamedBuildTarget.Android, fundo,  IconKind.AdaptiveBackground);

        // Executavel de computador (Windows, Mac, Linux)
        Definir(NamedBuildTarget.Standalone, comum, IconKind.Any);

        AssetDatabase.SaveAssets();
        Debug.Log("Icones aplicados ao Android e ao executavel de computador.");
    }

    // O Unity pede uma imagem para CADA tamanho que o sistema usa. A mesma
    // arte serve para todos: ele reduz cada uma na hora de compilar.
    static void Definir(NamedBuildTarget alvo, Texture2D arte, IconKind tipo)
    {
        int[] tamanhos = PlayerSettings.GetIconSizes(alvo, tipo);
        if (tamanhos == null || tamanhos.Length == 0) return;

        var lista = new Texture2D[tamanhos.Length];
        for (int i = 0; i < lista.Length; i++) lista[i] = arte;
        PlayerSettings.SetIcons(alvo, lista, tipo);
    }

    // Garante que a imagem esteja legivel e sem compressao: icone borrado
    // costuma ser so a compressao de textura aparecendo no lugar errado.
    static Texture2D Preparar(string caminho)
    {
        var importador = AssetImporter.GetAtPath(caminho) as TextureImporter;
        if (importador == null) return null;

        bool mudou = false;
        if (importador.textureType != TextureImporterType.Default)
        { importador.textureType = TextureImporterType.Default; mudou = true; }
        if (importador.textureCompression != TextureImporterCompression.Uncompressed)
        { importador.textureCompression = TextureImporterCompression.Uncompressed; mudou = true; }
        if (!importador.isReadable)   { importador.isReadable   = true;  mudou = true; }
        if (importador.mipmapEnabled) { importador.mipmapEnabled = false; mudou = true; }
        if (importador.maxTextureSize < 1024) { importador.maxTextureSize = 1024; mudou = true; }

        if (mudou)
        {
            importador.SaveAndReimport();
            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
    }
}
