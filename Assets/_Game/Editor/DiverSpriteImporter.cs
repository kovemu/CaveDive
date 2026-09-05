#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Any PNG dropped into Assets/_Game/Resources/Diver is automatically prepared
// as a transparent single Sprite so no manual import settings are required.
public sealed class DiverSpriteImporter : AssetPostprocessor
{
    private const string DiverFolder = "Assets/_Game/Resources/Diver/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(DiverFolder) || !assetPath.EndsWith(".png"))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 256f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
    }
}
#endif
