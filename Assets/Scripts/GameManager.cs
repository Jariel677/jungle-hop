using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central controller for the endless runner. Builds the world at runtime,
/// tracks state/score/speed, and draws the HUD.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum State { Ready, Playing, GameOver }
    public State CurrentState { get; private set; }

    /// <summary>World-space X of the three lanes (left, middle, right).</summary>
    public static readonly float[] LaneX = { -2.6f, 0f, 2.6f };

    const float StartSpeed = 9f;
    const float MaxSpeed = 27f;
    const float Acceleration = 0.6f;

    public float Speed { get; private set; }

    /// <summary>Forward speed actually applied — zero unless the run is live.</summary>
    public float CurrentSpeed { get { return CurrentState == State.Playing ? Speed : 0f; } }

    public float Distance { get; private set; }
    public int Coins { get; private set; }
    public int Score { get { return Mathf.FloorToInt(Distance) + Coins * 5; } }
    public int HighScore { get; private set; }

    public PlayerController Player { get; private set; }
    public WorldGenerator World { get; private set; }

    void Awake()
    {
        Instance = this;
        CurrentState = State.Ready;
        Speed = StartSpeed;
        HighScore = PlayerPrefs.GetInt("subway_highscore", 0);
        Application.targetFrameRate = 60;
        BuildWorld();
    }

    void BuildWorld()
    {
        RenderSettings.ambientLight = new Color(0.56f, 0.59f, 0.64f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.52f, 0.74f, 0.96f);
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 170f;

        GameObject sun = new GameObject("Sun");
        sun.transform.SetParent(transform);
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.97f, 0.92f);
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48f, -26f, 0f);

        GameObject playerGo = new GameObject("Player");
        Player = playerGo.AddComponent<PlayerController>();

        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.52f, 0.74f, 0.96f);
        cam.fieldOfView = 68f;
        cam.farClipPlane = 240f;
        CameraRig rig = camGo.AddComponent<CameraRig>();
        rig.target = playerGo.transform;

        GameObject worldGo = new GameObject("World");
        worldGo.transform.SetParent(transform);
        World = worldGo.AddComponent<WorldGenerator>();
        World.player = Player;
    }

    void Update()
    {
        switch (CurrentState)
        {
            case State.Ready:
                if (StartPressed()) CurrentState = State.Playing;
                break;
            case State.Playing:
                Speed = Mathf.Min(MaxSpeed, Speed + Acceleration * Time.deltaTime);
                Distance += Speed * Time.deltaTime;
                break;
            case State.GameOver:
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                    Restart();
                break;
        }
    }

    static bool StartPressed()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)
            || Input.GetKeyDown(KeyCode.W) || Input.GetMouseButtonDown(0)
            || Input.touchCount > 0;
    }

    public void AddCoin() { Coins++; }

    public void GameOver()
    {
        if (CurrentState == State.GameOver) return;
        CurrentState = State.GameOver;
        if (Score > HighScore)
        {
            HighScore = Score;
            PlayerPrefs.SetInt("subway_highscore", HighScore);
            PlayerPrefs.Save();
        }
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ----------------------------------------------------------------- HUD
    GUIStyle _hud, _sub, _hudRight, _big, _mid, _btn;
    Texture2D _panel;
    bool _uiReady;

    void InitUI()
    {
        _uiReady = true;
        _panel = new Texture2D(1, 1);
        _panel.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.74f));
        _panel.Apply();

        int s = Mathf.Clamp(Screen.height, 360, 2200);

        _hud = new GUIStyle { fontStyle = FontStyle.Bold };
        _hud.fontSize = Mathf.RoundToInt(s * 0.046f);
        _hud.normal.textColor = Color.white;

        _sub = new GUIStyle(_hud);
        _sub.fontSize = Mathf.RoundToInt(s * 0.034f);
        _sub.normal.textColor = new Color(1f, 0.85f, 0.3f);

        _hudRight = new GUIStyle(_hud);
        _hudRight.alignment = TextAnchor.UpperRight;

        _big = new GUIStyle(_hud);
        _big.fontSize = Mathf.RoundToInt(s * 0.085f);
        _big.alignment = TextAnchor.MiddleCenter;

        _mid = new GUIStyle(_hud);
        _mid.fontStyle = FontStyle.Normal;
        _mid.fontSize = Mathf.RoundToInt(s * 0.036f);
        _mid.alignment = TextAnchor.MiddleCenter;

        _btn = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        _btn.fontSize = Mathf.RoundToInt(s * 0.04f);
    }

    Rect Panel(float wFrac, float hFrac)
    {
        float pw = Mathf.Min(Screen.width * wFrac, 720f);
        float ph = Screen.height * hFrac;
        Rect r = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
        GUI.DrawTexture(r, _panel);
        return r;
    }

    void OnGUI()
    {
        if (!_uiReady) InitUI();
        float pad = Screen.height * 0.03f;

        if (CurrentState != State.Ready)
        {
            GUI.Label(new Rect(pad, pad, Screen.width * 0.6f, Screen.height * 0.1f),
                      "SCORE  " + Score, _hud);
            GUI.Label(new Rect(pad, pad + Screen.height * 0.062f, Screen.width * 0.6f, Screen.height * 0.08f),
                      "COINS  " + Coins, _sub);
            GUI.Label(new Rect(Screen.width * 0.35f, pad, Screen.width * 0.65f - pad, Screen.height * 0.1f),
                      "BEST  " + HighScore, _hudRight);
        }

        if (CurrentState == State.Ready)
        {
            Rect p = Panel(0.74f, 0.56f);
            GUI.Label(new Rect(p.x, p.y + p.height * 0.10f, p.width, p.height * 0.22f),
                      "SUBWAY RUNNER", _big);
            GUI.Label(new Rect(p.x, p.y + p.height * 0.36f, p.width, p.height * 0.40f),
                      "Dodge the blocks. Grab the coins.\n\n" +
                      "←  →   change lane\n" +
                      "↑ / Space   jump\n" +
                      "↓   slide under bars\n\n" +
                      "(swipe or drag works too)", _mid);
            GUI.Label(new Rect(p.x, p.y + p.height * 0.82f, p.width, p.height * 0.14f),
                      "Press SPACE or Click to START", _mid);
        }
        else if (CurrentState == State.GameOver)
        {
            Rect p = Panel(0.6f, 0.5f);
            GUI.Label(new Rect(p.x, p.y + p.height * 0.10f, p.width, p.height * 0.22f),
                      "GAME OVER", _big);
            GUI.Label(new Rect(p.x, p.y + p.height * 0.38f, p.width, p.height * 0.12f),
                      "Score   " + Score, _mid);
            GUI.Label(new Rect(p.x, p.y + p.height * 0.51f, p.width, p.height * 0.12f),
                      "Best    " + HighScore, _mid);
            float bw = p.width * 0.52f, bh = p.height * 0.17f;
            if (GUI.Button(new Rect(p.x + (p.width - bw) * 0.5f, p.y + p.height * 0.70f, bw, bh),
                           "RESTART", _btn))
                Restart();
        }
    }
}
