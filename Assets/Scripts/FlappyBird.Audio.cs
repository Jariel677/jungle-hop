using UnityEngine;

/// <summary>
/// <see cref="FlappyBird"/> sound: a short "pickup" blip synthesized in code (no
/// audio asset needed), played when a banana is collected. A bright, rising two-
/// harmonic tone with a click-free bell envelope — reads as a cheerful collect.
/// Same partial class.
/// </summary>
public partial class FlappyBird
{
    AudioSource _sfx;
    AudioClip _bananaClip;

    /// <summary>Creates the AudioSource and the banana pickup clip (idempotent).</summary>
    void SetupAudio()
    {
        _sfx = GetComponent<AudioSource>();
        if (_sfx == null) _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        if (_bananaClip == null) _bananaClip = MakePickupClip();
    }

    /// <summary>Plays the banana pickup pluck at a constant pitch, so every banana
    /// sounds identical no matter how many you collect.</summary>
    void PlayBanana()
    {
        if (_sfx == null || _bananaClip == null) return;
        _sfx.pitch = 1f;
        _sfx.PlayOneShot(_bananaClip, 0.5f);
    }

    /// <summary>Builds a soft two-note marimba/harp pluck (A4 then C#5, a warm major
    /// third) — a mellow, refined "collect" with a natural mallet decay, no siren
    /// rise and no piercing high frequencies. Click-free via attack ramp + tail fade.</summary>
    AudioClip MakePickupClip()
    {
        const int rate = 44100;
        const float dur = 0.28f;                          // short, with a gentle decay tail
        int n = (int)(rate * dur);
        var data = new float[n];

        const float f1 = 440.00f;                         // A4
        const float f2 = 554.37f;                         // C#5 (major third up)
        const float note2Start = 0.085f;                  // slight arpeggio between the two notes
        float ph1 = 0f, ph2 = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;

            // First note: quick attack, exponential mallet decay, warm harmonics.
            float e1 = PluckEnv(t, 0.004f, 9f);
            ph1 += 2f * Mathf.PI * f1 / rate;
            float v1 = Mathf.Sin(ph1) + 0.30f * Mathf.Sin(2f * ph1) + 0.08f * Mathf.Sin(3f * ph1);

            // Second note enters a touch later for a pleasant two-note lift.
            float v2 = 0f;
            float t2 = t - note2Start;
            if (t2 > 0f)
            {
                float e2 = PluckEnv(t2, 0.004f, 9f);
                ph2 += 2f * Mathf.PI * f2 / rate;
                v2 = (Mathf.Sin(ph2) + 0.30f * Mathf.Sin(2f * ph2) + 0.08f * Mathf.Sin(3f * ph2)) * e2;
            }
            v1 *= e1;

            float s = (v1 * 0.55f + v2 * 0.62f) * 0.5f;

            // Tail fade over the last 12 ms so the clip never ends on a click.
            float fadeSamples = 0.012f * rate;
            if (i > n - fadeSamples) s *= (n - i) / fadeSamples;

            data[i] = Mathf.Clamp(s, -0.99f, 0.99f);
        }

        AudioClip clip = AudioClip.Create("bananaPickup", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Fast linear attack then exponential decay — a natural plucked/mallet
    /// envelope. <paramref name="decay"/> is the per-second decay rate.</summary>
    static float PluckEnv(float t, float attack, float decay)
    {
        if (t < 0f) return 0f;
        float a = t < attack ? (t / attack) : 1f;
        return a * Mathf.Exp(-decay * t);
    }
}
