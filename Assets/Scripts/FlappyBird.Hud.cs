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

    // Wardrobe (skin picker): procedural rounded panel/cards, a tintable monkey
    // avatar (fur + baked face), and a lock glyph. All generated once and cached.
    Texture2D _wPanel; int _wPanelW = -1, _wPanelH = -1;
    Texture2D _wCard, _wCardSel; int _wCardW = -1, _wCardH = -1;
    Texture2D _wFur, _wFace, _wLock;
    Texture2D _wArrowL, _wArrowR;   // wardrobe page arrows
    int _wardPage;                  // current wardrobe page
    GUIStyle _wTitle, _wName, _wTag;

    Font _uiFont;
    /// <summary>Bundled game font (Resources/Fonts/GameFont); null until Unity imports it.</summary>
    Font UiFont => _uiFont != null ? _uiFont : (_uiFont = Resources.Load<Font>("Fonts/GameFont"));

    Font _titleFont;
    /// <summary>Jungle display face (Resources/Fonts/TitleFont, Luckiest Guy) for the
    /// wordmark, score, and big call-outs; null until Unity imports it.</summary>
    Font TitleFont => _titleFont != null ? _titleFont : (_titleFont = Resources.Load<Font>("Fonts/TitleFont"));

    // ---- "Message in the clouds": every 3rd banana floats a motivating line up in
    // the sky on a drifting cloud. State lives here; the trigger fires from the
    // banana-pickup code in FlappyBird.cs (same partial class).
    const float CloudMsgDur = 3.2f;
    int _lastBananaBucket;   // _bananas/3 when the last message fired (drives "every 3rd")
    string _cloudMsg;        // active message (null = nothing showing)
    float _cloudMsgT;        // seconds of life left on the active message
    float _cloudMsgDrift;    // grows while shown; gives the cloud a slow drift + rise
    Texture2D _cloudBanner;  // cloud sprite the message rides on
    GUIStyle _cloudStyle;

    static readonly string[] CloudMsgs = {
        "You've got this!", "Keep swinging!", "Go bananas!", "Unstoppable!",
        "Jungle legend!",   "Nice moves!",    "Monkey magic!", "Swing higher!",
        "On fire!",         "Banana power!",
    };

    /// <summary>Picks a motivating line and starts its float-up. Called every 3rd banana.</summary>
    void TriggerCloudMessage()
    {
        _cloudMsg = CloudMsgs[Random.Range(0, CloudMsgs.Length)];
        _cloudMsgT = CloudMsgDur;
        _cloudMsgDrift = 0f;
    }

    /// <summary>Draws the active cloud message: a motivating line on a drifting cloud
    /// near the top of the sky, fading in then out. No-op when nothing is showing.</summary>
    void DrawCloudMessage(float w, float h)
    {
        if (_cloudMsgT <= 0f || string.IsNullOrEmpty(_cloudMsg)) return;
        if (_cloudBanner == null) _cloudBanner = Resources.Load<Texture2D>("JungleUI/clouds/2");

        // Fade in over the first 0.3s, out over the last 0.7s.
        float a = Mathf.Clamp01(Mathf.Min((CloudMsgDur - _cloudMsgT) / 0.3f, _cloudMsgT / 0.7f));
        float scale = Mathf.Clamp(h / 1080f, 0.7f, 1.7f);
        float bw = Mathf.Min(w * 0.62f, 560f * scale);
        float bh = bw * 0.52f;
        float x = (w - bw) * 0.5f + _cloudMsgDrift * 18f; // slow rightward drift
        float y = h * 0.11f - _cloudMsgDrift * 6f;        // gently rises as it drifts

        Color prev = GUI.color;
        if (_cloudBanner != null)
        {
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(new Rect(x, y, bw, bh), _cloudBanner, ScaleMode.ScaleToFit);
        }

        if (_cloudStyle == null)
        {
            _cloudStyle = new GUIStyle(_mid) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold };
            Font tf = TitleFont; if (tf != null) _cloudStyle.font = tf;
        }
        _cloudStyle.fontSize = Mathf.RoundToInt(30f * scale);
        Rect tr = new Rect(x, y + bh * 0.22f, bw, bh * 0.5f);

        // Soft shadow, then the jungle-green text (GUI.color carries the fade alpha).
        _cloudStyle.normal.textColor = Color.black;
        GUI.color = new Color(1f, 1f, 1f, a * 0.32f);
        GUI.Label(new Rect(tr.x + 2f, tr.y + 2f, tr.width, tr.height), _cloudMsg, _cloudStyle);
        _cloudStyle.normal.textColor = new Color(0.16f, 0.42f, 0.15f);
        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.Label(tr, _cloudMsg, _cloudStyle);
        GUI.color = prev;
    }

    void OnGUI()
    {
        if (_big == null)
        {
            _big = new GUIStyle(GUI.skin.label) { fontSize = 54, alignment = TextAnchor.MiddleCenter };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            _banana = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleRight };
            Font f = UiFont;
            if (f != null) foreach (var st in new[] { _big, _mid, _small, _banana }) st.font = f;
            // The big wordmark / score / call-outs use the jungle display face.
            Font tf = TitleFont; if (tf != null) _big.font = tf;
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
        DrawActivePowerups(w, h);
        DrawCloudMessage(w, h);
    }

    /// <summary>Top-left stack of the currently active power-ups, each with a
    /// shrinking timer bar (the shield shows a full bar until it is used).</summary>
    void DrawActivePowerups(float w, float h)
    {
        if (_powTex == null)
            _powTex = new[] {
                Resources.Load<Texture2D>("Items/pow_shield"),
                Resources.Load<Texture2D>("Items/pow_slow"),
                Resources.Load<Texture2D>("Items/pow_magnet"),
                Resources.Load<Texture2D>("Items/pow_2x") };

        float scale = Mathf.Clamp(h / 1080f, 0.7f, 1.7f);
        float sz = 58f * scale, pad = 16f * scale;
        float y = pad + 80f * scale; // below the score
        if (_shield)           DrawPowIcon(pad, ref y, sz, _powTex[0], 1f);
        if (_slowTimer > 0f)   DrawPowIcon(pad, ref y, sz, _powTex[1], _slowTimer / PowSlowDur);
        if (_magnetTimer > 0f) DrawPowIcon(pad, ref y, sz, _powTex[2], _magnetTimer / PowMagnetDur);
        if (_x2Timer > 0f)     DrawPowIcon(pad, ref y, sz, _powTex[3], _x2Timer / Pow2xDur);
    }

    void DrawPowIcon(float x, ref float y, float sz, Texture2D tex, float frac)
    {
        if (tex != null) GUI.DrawTexture(new Rect(x, y, sz, sz), tex, ScaleMode.ScaleToFit);
        float bh = 6f, by = y + sz + 3f;
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.4f);
        GUI.DrawTexture(new Rect(x, by, sz, bh), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.86f, 0.32f, 0.95f);
        GUI.DrawTexture(new Rect(x, by, sz * Mathf.Clamp01(frac), bh), Texture2D.whiteTexture);
        GUI.color = prev;
        y += sz + 20f;
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

    /// <summary>Talking Tom–style wardrobe: a rounded panel with a large preview of
    /// the equipped monkey and a row of tappable skin cards, each showing a monkey
    /// avatar tinted to that skin's colour (locked ones greyed with a lock + price).</summary>
    void DrawSettingsMenu(float w, float h)
    {
        float scale = Mathf.Clamp(h / 1080f, 0.7f, 1.7f);
        EnsureWardrobeStyles(scale);
        EnsureWardrobeGlyphs();

        float pw = Mathf.Min(w * 0.86f, 900f);
        float ph = Mathf.Min(h * 0.92f, 1000f);
        float px = (w - pw) * 0.5f, py = (h - ph) * 0.5f;

        // Rounded panel — rebuilt only when its pixel size changes.
        int ipw = Mathf.RoundToInt(pw), iph = Mathf.RoundToInt(ph);
        if (_wPanel == null || _wPanelW != ipw || _wPanelH != iph)
        {
            if (_wPanel != null) Destroy(_wPanel);
            _wPanel = MakeRounded(ipw, iph, 34f * scale,
                                  new Color(0.10f, 0.13f, 0.11f, 0.95f),
                                  new Color(1f, 0.86f, 0.40f, 0.60f), Mathf.Max(2.5f, 5f * scale));
            _wPanelW = ipw; _wPanelH = iph;
        }

        Color pc = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(new Rect(px + 4f, py + 7f, pw, ph), _wPanel);
        GUI.color = pc;
        GUI.DrawTexture(new Rect(px, py, pw, ph), _wPanel);

        ShadowLabel(new Rect(px, py + ph * 0.035f, pw, 54f * scale), "WARDROBE", _wTitle);

        float cs = 48f * scale;
        if (SpriteButton(new Rect(px + pw - cs - 22f * scale, py + 20f * scale, cs, cs), "btn/close_2"))
            _inSettings = false;

        // Big preview of the equipped monkey.
        var eq = Monkeys[_equipped];
        float av = Mathf.Min(ph * 0.26f, pw * 0.34f);
        float avy = py + ph * 0.12f;
        DrawMonkey(new Rect(px + (pw - av) * 0.5f, avy, av, av), eq.color, false);
        ShadowLabel(new Rect(px, avy + av - 4f * scale, pw, 36f * scale), eq.name.ToUpper() + " MONKEY", _wName);
        _wTag.normal.textColor = new Color(1f, 0.88f, 0.40f);
        DrawBananaCount(new Rect(px, avy + av + 30f * scale, pw, 30f * scale), _totalBananas, 26f * scale, _wTag, " collected");

        // Wardrobe — one page of monkey cards with arrow navigation.
        const int perPage = 4;
        int pageCount = (Monkeys.Length + perPage - 1) / perPage;
        _wardPage = Mathf.Clamp(_wardPage, 0, pageCount - 1);
        int start = _wardPage * perPage;
        int count = Mathf.Min(perPage, Monkeys.Length - start);

        int n = perPage; // size the layout for a full page so cards stay aligned
        float rowY = py + ph * 0.55f, rowH = ph * 0.33f, cgap = 14f * scale;
        float cardW = (pw * 0.80f - cgap * (n - 1)) / n; // leave room for the arrows
        float cardH = Mathf.Min(rowH, cardW * 1.28f);
        float startX = px + (pw - (cardW * n + cgap * (n - 1))) * 0.5f;

        int icw = Mathf.RoundToInt(cardW), ich = Mathf.RoundToInt(cardH);
        if (_wCard == null || _wCardW != icw || _wCardH != ich)
        {
            if (_wCard != null) Destroy(_wCard);
            if (_wCardSel != null) Destroy(_wCardSel);
            _wCard    = MakeRounded(icw, ich, 22f * scale, new Color(1f, 1f, 1f, 0.06f),
                                    new Color(1f, 1f, 1f, 0.16f), Mathf.Max(2f, 3f * scale));
            _wCardSel = MakeRounded(icw, ich, 22f * scale, new Color(1f, 0.85f, 0.35f, 0.16f),
                                    new Color(1f, 0.88f, 0.40f, 0.95f), Mathf.Max(3f, 5f * scale));
            _wCardW = icw; _wCardH = ich;
        }

        for (int k = 0; k < count; k++)
        {
            int i = start + k;
            var m = Monkeys[i];
            bool unlocked = _totalBananas >= m.need;
            bool equipped = i == _equipped;
            float cx = startX + k * (cardW + cgap);
            var card = new Rect(cx, rowY, cardW, cardH);

            GUI.DrawTexture(card, equipped ? _wCardSel : _wCard);

            float ai = cardW * 0.58f;
            var ar = new Rect(cx + (cardW - ai) * 0.5f, rowY + cardH * 0.08f, ai, ai);
            DrawMonkey(ar, m.color, !unlocked);
            if (!unlocked)
            {
                float lk = ai * 0.44f;
                GUI.DrawTexture(new Rect(ar.center.x - lk * 0.5f, ar.center.y - lk * 0.5f, lk, lk), _wLock, ScaleMode.ScaleToFit);
            }

            ShadowLabel(new Rect(cx, rowY + cardH * 0.66f, cardW, 26f * scale), m.name, _wName);

            var tagR = new Rect(cx, rowY + cardH * 0.80f, cardW, 26f * scale);
            if (equipped)
            {
                _wTag.normal.textColor = new Color(0.45f, 1f, 0.55f);
                ShadowLabel(tagR, "EQUIPPED", _wTag);
            }
            else if (unlocked)
            {
                _wTag.normal.textColor = new Color(1f, 0.88f, 0.40f);
                ShadowLabel(tagR, "TAP TO WEAR", _wTag);
            }
            else
            {
                _wTag.normal.textColor = new Color(0.88f, 0.88f, 0.92f);
                DrawBananaCount(tagR, m.need, 22f * scale, _wTag, "");
            }

            if (unlocked && !equipped && GUI.Button(card, GUIContent.none, GUIStyle.none))
                EquipSkin(i);
        }

        // Page arrows + indicator.
        if (_wArrowL == null) _wArrowL = Resources.Load<Texture2D>("Items/arrow_left");
        if (_wArrowR == null) _wArrowR = Resources.Load<Texture2D>("Items/arrow_right");
        float aSz = 60f * scale, aY = rowY + cardH * 0.5f - aSz * 0.5f;
        if (_wardPage > 0)
        {
            var lr = new Rect(px + 6f * scale, aY, aSz, aSz);
            if (_wArrowL != null) GUI.DrawTexture(lr, _wArrowL, ScaleMode.ScaleToFit);
            if (GUI.Button(lr, GUIContent.none, GUIStyle.none)) _wardPage--;
        }
        if (_wardPage < pageCount - 1)
        {
            var rr = new Rect(px + pw - aSz - 6f * scale, aY, aSz, aSz);
            if (_wArrowR != null) GUI.DrawTexture(rr, _wArrowR, ScaleMode.ScaleToFit);
            if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) _wardPage++;
        }
        _wTag.normal.textColor = new Color(0.92f, 0.92f, 0.96f);
        ShadowLabel(new Rect(px, rowY + cardH + 8f * scale, pw, 26f * scale), "Page " + (_wardPage + 1) + " / " + pageCount, _wTag);
    }

    void EnsureWardrobeStyles(float scale)
    {
        if (_wTitle == null)
        {
            _wTitle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            _wName  = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            _wTag   = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            Font f = UiFont;
            if (f != null) foreach (var st in new[] { _wTitle, _wName, _wTag }) st.font = f;
        }
        _wTitle.fontSize = Mathf.RoundToInt(40f * scale);
        _wTitle.normal.textColor = new Color(1f, 0.88f, 0.40f);
        _wName.fontSize  = Mathf.RoundToInt(23f * scale);
        _wName.normal.textColor = Color.white;
        _wTag.fontSize   = Mathf.RoundToInt(17f * scale);
    }

    void EnsureWardrobeGlyphs()
    {
        if (_wFur  == null) _wFur  = MakeMonkeyFur(160);
        if (_wFace == null) _wFace = MakeMonkeyFace(160);
        if (_wLock == null) _wLock = MakeLock(96);
        if (_bananaIcon == null) _bananaIcon = MakeBananaIcon(72);
    }

    /// <summary>Draws the monkey avatar: fur silhouette tinted to the skin colour,
    /// then the baked face on top. Greyed out when locked.</summary>
    void DrawMonkey(Rect r, Color fur, bool locked)
    {
        Color prev = GUI.color;
        GUI.color = locked ? new Color(0.42f, 0.42f, 0.46f, 0.85f) : fur;
        GUI.DrawTexture(r, _wFur, ScaleMode.ScaleToFit);
        GUI.color = locked ? new Color(1f, 1f, 1f, 0.40f) : Color.white;
        GUI.DrawTexture(r, _wFace, ScaleMode.ScaleToFit);
        GUI.color = prev;
    }

    /// <summary>Banana icon + count (+ optional suffix), centred in the rect.</summary>
    void DrawBananaCount(Rect r, int count, float iconH, GUIStyle st, string suffix)
    {
        string s = count.ToString() + suffix;
        float tw = st.CalcSize(new GUIContent(s)).x;
        float total = iconH + 6f + tw;
        float x = r.x + (r.width - total) * 0.5f;
        float cy = r.y + r.height * 0.5f;
        GUI.DrawTexture(new Rect(x, cy - iconH * 0.5f, iconH, iconH), _bananaIcon, ScaleMode.ScaleToFit);
        var a = st.alignment; st.alignment = TextAnchor.MiddleLeft;
        ShadowLabel(new Rect(x + iconH + 6f, r.y, tw + 6f, r.height), s, st);
        st.alignment = a;
    }

    /// <summary>Label with a soft drop shadow for legibility over busy art.</summary>
    void ShadowLabel(Rect r, string t, GUIStyle st)
    {
        Color prev = st.normal.textColor;
        st.normal.textColor = new Color(0f, 0f, 0f, 0.5f);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.8f, r.width, r.height), t, st);
        st.normal.textColor = prev;
        GUI.Label(r, t, st);
    }

    /// <summary>Rounded-rectangle texture (fill + soft rim) at an exact pixel size.</summary>
    Texture2D MakeRounded(int w, int h, float radius, Color fill, Color rim, float rimT)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        float hw = w * 0.5f, hh = h * 0.5f, aa = 1.4f;
        float r = Mathf.Min(radius, Mathf.Min(hw, hh) - 1f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - hw) - (hw - r);
                float qy = Mathf.Abs(y + 0.5f - hh) - (hh - r);
                float d  = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                           + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                float cov    = Mathf.Clamp01(0.5f - d / aa);
                float border = Mathf.Clamp01(1f - Mathf.Abs(d + rimT) / rimT);
                Color c = Color.Lerp(fill, rim, border * 0.7f);
                c.a = Mathf.Lerp(fill.a, rim.a, border * 0.7f) * cov;
                px[y * w + x] = c;
            }
        }
        tex.SetPixels(px); tex.Apply();
        return tex;
    }

    /// <summary>Monkey fur silhouette (head + two ears), white for tinting.</summary>
    Texture2D MakeMonkeyFur(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pxs = new Color[s * s];
        float aa = 2f / s;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float nx = (x + 0.5f) / s * 2f - 1f;
                float ny = (y + 0.5f) / s * 2f - 1f;      // +y = top (Unity texture origin)
                float dHead = Mathf.Sqrt(nx * nx + (ny + 0.05f) * (ny + 0.05f)) - 0.62f;
                float dEarL = Mathf.Sqrt((nx + 0.50f) * (nx + 0.50f) + (ny - 0.52f) * (ny - 0.52f)) - 0.24f;
                float dEarR = Mathf.Sqrt((nx - 0.50f) * (nx - 0.50f) + (ny - 0.52f) * (ny - 0.52f)) - 0.24f;
                float d = Mathf.Min(dHead, Mathf.Min(dEarL, dEarR));
                pxs[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d / aa));
            }
        }
        tex.SetPixels(pxs); tex.Apply();
        return tex;
    }

    /// <summary>Baked monkey face (tan muzzle + inner ears, dark eyes + nose).</summary>
    Texture2D MakeMonkeyFace(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pxs = new Color[s * s];
        float aa = 2f / s;
        Color tan = new Color(0.93f, 0.76f, 0.55f, 1f);
        Color dark = new Color(0.11f, 0.09f, 0.10f, 1f);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float nx = (x + 0.5f) / s * 2f - 1f;
                float ny = (y + 0.5f) / s * 2f - 1f;
                Color c = new Color(0f, 0f, 0f, 0f);
                float dMuz = Mathf.Sqrt(nx * nx + (ny + 0.30f) * (ny + 0.30f)) - 0.40f;
                float dIeL = Mathf.Sqrt((nx + 0.50f) * (nx + 0.50f) + (ny - 0.52f) * (ny - 0.52f)) - 0.12f;
                float dIeR = Mathf.Sqrt((nx - 0.50f) * (nx - 0.50f) + (ny - 0.52f) * (ny - 0.52f)) - 0.12f;
                float tanCov = Mathf.Max(Mathf.Clamp01(0.5f - dMuz / aa),
                               Mathf.Max(Mathf.Clamp01(0.5f - dIeL / aa), Mathf.Clamp01(0.5f - dIeR / aa)));
                c = Over(c, new Color(tan.r, tan.g, tan.b, tanCov));
                float dEyeL = Mathf.Sqrt((nx + 0.19f) * (nx + 0.19f) + (ny - 0.12f) * (ny - 0.12f)) - 0.095f;
                float dEyeR = Mathf.Sqrt((nx - 0.19f) * (nx - 0.19f) + (ny - 0.12f) * (ny - 0.12f)) - 0.095f;
                float dNose = Mathf.Sqrt(nx * nx + (ny + 0.10f) * (ny + 0.10f)) - 0.06f;
                float darkCov = Mathf.Max(Mathf.Clamp01(0.5f - dEyeL / aa),
                                Mathf.Max(Mathf.Clamp01(0.5f - dEyeR / aa), Mathf.Clamp01(0.5f - dNose / aa)));
                c = Over(c, new Color(dark.r, dark.g, dark.b, darkCov));
                pxs[y * s + x] = c;
            }
        }
        tex.SetPixels(pxs); tex.Apply();
        return tex;
    }

    /// <summary>Padlock glyph (shackle + rounded body + keyhole), near-white.</summary>
    Texture2D MakeLock(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pxs = new Color[s * s];
        float aa = 2f / s;
        Color body = new Color(0.96f, 0.96f, 0.99f, 1f);
        Color hole = new Color(0.16f, 0.16f, 0.19f, 1f);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float nx = (x + 0.5f) / s * 2f - 1f;
                float ny = (y + 0.5f) / s * 2f - 1f;
                float ring = Mathf.Abs(Mathf.Sqrt(nx * nx + (ny - 0.30f) * (ny - 0.30f)) - 0.32f) - 0.085f;
                float shackle = (ny > 0.14f) ? Mathf.Clamp01(0.5f - ring / aa) : 0f;
                float qx = Mathf.Abs(nx) - (0.36f - 0.12f);
                float qy = Mathf.Abs(ny + 0.32f) - (0.30f - 0.12f);
                float db = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                           + Mathf.Min(Mathf.Max(qx, qy), 0f) - 0.12f;
                float bodyCov = Mathf.Clamp01(0.5f - db / aa);
                float cov = Mathf.Max(shackle, bodyCov);
                Color c = new Color(body.r, body.g, body.b, cov);
                float dk = Mathf.Sqrt(nx * nx + (ny + 0.30f) * (ny + 0.30f)) - 0.07f;
                float kh = Mathf.Clamp01(0.5f - dk / aa) * bodyCov;
                c = Color.Lerp(c, new Color(hole.r, hole.g, hole.b, cov), kh);
                pxs[y * s + x] = c;
            }
        }
        tex.SetPixels(pxs); tex.Apply();
        return tex;
    }

    /// <summary>Alpha "over" composite of src onto dst (premultiplied maths).</summary>
    Color Over(Color dst, Color src)
    {
        float a = src.a + dst.a * (1f - src.a);
        if (a <= 0.0001f) return new Color(0f, 0f, 0f, 0f);
        float r = (src.r * src.a + dst.r * dst.a * (1f - src.a)) / a;
        float g = (src.g * src.a + dst.g * dst.a * (1f - src.a)) / a;
        float b = (src.b * src.a + dst.b * dst.a * (1f - src.a)) / a;
        return new Color(r, g, b, a);
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
