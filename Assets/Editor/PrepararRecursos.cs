using UnityEditor;
using UnityEngine;

// PrepararRecursos: garante que as imagens usadas pelo menu estejam importadas
// como Sprite, para poderem ser carregadas por Resources.Load<Sprite> em tempo
// de execução. Roda automaticamente antes de cada build.
public static class PrepararRecursos
{
    const string SIMBOLO_LIBRAS = "Assets/Resources/simbolo_libras.png";

    public static void Configurar()
    {
        GarantirSprite(SIMBOLO_LIBRAS);
    }

    static void GarantirSprite(string caminho)
    {
        var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
        if (imp == null) { Debug.LogWarning("Recurso nao encontrado: " + caminho); return; }

        bool mudou = false;
        if (imp.textureType != TextureImporterType.Sprite)
        { imp.textureType = TextureImporterType.Sprite; mudou = true; }
        if (imp.spriteImportMode != SpriteImportMode.Single)
        { imp.spriteImportMode = SpriteImportMode.Single; mudou = true; }
        if (imp.alphaIsTransparency != true)
        { imp.alphaIsTransparency = true; mudou = true; }

        if (mudou) { imp.SaveAndReimport(); Debug.Log("Sprite configurado: " + caminho); }
    }
}
