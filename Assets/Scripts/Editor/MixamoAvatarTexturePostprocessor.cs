using UnityEditor;

/// <summary>
/// Auto-fixes Mixamo avatar texture imports so freshly-imported avatars don't render
/// shiny/silver. Runs automatically on import (no manual steps), for any texture under a
/// "/Mixamo/" folder whose filename contains "Normal":
///   - Marks it as a Normal Map (textureType = NormalMap), which also forces linear color.
///
/// Why: Mixamo "_Normal" textures extract as plain (sRGB Default) textures. When a material
/// uses such a texture as its Normal/Bump map, the lighting is wrong and the avatar looks
/// metallic/silver. Marking them as normal maps fixes the shading. This saved having to do it
/// by hand for every one of the 6 avatars (Ch07/08/22/23/33/37).
///
/// NOTE: hair-material Surface Type (Transparent → Opaque) and Smoothness are set per-avatar
/// after import (they live on the extracted .mat, not the texture importer).
/// </summary>
public class MixamoAvatarTexturePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (path.IndexOf("/Mixamo/", System.StringComparison.OrdinalIgnoreCase) < 0) return;

        string file = System.IO.Path.GetFileNameWithoutExtension(path);
        if (file.IndexOf("Normal", System.StringComparison.OrdinalIgnoreCase) < 0) return;

        var importer = (TextureImporter)assetImporter;
        if (importer.textureType != TextureImporterType.NormalMap)
            importer.textureType = TextureImporterType.NormalMap; // also sets linear color space
    }
}
