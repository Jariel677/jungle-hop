using UnityEngine;
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

    public enum PowerUp { None, Magnet, Jetpack, Double, Sneakers }

    public static readonly float[] LaneX = { -2.6f, 0f, 2.6f };

    const float StartSpeed = 9f;
    const float MaxSpeed = 27f;
    const float Acceleration = 0.6f;

    public float Speed { get; private set; }
    public float CurrentSpeed { get { return CurrentState == State.Playing ? Speed : 0f; } }

    public float Distance { get; private set; }
    public int Coins { get; private set; }
    public int RunPowerUps { get; private set; }
    public int Score { get { return Mathf.FloorToInt(Distance) + Coins * 5; } }

    public PlayerController Player { get; private set; }
    public WorldGenerator World { get; private set; }
    public CameraRig Cam { get; private set; }

    PowerUp _power = PowerUp.None;
    float _powerTimer;

    public PowerUp ActivePower { get { return _powerTimer > 0f ? _power : PowerUp.None; } }
    public float PowerTimeLeft { get { return Mathf.Max(0f, _powerTimer); } }
    public int Multiplier { get { return ActivePower == PowerUp.Double ? 2 : 1; } }

    float _hitStopTimer;

    public static Color PowerColor(PowerUp p)
    {
        switch (p)
        {
            case PowerUp.Magnet: return new Color(0.2f, 0.82f, 0.95f);
            case PowerUp.Jetpack: return new Color(1f, 0.55f, 0.15f);
            case PowerUp.Double: return new Color(0.32f, 0.9f, 0.36f);
            case PowerUp.Sneakers: return new Color(1f, 0.42f, 0.72f);
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

        GameObject worldGo = new GameObject("World");
        worldGo.transform.SetParent(transform);
        World = worldGo.AddComponent<WorldGenerator>();
        World.player = Player;

        gameObject.AddComponent<MenuUI>();
    }

    void Update()
    {
        if (_hitStopTimer > 0f)
        {
            _hitStopTimer -= Time.unscaledDeltaTime;
            if (_hitStopTimer <= 0f) Time.timeScale = 1f;
        }

        if (CurrentState == State.Playing)
        {
            Speed = Mathf.Min(MaxSpeed, Speed + Acceleration * Time.deltaTime);
            Distance += Speed * Time.deltaTime * Multiplier;
            if (_powerTimer > 0f) _powerTimer -= Time.deltaTime;
        }
    }

    public void StartRun()
    {
        if (CurrentState == State.Menu) CurrentState = State.Playing;
    }

    public void AddCoin()
    {
        Coins += Multiplier;
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
        }
        if (Cam != null) Cam.Shake(0.12f);
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

        GameData.Coins += Coins;
        if (Score > GameData.HighScore) GameData.HighScore = Score;
        Missions.ReportRun(Coins, Mathf.FloorToInt(Distance), RunPowerUps);
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
            GUI.Label(new Rect(pad, pad + Screen.height * 0.062f, Screen.width * 0.6f, Screen.height * 0.08f),
                      "COINS  " + Coins, _sub);
            GUI.Label(new Rect(Screen.width * 0.35f, pad, Screen.width * 0.65f - pad, Screen.height * 0.1f),
                      "BEST  " + GameData.HighScore, _hudRight);

            if (CurrentState == State.Playing && ActivePower != PowerUp.None)
            {
                float pw = Screen.width * 0.42f;
                Rect pr = new Rect((Screen.width - pw) * 0.5f, pad, pw, Screen.height * 0.072f);
                GUI.DrawTexture(pr, _pill);
                Color prev = _power_.normal.textColor;
                _power_.normal.textColor = PowerColor(ActivePower);
                GUI.Label(pr, PowerName(ActivePower) + "   " + Mathf.CeilToInt(PowerTimeLeft) + "s", _power_);
                _power_.normal.textColor = prev;
            }
        }

        if (CurrentState == State.GameOver)
        {
            float pw = Mathf.Min(Screen.width * 0.7f, 640f);
            float ph = Screen.height * 0.62f;
            Rect p = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
            GUI.DrawTexture(p, _panel);

            GUI.Label(new Rect(p.x, p.y + ph * 0.07f, pw, ph * 0.15f), "RUN OVER", _big);
            GUI.Label(new Rect(p.x, p.y + ph * 0.27f, pw, ph * 0.09f), "Score   " + Score, _mid);
            GUI.Label(new Rect(p.x, p.y + ph * 0.37f, pw, ph * 0.09f), "Coins this run   " + Coins, _mid);
            GUI.Label(new Rect(p.x, p.y + ph * 0.47f, pw, ph * 0.09f), "Total coins   " + GameData.Coins, _mid);
            GUI.Label(new Rect(p.x, p.y + ph * 0.57f, pw, ph * 0.09f), "Best   " + GameData.HighScore, _mid);

            float bw = pw * 0.42f, bh = ph * 0.15f, gap = pw * 0.06f;
            if (GUI.Button(new Rect(p.x + (pw - bw * 2f - gap) * 0.5f, p.y + ph * 0.78f, bw, bh),
                           "PLAY AGAIN", _btn))
                PlayAgain();
            if (GUI.Button(new Rect(p.x + (pw - bw * 2f - gap) * 0.5f + bw + gap, p.y + ph * 0.78f, bw, bh),
                           "HOME", _btn))
                Restart();
        }
    }
}
