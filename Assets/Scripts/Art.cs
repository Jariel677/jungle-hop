using UnityEngine;

/// <summary>
/// Shared material factory and primitive-building helpers. Callers create a
/// small palette of materials once and reuse them, keeping the procedural
/// world cheap.
/// </summary>
public static class Art
{
    static Shader _std;

    static Shader Std
    {
        get
        {
            if (_std == null) _std = Shader.Find("Standard");
            return _std;
        }
    }

    /// <summary>A solid Standard material.</summary>
    public static Material Mat(Color color, float metallic, float smoothness)
    {
        Material m = new Material(Std);
        m.color = color;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smoothness);
        return m;
    }

    /// <summary>A material that emits light (glows).</summary>
    public static Material Glow(Color color, Color emission, float smoothness)
    {
        Material m = Mat(color, 0f, smoothness);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emission);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return m;
    }

    /// <summary>
    /// Spawns a collider-free primitive under <paramref name="parent"/> with the
    /// given local transform and shared material.
    /// </summary>
    public static GameObject Solid(PrimitiveType type, Transform parent, Vector3 localPos,
                                   Vector3 localScale, Material mat, string name)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
        return go;
    }

    /// <summary>An unlit, alpha-blended material for a flat sprite (e.g. clouds).</summary>
    public static Material SpriteMat(Texture texture)
    {
        Shader s = Shader.Find("Unlit/Transparent");
        Material m = new Material(s != null ? s : Std);
        m.mainTexture = texture;
        return m;
    }

    /// <summary>An opaque, unlit material for a full-bright backdrop image.</summary>
    public static Material BackdropMat(Texture texture)
    {
        Shader s = Shader.Find("Unlit/Texture");
        Material m = new Material(s != null ? s : Std);
        m.mainTexture = texture;
        return m;
    }
}
