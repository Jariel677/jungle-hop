using UnityEngine;

/// <summary>
/// A single short-lived primitive used as a cheap particle (debris, sparkle,
/// dust). Flies out, spins, shrinks, and self-destructs.
/// </summary>
public class Particle : MonoBehaviour
{
    Vector3 _vel;
    float _life, _maxLife, _spin, _gravity;
    Vector3 _scale;

    public void Init(Vector3 vel, float life, float spin, float gravity, Vector3 scale)
    {
        _vel = vel;
        _life = _maxLife = life;
        _spin = spin;
        _gravity = gravity;
        _scale = scale;
    }

    void Update()
    {
        _life -= Time.deltaTime;
        if (_life <= 0f) { Destroy(gameObject); return; }

        _vel.y -= _gravity * Time.deltaTime;
        transform.position += _vel * Time.deltaTime;
        transform.Rotate(_spin * Time.deltaTime, _spin * 1.3f * Time.deltaTime, 0f);
        transform.localScale = _scale * Mathf.Clamp01(_life / _maxLife);
    }
}

/// <summary>
/// Fire-and-forget juice effects — coin sparkles, crash debris, dust puffs.
/// Built from pooled-free primitives so no art assets or shaders are needed.
/// </summary>
public static class Effects
{
    static Material _gold, _dust, _debris;

    static Material Gold
    {
        get { if (_gold == null) _gold = Art.Glow(new Color(1f, 0.85f, 0.2f), new Color(0.8f, 0.55f, 0.05f), 0.7f); return _gold; }
    }

    static Material Dust
    {
        get { if (_dust == null) _dust = Art.Mat(new Color(0.82f, 0.82f, 0.85f), 0f, 0.15f); return _dust; }
    }

    static Material Debris
    {
        get { if (_debris == null) _debris = Art.Mat(new Color(0.92f, 0.4f, 0.2f), 0f, 0.4f); return _debris; }
    }

    static void Burst(Vector3 pos, int count, Material mat, float speed, float life,
                      float size, float gravity, float upBias)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = "FX";
            Collider c = p.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            p.transform.position = pos;
            float s = size * Random.Range(0.6f, 1.25f);
            Vector3 scale = new Vector3(s, s, s);
            p.transform.localScale = scale;
            Renderer r = p.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;

            Vector3 dir = new Vector3(Random.Range(-1f, 1f), Random.Range(upBias, upBias + 1.1f),
                                      Random.Range(-1f, 1f)).normalized;
            Particle part = p.AddComponent<Particle>();
            part.Init(dir * speed * Random.Range(0.65f, 1.3f), life * Random.Range(0.8f, 1.1f),
                      Random.Range(160f, 540f), gravity, scale);
        }
    }

    public static void CoinSparkle(Vector3 pos)
    {
        Burst(pos, 7, Gold, 5.5f, 0.45f, 0.17f, 11f, 0.4f);
    }

    public static void DustPuff(Vector3 pos)
    {
        Burst(pos, 5, Dust, 2.8f, 0.4f, 0.24f, 7f, 0.2f);
    }

    public static void Crash(Vector3 pos, Color tint)
    {
        Burst(pos, 18, Debris, 8.5f, 0.95f, 0.3f, 16f, 0.5f);
    }

    public static void Pickup(Vector3 pos, Color tint)
    {
        Material m = Art.Glow(tint, tint * 0.7f, 0.7f);
        Burst(pos, 10, m, 6f, 0.55f, 0.2f, 10f, 0.5f);
    }
}
