using UnityEngine;

/// <summary>
/// Central sound effects. Loads CC0 clips from <c>Resources/Audio</c> and plays
/// them as one-shots. Coin pickups rise in pitch through a collecting streak.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    AudioSource _sfx;
    AudioSource _music;
    AudioClip _coin, _jump, _slide, _crash, _powerup, _click;
    float _coinPitch = 1f, _coinResetTimer;

    void Awake()
    {
        Instance = this;
        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;

        _coin = Load("coin");
        _jump = Load("jump");
        _slide = Load("slide");
        _crash = Load("crash");
        _powerup = Load("powerup");
        _click = Load("click");

        _music = gameObject.AddComponent<AudioSource>();
        _music.playOnAwake = false;
        _music.loop = true;
        _music.volume = 0.3f;
        _music.clip = BuildMusic();
        if (GameData.Music) _music.Play();
    }

    /// <summary>Starts or stops the looping background track and remembers the choice.</summary>
    public void SetMusic(bool on)
    {
        if (_music == null) return;
        if (on) { if (!_music.isPlaying) _music.Play(); }
        else _music.Stop();
    }

    /// <summary>Suspends / resumes the track while the game is paused (no-op if music is off).</summary>
    public void SetMusicPaused(bool paused)
    {
        if (_music == null) return;
        if (paused) { if (_music.isPlaying) _music.Pause(); }
        else if (GameData.Music) _music.UnPause();
    }

    // ------------------------------------------------------- procedural music
    /// <summary>
    /// Synthesises a short seamless chiptune loop (A-minor pentatonic) at runtime
    /// so the game ships music without any audio assets.
    /// </summary>
    AudioClip BuildMusic()
    {
        const int sr = 44100;
        const int steps = 32;        // 16th notes
        const int stepLen = 5512;    // ~0.125 s/step -> ~120 BPM, ~4 s loop
        int total = steps * stepLen;
        float[] buf = new float[total];

        // A-minor pentatonic across two octaves: A C D E G ...
        float[] scale = { 220f, 261.63f, 293.66f, 329.63f, 392f,
                          440f, 523.25f, 587.33f, 659.25f, 783.99f };
        // -1 = rest; values index into scale.
        int[] melody =
        {
            5, -1, 7, 5,  4, -1, 5, -1,  3, -1, 4, 3,  1, -1, 3, -1,
            5, -1, 8, 7,  6, -1, 5, -1,  4, -1, 3, 4,  5, -1, -1, -1,
        };
        float[] bass = { 110f, 110f, 146.83f, 130.81f };  // one per 8 steps

        for (int s = 0; s < steps; s++)
        {
            int start = s * stepLen;
            RenderNote(buf, start, stepLen, bass[(s / 8) % bass.Length], 0.16f, sr, true);
            if (melody[s] >= 0)
                RenderNote(buf, start, stepLen, scale[melody[s]], 0.11f, sr, false);
        }

        // Guard the loop seam and prevent clipping.
        int fade = 500;
        for (int i = 0; i < fade; i++) buf[total - 1 - i] *= i / (float)fade;
        for (int i = 0; i < total; i++) buf[i] = Mathf.Clamp(buf[i], -0.99f, 0.99f);

        AudioClip clip = AudioClip.Create("bgm", total, 1, sr, false);
        clip.SetData(buf, 0);
        return clip;
    }

    static void RenderNote(float[] buf, int start, int len, float freq, float amp, int sr, bool sine)
    {
        for (int i = 0; i < len && start + i < buf.Length; i++)
        {
            float t = i / (float)sr;
            float attack = Mathf.Clamp01(i / (sr * 0.006f));
            float env = attack * Mathf.Exp(-t * 6.5f);
            float phase = freq * t;
            float w;
            if (sine) w = Mathf.Sin(phase * 2f * Mathf.PI);
            else { float frac = phase - Mathf.Floor(phase); w = frac < 0.5f ? 1f : -1f; }
            buf[start + i] += w * amp * env;
        }
    }

    static AudioClip Load(string n) { return Resources.Load<AudioClip>("Audio/" + n); }

    void Update()
    {
        if (_coinResetTimer > 0f)
        {
            _coinResetTimer -= Time.unscaledDeltaTime;
            if (_coinResetTimer <= 0f) _coinPitch = 1f;
        }
    }

    void Play(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || _sfx == null) return;
        _sfx.pitch = pitch;
        _sfx.PlayOneShot(clip, volume);
    }

    public void Coin()
    {
        _coinPitch = Mathf.Min(_coinPitch + 0.07f, 2.1f);
        _coinResetTimer = 0.5f;
        Play(_coin, 0.5f, _coinPitch);
    }

    public void Jump() { Play(_jump, 0.45f, 1f); }
    public void Slide() { Play(_slide, 0.5f, 1f); }
    public void Crash() { Play(_crash, 0.95f, 0.9f); }
    public void PowerUp() { Play(_powerup, 0.8f, 1f); }
    public void Click() { Play(_click, 0.55f, 1f); }
}
