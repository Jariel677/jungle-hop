using UnityEngine;

/// <summary>
/// Pre-game menu: Home, Shop (characters &amp; boards), Missions and Settings.
/// Drawn only while the game is in the Menu state; the live game world shows
/// behind it.
/// </summary>
public class MenuUI : MonoBehaviour
{
    enum Page { Home, Shop, Missions, Settings }

    Page _page = Page.Home;
    int _shopTab;
    Vector2 _shopScroll;
    string _toast = "";
    float _toastTimer;

    GUIStyle _title, _body, _bodyLeft, _coin, _btn, _btnBig, _tab, _tabOn, _toastSt;
    Texture2D _panel, _barBg, _barFill;
    bool _ready;

    public static Texture2D Tex(Color c)
    {
        Texture2D t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void InitUI()
    {
        _ready = true;
        _panel = Tex(new Color(0.06f, 0.07f, 0.1f, 0.94f));
        _barBg = Tex(new Color(1f, 1f, 1f, 0.13f));
        _barFill = Tex(new Color(0.32f, 0.86f, 0.42f, 0.96f));

        int s = Mathf.Clamp(Screen.height, 360, 2200);

        _title = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _title.fontSize = Mathf.RoundToInt(s * 0.07f);
        _title.normal.textColor = Color.white;

        _body = new GUIStyle(_title) { fontStyle = FontStyle.Normal };
        _body.fontSize = Mathf.RoundToInt(s * 0.032f);
        _body.normal.textColor = new Color(0.85f, 0.87f, 0.92f);

        _bodyLeft = new GUIStyle(_body) { alignment = TextAnchor.MiddleLeft };
        _bodyLeft.fontStyle = FontStyle.Bold;
        _bodyLeft.normal.textColor = Color.white;

        _coin = new GUIStyle(_title);
        _coin.fontSize = Mathf.RoundToInt(s * 0.04f);
        _coin.normal.textColor = new Color(1f, 0.85f, 0.3f);

        _btn = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        _btn.fontSize = Mathf.RoundToInt(s * 0.032f);

        _btnBig = new GUIStyle(_btn);
        _btnBig.fontSize = Mathf.RoundToInt(s * 0.05f);

        _tab = new GUIStyle(_btn);
        _tabOn = new GUIStyle(_btn);
        _tabOn.normal.textColor = new Color(1f, 0.85f, 0.3f);

        _toastSt = new GUIStyle(_title);
        _toastSt.fontSize = Mathf.RoundToInt(s * 0.04f);
        _toastSt.normal.textColor = new Color(1f, 0.9f, 0.45f);
    }

    void Toast(string msg) { _toast = msg; _toastTimer = 2.4f; }

    float BtnH { get { return Screen.height * 0.07f; } }

    void OnGUI()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentState != GameManager.State.Menu) return;
        if (!_ready) InitUI();

        float pw = Mathf.Min(Screen.width * 0.88f, 760f);
        float ph = Mathf.Min(Screen.height * 0.92f, 1180f);
        Rect area = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
        GUI.DrawTexture(area, _panel);

        GUILayout.BeginArea(new Rect(area.x + pw * 0.07f, area.y + ph * 0.045f,
                                     pw * 0.86f, ph * 0.91f));
        switch (_page)
        {
            case Page.Home: DrawHome(); break;
            case Page.Shop: DrawShop(); break;
            case Page.Missions: DrawMissions(); break;
            case Page.Settings: DrawSettings(); break;
        }
        GUILayout.EndArea();

