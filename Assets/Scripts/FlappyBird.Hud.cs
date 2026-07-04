using UnityEngine;

/// <summary>
/// <see cref="FlappyBird"/> heads-up display, reskinned with the CraftPix jungle
/// UI sprites (loaded in FlappyBird.Art.cs): title screen, in-run score/banana
/// readouts, pause menu, settings/skins panel, and game-over panel. Interaction
/// state (pause, equip) lives on the core file. Same partial class.
///
/// Layout is proportional and eyeballed — tweak the fractions if a panel or
/// button sits off. Sprites degrade gracefully: a missing texture just draws
/// nothing, the click areas still work.
/// </summary>
public partial class FlappyBird
{
    GUIStyle _big, _mid, _small, _banana;

    void OnGUI()
    {
        if (_big == null)
        {
            _big = new GUIStyle(GUI.skin.label) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            _banana = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        }
        _big.normal.textColor = Color.white;
        _mid.normal.textColor = Color.white;
        _small.normal.textColor = Color.white;
        _banana.normal.textColor = new Color(1f, 0.88f, 0.2f);

        float w = Screen.width, h = Screen.height;

        // Settings / skins overlay — reachable from Ready, Game Over, or the pause
        // menu, so you don't have to be mid-run to open it. Takes over the screen.
        if (_inSettings)
        {
            DimScreen(w, h, 0.55f);
            DrawSettingsMenu(w, h);
            return;
        }

        switch (_state)
        {
            case State.Ready: DrawReady(w, h); break;
            case State.Playing: DrawInRun(w, h); break;
            case State.GameOver: DrawGameOver(w, h); break;
            case State.Paused:
                DrawInRun(w, h);
                DimScreen(w, h, 0.5f);
                DrawPauseMenu(w, h);
                break;
        }

        // Corner button — pause while playing, open the menu otherwise.
        if (_state == State.Playing || _state == State.Ready || _state == State.GameOver)
            Sprite(new Rect(w - PauseBtnSize - PauseBtnMargin, PauseBtnY, PauseBtnSize, PauseBtnSize),
                   _state == State.Playing ? "btn/pause" : "btn/menu");
    }

    void DrawReady(float w, float h)
    {
        SpriteFill(new Rect(0f, 0f, w, h), "menu/bg");

        // Game title as text (replaces the pack's logo art) — edit GameTitle to rename.
        GUI.Label(new Rect(0f, h * 0.16f, w, 90f), GameTitle, _big);

        float pbw = Mathf.Min(w * 0.3f, 260f);
        Sprite(new Rect(w * 0.5f - pbw * 0.5f, h * 0.56f, pbw, pbw * 0.42f), "btn/play");
        GUI.Label(new Rect(0f, h * 0.72f, w, 34f), "SPACE / CLICK / TAP to play", _small);
    }

    void DrawInRun(float w, float h)
    {
        GUI.Label(new Rect(0f, h * 0.06f, w, 70f), _score.ToString(), _big);
        // "BANANA COUNTER  N" top-right, left of the round pause button (matches the ref).
        float px = w - PauseBtnSize - PauseBtnMargin;
        GUI.Label(new Rect(px - 350f, PauseBtnY + 8f, 340f, 44f), "BANANA COUNTER  " + _bananas, _banana);
    }

    void DrawGameOver(float w, float h)
    {
        float pw = Mathf.Min(w * 0.72f, 560f), ph = Mathf.Min(h * 0.78f, 600f);
        float px = (w - pw) * 0.5f, py = (h - ph) * 0.5f;

        Sprite(new Rect(px, py, pw, ph), "you_lose/bg");
        Sprite(new Rect(px + pw * 0.1f, py + ph * 0.02f, pw * 0.8f, ph * 0.24f), "you_lose/header");

        GUI.Label(new Rect(px, py + ph * 0.40f, pw, 40f), "Score  " + _score, _mid);
        GUI.Label(new Rect(px, py + ph * 0.50f, pw, 34f), "Bananas  " + _bananas, _small);
        GUI.Label(new Rect(px, py + ph * 0.58f, pw, 34f), "Best  " + _best, _small);

        float bw = pw * 0.42f;
        Sprite(new Rect(px + (pw - bw) * 0.5f, py + ph * 0.68f, bw, bw * 0.42f), "btn/restart");
        GUI.Label(new Rect(px, py + ph * 0.90f, pw, 30f), "SPACE / CLICK to play again", _small);
    }

    void DrawPauseMenu(float w, float h)
    {
        float pw = Mathf.Min(w * 0.62f, 520f), ph = Mathf.Min(h * 0.8f, 620f);
        float px = (w - pw) * 0.5f, py = (h - ph) * 0.5f;

        Sprite(new Rect(px, py, pw, ph), "pause/bg");
        Sprite(new Rect(px + pw * 0.08f, py + ph * 0.02f, pw * 0.84f, ph * 0.24f), "pause/header");

        float bw = pw * 0.52f, bh = bw * 0.42f, bx = px + (pw - bw) * 0.5f;
        if (SpriteButton(new Rect(bx, py + ph * 0.42f, bw, bh), "btn/play"))
        {
            _state = State.Playing;
            if (DebugScore) Debug.Log("[JungleHop] resume");
        }
        if (SpriteButton(new Rect(bx, py + ph * 0.64f, bw, bh), "btn/settings"))
            _inSettings = true;
    }

    void DrawSettingsMenu(float w, float h)
    {
        float pw = Mathf.Min(w * 0.82f, 720f), ph = Mathf.Min(h * 0.86f, 680f);
        float px = (w - pw) * 0.5f, py = (h - ph) * 0.5f;

        Sprite(new Rect(px, py, pw, ph), "settings/bg");
        GUI.Label(new Rect(px, py + ph * 0.10f, pw, 40f), "MONKEY SKINS", _mid);
        GUI.Label(new Rect(px, py + ph * 0.10f + 40f, pw, 30f), "Bananas collected:  " + _totalBananas, _small);

        // Close (top-right of the panel).
        if (SpriteButton(new Rect(px + pw - 70f, py + 20f, 50f, 50f), "btn/close_2"))
            _inSettings = false;

        float rowW = pw * 0.72f, rowH = 52f, cx = px + (pw - rowW) * 0.5f, y = py + ph * 0.30f;
        for (int i = 0; i < Monkeys.Length; i++)
        {
            var m = Monkeys[i];
            bool unlocked = _totalBananas >= m.need;

            Color prev = GUI.color;
            GUI.color = unlocked ? m.color : new Color(m.color.r, m.color.g, m.color.b, 0.35f);
            GUI.DrawTexture(new Rect(cx + 6f, y + 10f, 32f, 32f), Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(cx + 50f, y, 160f, rowH), m.name + " monkey", _small);

            var btn = new Rect(cx + rowW - 160f, y + 6f, 152f, 40f);
            if (i == _equipped)
                GUI.Label(btn, "Equipped", _small);
            else if (unlocked)
            {
                if (GUI.Button(btn, "Equip")) EquipSkin(i);
            }
            else
                GUI.Label(btn, "Need " + m.need, _small);

            y += rowH + 10f;
        }
    }

    /// <summary>Draws a translucent black fill over the whole screen.</summary>
    void DimScreen(float w, float h, float alpha) => DimScreen(new Rect(0f, 0f, w, h), alpha);

    /// <summary>Draws a translucent black fill over the given rect.</summary>
    void DimScreen(Rect r, float alpha)
    {
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }
}
