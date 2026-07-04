using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the in-game backdrop and background art crisp: textures under
/// Resources/JungleBG import uncompressed, with no mipmaps (so a full-screen
/// backdrop never samples a downscaled mip) and clamped edges. This is the
/// sharpest an image can be at a given source resolution — for more detail,
/// supply a higher-resolution source image.
/// </summary>
public class JungleTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath.Replace('\\', '/').IndexOf("Resources/JungleBG", System.StringComparison.OrdinalIgnoreCase) < 0)
            return;

        var ti = (TextureImporter)assetImporter;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 2048;
    }
}
