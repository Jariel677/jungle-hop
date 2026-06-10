using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

/// <summary>
/// Central controller: builds the world, runs the menu/play/game-over state
/// machine, tracks score/coins/power-ups, banks rewards, and draws the in-game
/// HUD and run-summary. Pre-game menus are drawn by <see cref="MenuUI"/>.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    static bool _autoStart;

    public enum State { Menu, Playing, GameOver }
    public State CurrentState { get; private set; }

    public enum PowerUp { None, Magnet, Jetpack, Double, Sneakers, Shield }

    public static readonly float[] LaneX = { -2.6f, 0f, 2.6f };

    const float StartSpeed = 9f;
    const float MaxSpeed = 27f;
    const float Acceleration = 0.6f;

    public float Speed { get; private set; }
    public float CurrentSpeed { get { return CurrentState == State.Playing ? Speed : 0f; } }

    public float Distance { get; private set; }
    public int Coins { get; private set; }
    public int RunPowerUps { get; private set; }
    public int Bonus { get; private set; }
    public int Score { get { return Mathf.FloorToInt(Distance) + Coins * 5 + Bonus; } }

    const float ComboWindow = 3f;
    int _combo;
    float _comboTimer;
    public int BestCombo { get; private set; }
    bool _newBest;
    float _runTime;

    // Shared transient pop-up (near-miss, milestone, ...).
    float _flash;
    string _flashText = "";
    Color _flashColor = Color.white;
    int _nextMilestone = 500;

    void Flash(string text, Color color)
    {
        _flash = 1f;
        _flashText = text;
        _flashColor = color;
    }

    public PlayerController Player { get; private set; }
    public WorldGenerator World { get; private set; }
    public CameraRig Cam { get; private set; }

    PowerUp _power = PowerUp.None;
    float _powerTimer;

    public PowerUp ActivePower { get { return _powerTimer > 0f ? _power : PowerUp.None; } }
    public float PowerTimeLeft { get { return Mathf.Max(0f, _powerTimer); } }
    public int Multiplier { get { return ActivePower == PowerUp.Double ? 2 : 1; } }

    float _hitStopTimer;
    float _coinPulse;
    bool _paused;

    public bool IsPaused { get { return _paused; } }

    public static Color PowerColor(PowerUp p)
    {
        switch (p)
        {
            case PowerUp.Magnet: return new Color(0.2f, 0.82f, 0.95f);
            case PowerUp.Jetpack: return new Color(1f, 0.55f, 0.15f);
            case PowerUp.Double: return new Color(0.32f, 0.9f, 0.36f);
            case PowerUp.Sneakers: return new Color(1f, 0.42f, 0.72f);
            case PowerUp.Shield: return new Color(0.55f, 0.82f, 1f);
            default: return Color.white;
        }
    }

    public static string PowerName(PowerUp p)
    {
        switch (p)
        {
            case PowerUp.Magnet: return "COIN MAGNET";
            case PowerUp.Jetpack: return "JETPACK";
            case PowerUp.Double: return "2X SCORE";
            case PowerUp.Sneakers: return "SUPER SNEAKERS";
            case PowerUp.Shield: return "SHIELD";
            default: return "";
        }
    }

    void Awake()
    {
        Instance = this;
        CurrentState = State.Menu;
        Speed = StartSpeed;
        Time.timeScale = 1f;
        Application.targetFrameRate = 60;
        BuildWorld();

        if (_autoStart)
        {
            _autoStart = false;
            CurrentState = State.Playing;
        }
    }

    void BuildWorld()
    {
        GameObject sun = new GameObject("Sun");
        sun.transform.SetParent(transform);
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        light.color = new Color(1f, 0.96f, 0.87f);
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(44f, 32f, 0f);
        RenderSettings.sun = light;

        GameObject fill = new GameObject("Fill Light");
        fill.transform.SetParent(transform);
        Light fillLight = fill.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.intensity = 0.42f;
        fillLight.color = new Color(0.62f, 0.72f, 0.96f);
        fillLight.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(34f, -148f, 0f);

        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader != null)
        {
            Material sky = new Material(skyShader);
            sky.SetColor("_SkyTint", new Color(0.48f, 0.6f, 0.82f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.45f, 0.49f));
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetFloat("_SunSize", 0.05f);
            sky.SetFloat("_Exposure", 1.3f);
            RenderSettings.skybox = sky;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.7f, 0.76f, 0.86f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.28f, 0.28f, 0.3f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.64f, 0.72f, 0.84f);
        RenderSettings.fogStartDistance = 90f;
        RenderSettings.fogEndDistance = 230f;

        GameObject playerGo = new GameObject("Player");
        Player = playerGo.AddComponent<PlayerController>();

        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = new Color(0.52f, 0.74f, 0.96f);
        cam.fieldOfView = 68f;
        cam.farClipPlane = 240f;
        Cam = camGo.AddComponent<CameraRig>();
        Cam.target = playerGo.transform;
        SetupPostFX(camGo);

        GameObject worldGo = new GameObject("World");
        worldGo.transform.SetParent(transform);
        World = worldGo.AddComponent<WorldGenerator>();
        World.player = Player;

        gameObject.AddComponent<AudioManager>();
        gameObject.AddComponent<MenuUI>();
    }

    void SetupPostFX(GameObject camGo)
    {
        PPResourcesHolder holder = Resources.Load<PPResourcesHolder>("PPResourcesHolder");
        if (holder == null || holder.resources == null) return;

        PostProcessLayer layer = camGo.AddComponent<PostProcessLayer>();
        layer.Init(holder.resources);
        layer.volumeTrigger = camGo.transform;
        layer.volumeLayer = 1;
        layer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;

        PostProcessProfile profile = ScriptableObject.CreateInstance<PostProcessProfile>();

        Bloom bloom = profile.AddSettings<Bloom>();
        bloom.active = true;
        bloom.intensity.Override(3.4f);
        bloom.threshold.Override(1.0f);
        bloom.softKnee.Override(0.55f);

        ColorGrading grading = profile.AddSettings<ColorGrading>();
        grading.active = true;
        grading.contrast.Override(11f);
        grading.saturation.Override(16f);
        grading.temperature.Override(5f);

        Vignette vignette = profile.AddSettings<Vignette>();
        vignette.active = true;
        vignette.intensity.Override(0.3f);
        vignette.smoothness.Override(0.45f);

        GameObject volGo = new GameObject("PostFX Volume");
        volGo.transform.SetParent(transform);
        PostProcessVolume volume = volGo.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.profile = profile;
    }

    void Update()
    {
        if (_hitStopTimer > 0f)
        {
            _hitStopTimer -= Time.unscaledDeltaTime;
            if (_hitStopTimer <= 0f) Time.timeScale = 1f;
        }
        if (_coinPulse > 0f) _coinPulse -= Time.unscaledDeltaTime * 3.2f;
        if (_flash > 0f) _flash -= Time.unscaledDeltaTime * 1.5f;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            TogglePause();

        if (CurrentState == State.Playing && !_paused)
        {
            Speed = Mathf.Min(MaxSpeed, Speed + Acceleration * Time.deltaTime);
            Distance += Speed * Time.deltaTime * Multiplier;
            _runTime += Time.deltaTime;
            if (_powerTimer > 0f) _powerTimer -= Time.deltaTime;
            if (_comboTimer > 0f)
            {
                _comboTimer -= Time.deltaTime;
                if (_comboTimer <= 0f) _combo = 0;
            }
            while (Distance >= _nextMilestone)
            {
                Bonus += 25;
                Flash(_nextMilestone + " m", new Color(1f, 0.85f, 0.3f));
                _nextMilestone += 500;
            }
        }
    }

    /// <summary>Awards a near-miss: builds the dodge combo and grants escalating bonus points.</summary>
    public void NearMiss()
    {
        if (CurrentState != State.Playing || _paused) return;
        _combo++;
        if (_combo > BestCombo) BestCombo = _combo;
        _comboTimer = ComboWindow;
        Bonus += 10 * _combo * Multiplier;
        Flash(_combo > 1 ? "NEAR MISS  x" + _combo : "NEAR MISS", new Color(0.4f, 1f, 0.5f));
        if (Cam != null && GameData.ScreenShake) Cam.Shake(0.04f);
    }

    public void StartRun()
    {
        if (CurrentState == State.Menu) CurrentState = State.Playing;
    }

    public void TogglePause()
    {
        if (CurrentState != State.Playing) return;
        if (_paused) Resume(); else Pause();
    }

    void Pause()
    {
        _paused = true;
        Time.timeScale = 0f;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Click();
            AudioManager.Instance.SetMusicPaused(true);
        }
    }

    void Resume()
    {
        _paused = false;
        // _hitStopTimer is never active during normal play, so restoring to 1 is safe.
        Time.timeScale = 1f;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Click();
            AudioManager.Instance.SetMusicPaused(false);
        }
    }

    // Auto-pause when the app is backgrounded or loses focus, so a run never
    // continues unattended. Resume stays manual (via the pause overlay).
    void OnApplicationFocus(bool focus)
    {
        if (!focus && CurrentState == State.Playing && !_paused) Pause();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && CurrentState == State.Playing && !_paused) Pause();
    }

    public void AddCoin()
    {
        Coins += Multiplier;
        _coinPulse = 1f;
        if (AudioManager.Instance != null) AudioManager.Instance.Coin();
    }

    public void ActivatePower(PowerUp p)
    {
        _power = p;
        RunPowerUps++;
        switch (p)
        {
            case PowerUp.Magnet: _powerTimer = 7f; break;
            case PowerUp.Jetpack: _powerTimer = 4.5f; break;
            case PowerUp.Double: _powerTimer = 9f; break;
            case PowerUp.Sneakers: _powerTimer = 8f; break;
            case PowerUp.Shield: _powerTimer = 10f; break;
        }
        if (Cam != null) Cam.Shake(0.12f);
        if (AudioManager.Instance != null) AudioManager.Instance.PowerUp();
    }

    /// <summary>If a shield is active, spends it (saving the run) and returns true.</summary>
    public bool ConsumeShield()
    {
        if (ActivePower != PowerUp.Shield) return false;
        _powerTimer = 0f;
        _power = PowerUp.None;
        if (Cam != null && GameData.ScreenShake) Cam.Shake(0.2f);
        HitStop(0.08f, 0.15f);
        if (AudioManager.Instance != null) AudioManager.Instance.PowerUp();
        return true;
    }

    public void HitStop(float duration, float scale)
    {
        Time.timeScale = scale;
        _hitStopTimer = duration;
    }

    public void GameOver()
    {
        if (CurrentState == State.GameOver) return;
        CurrentState = State.GameOver;

        if (Player != null)
        {
            Effects.Crash(Player.transform.position, new Color(0.93f, 0.36f, 0.16f));
            if (Cam != null && GameData.ScreenShake) Cam.Shake(0.6f);
            HitStop(0.16f, 0.05f);
        }
        if (AudioManager.Instance != null) AudioManager.Instance.Crash();

        GameData.Coins += Coins;
        _newBest = Score > GameData.HighScore;
        if (_newBest) GameData.HighScore = Score;
        Missions.ReportRun(Coins, Mathf.FloorToInt(Distance), RunPowerUps);
        GameData.SeenTutorial = true;
        GameData.Save();
    }

    public void PlayAgain()
    {
        _autoStart = true;
        Restart();
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ----------------------------------------------------------------- HUD
    GUIStyle _hud, _sub, _hudRight, _big, _mid, _btn, _power_;
    Texture2D _panel, _pill;
    bool _uiReady;

    void InitUI()
    {
        _uiReady = true;
        _panel = MenuUI.Tex(new Color(0f, 0f, 0f, 0.78f));
        _pill = MenuUI.Tex(new Color(0f, 0f, 0f, 0.55f));

        int s = Mathf.Clamp(Screen.height, 360, 2200);

        _hud = new GUIStyle { fontStyle = FontStyle.Bold };
        _hud.fontSize = Mathf.RoundToInt(s * 0.046f);
        _hud.normal.textColor = Color.white;

        _sub = new GUIStyle(_hud);
        _sub.fontSize = Mathf.RoundToInt(s * 0.034f);
        _sub.normal.textColor = new Color(1f, 0.85f, 0.3f);

        _hudRight = new GUIStyle(_hud) { alignment = TextAnchor.UpperRight };

        _big = new GUIStyle(_hud) { alignment = TextAnchor.MiddleCenter };
        _big.fontSize = Mathf.RoundToInt(s * 0.082f);

        _mid = new GUIStyle(_hud) { fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter };
        _mid.fontSize = Mathf.RoundToInt(s * 0.038f);

        _btn = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        _btn.fontSize = Mathf.RoundToInt(s * 0.04f);

        _power_ = new GUIStyle(_hud) { alignment = TextAnchor.MiddleCenter };
        _power_.fontSize = Mathf.RoundToInt(s * 0.042f);
    }

    void OnGUI()
    {
        if (!_uiReady) InitUI();
        float pad = Screen.height * 0.03f;

        if (CurrentState == State.Playing || CurrentState == State.GameOver)
        {
            GUI.Label(new Rect(pad, pad, Screen.width * 0.6f, Screen.height * 0.1f),
                      "SCORE  " + Score, _hud);
            int sub0 = _sub.fontSize;
            if (_coinPulse > 0f)
                _sub.fontSize = Mathf.RoundToInt(sub0 * (1f + Mathf.Clamp01(_coinPulse) * 0.4f));
            GUI.Label(new Rect(pad, pad + Screen.height * 0.062f, Screen.width * 0.6f, Screen.height * 0.08f),
                      "COINS  " + Coins, _sub);
            _sub.fontSize = sub0;
            float pauseBtn = Screen.height * 0.07f;
            GUI.Label(new Rect(Screen.width * 0.35f, pad, Screen.width * 0.65f - pad * 2f - pauseBtn,
                               Screen.height * 0.1f),
                      "BEST  " + GameData.HighScore, _hudRight);

            if (CurrentState == State.Playing && !_paused &&
                GUI.Button(new Rect(Screen.width - pad - pauseBtn, pad, pauseBtn, pauseBtn), "II", _btn))
                TogglePause();

            if (CurrentState == State.Playing && !_paused && ActivePower != PowerUp.None)
            {
                float pw = Screen.width * 0.42f;
                Rect pr = new Rect((Screen.width - pw) * 0.5f, pad, pw, Screen.height * 0.072f);
                GUI.DrawTexture(pr, _pill);
                Color prev = _power_.normal.textColor;
                _power_.normal.textColor = PowerColor(ActivePower);
                GUI.Label(pr, PowerName(ActivePower) + "   " + Mathf.CeilToInt(PowerTimeLeft) + "s", _power_);
                _power_.normal.textColor = prev;
            }

            if (CurrentState == State.Playing && !_paused && _flash > 0f && _flashText.Length > 0)
            {
                Color prevC = GUI.color;
                float a = Mathf.Clamp01(_flash);
                GUI.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, a);
                int fs0 = _big.fontSize;
                _big.fontSize = Mathf.RoundToInt(fs0 * (0.7f + a * 0.35f));
                GUI.Label(new Rect(0f, Screen.height * 0.28f, Screen.width, Screen.height * 0.12f),
                          _flashText, _big);
                _big.fontSize = fs0;
                GUI.color = prevC;
            }

            if (CurrentState == State.Playing && !_paused && !GameData.SeenTutorial && _runTime < 5f)
            {
                Color prevC = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(5f - _runTime));
                float bw = Mathf.Min(Screen.width * 0.9f, 760f);
                Rect br = new Rect((Screen.width - bw) * 0.5f, Screen.height * 0.8f, bw, Screen.height * 0.08f);
                GUI.DrawTexture(br, _pill);
                GUI.Label(br, "← →  switch lanes      ↑  jump      ↓  slide      (swipe works too)", _mid);
                GUI.color = prevC;
            }
        }

        if (CurrentState == State.GameOver)
        {
            float pw = Mathf.Min(Screen.width * 0.7f, 640f);
            float ph = Screen.height * 0.62f;
            Rect p = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
            GUI.DrawTexture(p, _panel);

            GUI.Label(new Rect(p.x, p.y + ph * 0.07f, pw, ph * 0.15f), "RUN OVER", _big);
            if (_newBest)
            {
                Color prevC = _mid.normal.textColor;
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);
                _mid.normal.textColor = new Color(1f, 0.85f * pulse + 0.15f, 0.25f);
                GUI.Label(new Rect(p.x, p.y + ph * 0.20f, pw, ph * 0.07f), "★  NEW BEST!  ★", _mid);
                _mid.normal.textColor = prevC;
            }
            GUI.Label(new Rect(p.x, p.y + ph * 0.27f, pw, ph * 0.09f), "Score   " + Score, _mid);
            GUI.Label(new Rect(p.x, p.y + ph * 0.37f, pw, ph * 0.09f), "Coins this run   " + Coins, _mid);
            GUI.Label(new Rect(p.x, p.y + ph * 0.47f, pw, ph * 0.09f), "Total coins   " + GameData.Coins, _mid);
            GUI.Label(new Rect(p.x, p.y + ph * 0.57f, pw, ph * 0.09f), "Best   " + GameData.HighScore, _mid);
            if (BestCombo > 1)
            {
                Color prevC = _mid.normal.textColor;
                _mid.normal.textColor = new Color(0.45f, 1f, 0.55f);
                GUI.Label(new Rect(p.x, p.y + ph * 0.665f, pw, ph * 0.08f),
                          "Best dodge combo   x" + BestCombo, _mid);
                _mid.normal.textColor = prevC;
            }

            float bw = pw * 0.42f, bh = ph * 0.15f, gap = pw * 0.06f;
            if (GUI.Button(new Rect(p.x + (pw - bw * 2f - gap) * 0.5f, p.y + ph * 0.78f, bw, bh),
                           "PLAY AGAIN", _btn))
                PlayAgain();
            if (GUI.Button(new Rect(p.x + (pw - bw * 2f - gap) * 0.5f + bw + gap, p.y + ph * 0.78f, bw, bh),
                           "HOME", _btn))
                Restart();
        }

        if (_paused && CurrentState == State.Playing)
        {
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _pill);

            float pw = Mathf.Min(Screen.width * 0.7f, 600f);
            float ph = Screen.height * 0.5f;
            Rect p = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
            GUI.DrawTexture(p, _panel);

            GUI.Label(new Rect(p.x, p.y + ph * 0.1f, pw, ph * 0.2f), "PAUSED", _big);
            GUI.Label(new Rect(p.x, p.y + ph * 0.33f, pw, ph * 0.12f),
                      "Score   " + Score, _mid);

            float bw = pw * 0.62f, bh = ph * 0.17f;
            if (GUI.Button(new Rect(p.x + (pw - bw) * 0.5f, p.y + ph * 0.52f, bw, bh), "RESUME", _btn))
                Resume();
            if (GUI.Button(new Rect(p.x + (pw - bw) * 0.5f, p.y + ph * 0.73f, bw, bh), "HOME", _btn))
                Restart();
        }
    }
}
