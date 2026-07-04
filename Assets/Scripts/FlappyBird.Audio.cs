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
    int _bananaCombo;        // consecutive quick pickups — drives a rising pitch
    float _lastBananaTime;   // used to reset the combo after a pause

    /// <summary>Creates the AudioSource and the banana pickup clip (idempotent).</summary>
    void SetupAudio()
    {
        _sfx = GetComponent<AudioSource>();
        if (_sfx == null) _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        if (_bananaClip == null) _bananaClip = MakePickupClip();
    }

    /// <summary>Plays the banana pickup blip. Rapid pickups climb in pitch (like a
    /// coin combo, so repetition feels rewarding); a pause resets it so it never
    /// becomes a repetitive drone.</summary>
    void PlayBanana()
    {
        if (_sfx == null || _bananaClip == null) return;
        if (Time.time - _lastBananaTime > 1.2f) _bananaCombo = 0;
        _lastBananaTime = Time.time;
        _sfx.pitch = 1f + Mathf.Min(_bananaCombo, 10) * 0.05f; // caps at +50%
        _sfx.PlayOneShot(_bananaClip, 0.7f);
        _bananaCombo++;
    }

    /// <summary>Builds a rising two-harmonic blip with a sine (bell) envelope so it
    /// has no start/end clicks.</summary>
    AudioClip MakePickupClip()
    {
        const int rate = 44100;
        const float dur = 0.13f;                          // short and light
        int n = (int)(rate * dur);
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float prog = (float)i / n;
            float freq = Mathf.Lerp(520f, 780f, prog);    // lower/warmer than before, less piercing
            phase += 2f * Mathf.PI * freq / rate;
            float env = Mathf.Sin(Mathf.PI * prog);       // 0 -> 1 -> 0, click-free
            float wave = Mathf.Sin(phase) + 0.2f * Mathf.Sin(2f * phase); // gentle warmth
            data[i] = wave * env * 0.3f;
        }
        AudioClip clip = AudioClip.Create("bananaPickup", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
