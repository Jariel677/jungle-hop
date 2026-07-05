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
    Texture2D _pillTex; int _pillW = -1, _pillH = -1;  // cached banana-counter pill background
    Texture2D _bananaIcon;                             // procedural banana glyph for the counter

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
        DrawBananaChip(w, h);
    }

    /// <summary>Top-right banana tally: a rounded translucent pill with a gold rim,
    /// a banana glyph, and the count — sized to the digits and the resolution, and
    /// vertically centred against the round pause button.</summary>
    void DrawBananaChip(float w, float h)
    {
        float scale = Mathf.Clamp(h / 1080f, 0.75f, 1.7f);
        int   ch    = Mathf.RoundToInt(56f * scale);   // pill height
        float pad   = Mathf.Round(16f * scale);
        float iconS = Mathf.Round(ch - pad * 1.1f);
        float gap   = Mathf.Round(8f * scale);

        _banana.fontSize  = Mathf.RoundToInt(30f * scale);
        _banana.alignment = TextAnchor.MiddleLeft;
        string label = _bananas.ToString();
        float textW  = _banana.CalcSize(new GUIContent(label)).x;
        int   cw     = Mathf.RoundToInt(pad + iconS + gap + textW + pad);

        // Rebuild the pill only when its pixel size changes (i.e. a new digit appears).
        if (_pillTex == null || _pillW != cw || _pillH != ch)
        {
            if (_pillTex != null) Destroy(_pillTex);
            _pillTex = MakePill(cw, ch);
            _pillW = cw; _pillH = ch;
        }
        if (_bananaIcon == null) _bananaIcon = MakeBananaIcon(72);

        float x = w - PauseBtnSize - PauseBtnMargin - cw - Mathf.Round(14f * scale);
        float y = PauseBtnY + (PauseBtnSize - ch) * 0.5f;

        // Soft drop shadow, then the pill, glyph, and count.
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.22f);
        GUI.DrawTexture(new Rect(x + 2f, y + 3f, cw, ch), _pillTex);
        GUI.color = prev;
        GUI.DrawTexture(new Rect(x, y, cw, ch), _pillTex);
        GUI.DrawTexture(new Rect(x + pad, y + (ch - iconS) * 0.5f, iconS, iconS), _bananaIcon, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(x + pad + iconS + gap, y, textW + 4f, ch), label, _banana);
    }

    /// <summary>Builds a stadium/pill texture (dark translucent fill, soft gold rim)
    /// at an exact pixel size, so corners stay crisp without stretching.</summary>
    Texture2D MakePill(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        float hw = w * 0.5f, hh = h * 0.5f, r = hh - 1f, aa = 1.3f;
        float bt = Mathf.Max(2f, h * 0.09f);                    // rim band thickness
        Color fill = new Color(0.05f, 0.07f, 0.04f, 0.60f);     // dark translucent
        Color rim  = new Color(1f, 0.85f, 0.35f, 1f);           // warm gold edge
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - hw) - (hw - r);
                float qy = Mathf.Abs(y + 0.5f - hh) - (hh - r);
                float d  = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                           + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;      // signed distance, <0 inside
                float cov    = Mathf.Clamp01(0.5f - d / aa);            // outer anti-aliased coverage
                float border = Mathf.Clamp01(1f - Mathf.Abs(d + bt) / bt);
                Color c = Color.Lerp(fill, rim, border * 0.5f);
                c.a = Mathf.Lerp(fill.a, 0.95f, border * 0.5f) * cov;
                px[y * w + x] = c;
            }
        }
        tex.SetPixels(px); tex.Apply();
        return tex;
    }

    /// <summary>Builds a leaning banana glyph as the difference of two circles
    /// (a crescent), gold with a darker rim. Transparent elsewhere.</summary>
    Texture2D MakeBananaIcon(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];
        float ang = -0.52f, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);  // ~ -30 deg lean
        float oR = 0.92f, iR = 0.80f;                                  // outer / inner circle radii
        Vector2 iC = new Vector2(0.42f, 0.30f);                        // inner circle offset -> crescent
        Color body  = new Color(1f, 0.80f, 0.12f, 1f);
        Color shade = new Color(0.80f, 0.50f, 0.05f, 1f);
        float aa = 2f / s;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float nx = (x + 0.5f) / s * 2f - 1f;
                float ny = (y + 0.5f) / s * 2f - 1f;
                float rx = nx * ca - ny * sa, ry = nx * sa + ny * ca;
                float dOut = Mathf.Sqrt(rx * rx + ry * ry) - oR;
                float dIn  = Mathf.Sqrt((rx - iC.x) * (rx - iC.x) + (ry - iC.y) * (ry - iC.y)) - iR;
                float cov  = Mathf.Clamp01(0.5f - dOut / aa) * Mathf.Clamp01(0.5f + dIn / aa);
                if (cov <= 0f) { px[y * s + x] = new Color(0f, 0f, 0f, 0f); continue; }
                Color c = Color.Lerp(shade, body, Mathf.Clamp01(-dOut / 0.16f)); // dark rim near outer edge
                c.a = cov;
                px[y * s + x] = c;
            }
        }
        tex.SetPixels(px); tex.Apply();
        return tex;
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

        // Stack the buttons from a fixed start with a guaranteed gap, so the resume
        // and settings buttons never touch — even on short/wide game views where the
        // old ph-fraction positions used to overlap.
        float bw = Mathf.Min(pw * 0.52f, 300f), bh = bw * 0.40f, bx = px + (pw - bw) * 0.5f;
        float gap = Mathf.Max(bh * 0.35f, 22f);
        float by = py + ph * 0.34f;
        if (SpriteButton(new Rect(bx, by, bw, bh), "btn/play"))
        {
            _state = State.Playing;
            if (DebugScore) Debug.Log("[JungleHop] resume");
        }
        by += bh + gap;
        if (SpriteButton(new Rect(bx, by, bw, bh), "btn/settings"))
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