        if (_toastTimer > 0f)
        {
            _toastTimer -= Time.unscaledDeltaTime;
            GUI.Label(new Rect(0f, Screen.height * 0.85f, Screen.width, Screen.height * 0.08f),
                      _toast, _toastSt);
        }
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentState != GameManager.State.Menu) return;
        if (_page == Page.Home && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)))
            gm.StartRun();
    }

    void Bar(float frac)
    {
        Rect r = GUILayoutUtility.GetRect(10f, Screen.height * 0.022f);
        GUI.DrawTexture(r, _barBg);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), _barFill);
    }

    // ----------------------------------------------------------------- home
    void DrawHome()
    {
        GUILayout.Space(Screen.height * 0.015f);
        GUILayout.Label("SUBWAY RUNNER", _title);
        GUILayout.Label("◆  " + GameData.Coins + "      Best  " + GameData.HighScore, _coin);
        GUILayout.Space(Screen.height * 0.03f);

        if (GUILayout.Button("PLAY", _btnBig, GUILayout.Height(Screen.height * 0.12f)))
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Click();
            GameManager.Instance.StartRun();
        }

        GUILayout.Space(Screen.height * 0.02f);
        if (GUILayout.Button("SHOP", _btn, GUILayout.Height(BtnH))) _page = Page.Shop;
        GUILayout.Space(Screen.height * 0.012f);
        if (GUILayout.Button("MISSIONS", _btn, GUILayout.Height(BtnH))) _page = Page.Missions;
        GUILayout.Space(Screen.height * 0.012f);
        if (GUILayout.Button("SETTINGS", _btn, GUILayout.Height(BtnH))) _page = Page.Settings;

        GUILayout.Space(Screen.height * 0.022f);
        if (GameData.DailyAvailable)
        {
            if (GUILayout.Button("★  CLAIM DAILY REWARD", _btn, GUILayout.Height(BtnH)))
            {
                int r = GameData.ClaimDaily();
                Toast("Daily reward:  +" + r + " coins!");
            }
        }
        else
        {
            GUILayout.Label("Daily reward claimed — back tomorrow", _body);
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("← →  lane     ↑  jump     ↓  slide     (swipe works too)", _body);
    }

    // ----------------------------------------------------------------- shop
    void DrawShop()
    {
        GUILayout.Space(Screen.height * 0.01f);
        GUILayout.Label("SHOP", _title);
        GUILayout.Label("◆  " + GameData.Coins + " coins", _coin);
        GUILayout.Space(Screen.height * 0.014f);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("CHARACTERS", _shopTab == 0 ? _tabOn : _tab, GUILayout.Height(BtnH * 0.85f)))
            _shopTab = 0;
        if (GUILayout.Button("BOARDS", _shopTab == 1 ? _tabOn : _tab, GUILayout.Height(BtnH * 0.85f)))
            _shopTab = 1;
        GUILayout.EndHorizontal();
        GUILayout.Space(Screen.height * 0.014f);

        _shopScroll = GUILayout.BeginScrollView(_shopScroll);
        if (_shopTab == 0)
        {
            foreach (CharacterDef c in Catalog.Characters)
            {
                int r = ShopRow(c.name, c.cost, GameData.CharacterOwned(c.id),
                                GameData.EquippedCharacter == c.id);
                if (r == 1) BuyCharacter(c);
                else if (r == 2) EquipCharacter(c);
            }
        }
        else
        {
            foreach (BoardDef b in Catalog.Boards)
            {
                int r = ShopRow(b.name, b.cost, GameData.BoardOwned(b.id),
                                GameData.EquippedBoard == b.id);
                if (r == 1) BuyBoard(b);
                else if (r == 2) EquipBoard(b);
            }
        }
        GUILayout.EndScrollView();

        GUILayout.Space(Screen.height * 0.01f);
        if (GUILayout.Button("BACK", _btn, GUILayout.Height(BtnH))) _page = Page.Home;
    }

    /// <summary>Draws one shop row. Returns 0 = none, 1 = buy, 2 = equip.</summary>
    int ShopRow(string name, int cost, bool owned, bool equipped)
    {
        int result = 0;
        GUILayout.BeginHorizontal(GUILayout.Height(BtnH));
        GUILayout.Label("  " + name, _bodyLeft);
        GUILayout.FlexibleSpace();
        float w = Screen.width * 0.22f;
        if (equipped)
        {
            GUILayout.Label("EQUIPPED ", _coin, GUILayout.Width(w));
        }
        else if (owned)
        {
            if (GUILayout.Button("EQUIP", _btn, GUILayout.Width(w), GUILayout.Height(BtnH * 0.82f)))
                result = 2;
        }
        else
        {
            if (GUILayout.Button(cost + " ◆", _btn, GUILayout.Width(w), GUILayout.Height(BtnH * 0.82f)))
                result = 1;
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(Screen.height * 0.01f);
        return result;
    }

    void BuyCharacter(CharacterDef c)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Click();
        if (GameData.Coins >= c.cost)
        {
            GameData.Coins -= c.cost;
            GameData.OwnCharacter(c.id);
            GameData.Save();
            Toast("Unlocked " + c.name + "!");
        }
        else Toast("Not enough coins");
    }

    void EquipCharacter(CharacterDef c)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Click();
        GameData.EquippedCharacter = c.id;
        GameData.Save();
        if (GameManager.Instance.Player != null)
            GameManager.Instance.Player.RebuildAppearance();
        Toast("Equipped " + c.name);
    }

    void BuyBoard(BoardDef b)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Click();
        if (GameData.Coins >= b.cost)
        {
            GameData.Coins -= b.cost;
            GameData.OwnBoard(b.id);
            GameData.Save();
            Toast("Unlocked " + b.name + "!");
        }
        else Toast("Not enough coins");
    }

    void EquipBoard(BoardDef b)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Click();
        GameData.EquippedBoard = b.id;
        GameData.Save();
        if (GameManager.Instance.Player != null)
            GameManager.Instance.Player.RebuildAppearance();
        Toast("Equipped " + b.name);
    }

    // ------------------------------------------------------------- missions
    void DrawMissions()
    {
        GUILayout.Space(Screen.height * 0.01f);
        GUILayout.Label("MISSIONS", _title);
        GUILayout.Label("◆  " + GameData.Coins + " coins", _coin);
        GUILayout.Space(Screen.height * 0.02f);

        Missions.Mission[] ms = Missions.Current();
        for (int i = 0; i < ms.Length; i++)
        {
            Missions.Mission m = ms[i];
            GUILayout.Label("  " + m.Label, _bodyLeft);
            Bar(m.target > 0 ? m.progress / (float)m.target : 1f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("  " + Mathf.Min(m.progress, m.target) + " / " + m.target +
                            "      reward  " + m.reward + " ◆", _body);
            GUILayout.FlexibleSpace();
            if (m.Done)
            {
                if (GUILayout.Button("CLAIM", _btn, GUILayout.Width(Screen.width * 0.2f),
                                     GUILayout.Height(BtnH * 0.82f)))
                {
                    int r = Missions.Claim(i);
                    Toast("Mission complete!  +" + r + " coins");
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(Screen.height * 0.022f);
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("BACK", _btn, GUILayout.Height(BtnH))) _page = Page.Home;
    }

    // ------------------------------------------------------------- settings
    void DrawSettings()
    {
        GUILayout.Space(Screen.height * 0.01f);
        GUILayout.Label("SETTINGS", _title);
        GUILayout.Space(Screen.height * 0.03f);

        if (GUILayout.Button("Screen Shake:   " + (GameData.ScreenShake ? "ON" : "OFF"),
                             _btn, GUILayout.Height(BtnH)))
        {
            GameData.ScreenShake = !GameData.ScreenShake;
            GameData.Save();
        }

        GUILayout.Space(Screen.height * 0.012f);
        if (GUILayout.Button("Music:   " + (GameData.Music ? "ON" : "OFF"),
                             _btn, GUILayout.Height(BtnH)))
        {
            GameData.Music = !GameData.Music;
            GameData.Save();
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusic(GameData.Music);
        }

        GUILayout.Space(Screen.height * 0.012f);
        if (GUILayout.Button("Sound Effects:   " + (GameData.Sfx ? "ON" : "OFF"),
                             _btn, GUILayout.Height(BtnH)))
        {
            GameData.Sfx = !GameData.Sfx;
            GameData.Save();
        }

        GUILayout.Space(Screen.height * 0.012f);
        if (GUILayout.Button("Hazard Cues:   " + (GameData.HighContrast ? "ON" : "OFF"),
                             _btn, GUILayout.Height(BtnH)))
        {
            GameData.HighContrast = !GameData.HighContrast;
            GameData.Save();
            if (GameManager.Instance != null && GameManager.Instance.World != null)
                GameManager.Instance.World.RefreshHazardCues();
            Toast(GameData.HighContrast
                ? "Hazard cues on — markers show jump / slide / dodge"
                : "Hazard cues off");
        }

        GUILayout.Space(Screen.height * 0.04f);
        GUILayout.Label("Tip: collect coins to unlock characters and boards in the shop.", _body);

        GUILayout.Space(Screen.height * 0.03f);
        if (GUILayout.Button("RESET PROGRESS", _btn, GUILayout.Height(BtnH)))
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Toast("Progress reset");
            GameManager.Instance.Restart();
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("BACK", _btn, GUILayout.Height(BtnH))) _page = Page.Home;
    }
}
