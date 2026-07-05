using UnityEngine;

/// <summary>
/// Self-contained Flappy Bird–style game with a jungle reskin: a monkey flaps
/// through gaps between trees over a jungle backdrop, collecting bananas that
/// unlock recolourable skins. Runs the Ready / Playing / Paused / GameOver
/// state machine. Drop this on one empty GameObject and press Play.
///
/// Controls: SPACE / left-click / touch to flap (and to start or restart);
/// Esc or the on-screen button to pause. The class name is kept as
/// <c>FlappyBird</c> so the existing scene reference stays wired.
///
/// This is one partial class split across three files by concern:
///   FlappyBird.cs        — tunables, state, lifecycle, and per-frame simulation
///   FlappyBird.World.cs  — one-time procedural construction of the scene
///   FlappyBird.Hud.cs    — immediate-mode HUD, pause menu, and skins settings
/// </summary>
public partial class FlappyBird : MonoBehaviour
{
    // ---- Tunables -----------------------------------------------------------
    const float OrthoSize = 5f;        // half-height of the view; y spans -5..5
    const float GroundTop = -4f;       // world y of the jungle floor surface
    const float MonkeyX = -3.2f;       // monkey is fixed on x, world scrolls past
    const float MonkeyHalf = 0.28f;    // half-extent used for collision
    const float MonkeySpriteSize = 2.0f; // visual size of the monkey billboard (world units)
    const float FlapAnimDur = 0.22f;   // how long the flap pose + squash-stretch plays

    const float Gravity = -20f;
    const float FlapVelocity = 7f;

    const float ScrollSpeed = 3.6f;    // tree / ground travel speed
    const float TreeHalfW = 0.8f;      // half trunk width
    const float TreeGap = 3.4f;        // vertical opening the monkey flies through
    const float TreeSpacing = 3.7f;    // horizontal distance between tree pairs
    const int TreeCount = 6;
    const float TreeSpriteW = 2.5f;    // flat tree-pipe sprite width (world units)
    const float TreeSpriteH = 14f;     // flat tree-pipe sprite height (trunk runs off-screen)
    const float BananaSpriteSize = 0.85f; // flat cartoon banana sprite size (world units)
    const float PowSpriteSize = 0.95f;    // power-up icon size (world units)
    const float PowSpawnChance = 0.17f;   // chance a placed tree carries a power-up instead of a banana
    const float PowSlowDur = 4.5f, PowMagnetDur = 6f, Pow2xDur = 8f;
    const float SlowFactor = 0.55f;       // scroll-speed multiplier while Slow-Mo is active
    const float MagnetRange = 2.6f;       // world radius the banana magnet pulls from

    const float BananaCollectR = 0.55f; // monkey-to-banana distance that counts as a pickup

    const string GameTitle = "JUNGLE HOP"; // shown on the start screen (edit to rename the game)

    // On-screen pause/menu button (top-right); its click is handled in Update, drawn in OnGUI.
    const float PauseBtnMargin = 14f, PauseBtnY = 14f, PauseBtnSize = 64f;

    // Selectable monkey skins: display name, fur colour, and lifetime bananas needed to unlock.
    static readonly (string name, Color color, int need)[] Monkeys =
    {
        ("Brown",    new Color(0.486f, 0.306f, 0.165f), 0),
        ("Yellow",   new Color(0.941f, 0.776f, 0.157f), 50),
        ("Blue",     new Color(0.314f, 0.549f, 0.949f), 100),
        ("Green",    new Color(0.353f, 0.745f, 0.361f), 300),
        ("Red",      new Color(0.902f, 0.251f, 0.220f), 600),
        ("Orange",   new Color(0.980f, 0.549f, 0.149f), 1000),
        ("Purple",   new Color(0.588f, 0.361f, 0.847f), 1500),
        ("Pink",     new Color(0.980f, 0.549f, 0.722f), 2200),
        ("Cyan",     new Color(0.298f, 0.800f, 0.847f), 3000),
        ("Lime",     new Color(0.620f, 0.847f, 0.251f), 4200),
        ("Gray",     new Color(0.549f, 0.573f, 0.620f), 5800),
        ("Magenta",  new Color(0.847f, 0.204f, 0.549f), 8000),
        ("Teal",     new Color(0.133f, 0.698f, 0.667f), 11000),
        ("Lavender", new Color(0.745f, 0.620f, 0.957f), 15000),
        ("Crimson",  new Color(0.808f, 0.118f, 0.290f), 20000),
        ("Mint",     new Color(0.431f, 0.871f, 0.651f), 26000),
    };

    // Flip to true to log score/banana/skin events to the Editor/Player log for verification.
    const bool DebugScore = false;

    // ---- State --------------------------------------------------------------
    enum State { Ready, Playing, GameOver, Paused }
    State _state = State.Ready;
    bool _inSettings;   // true when the settings sub-panel is open over the pause menu

    Camera _cam;
    Transform _monkey;
    float _monkeyVel;
    float _readyBaseY;

    Transform[] _ground = new Transform[2];
    Transform[] _clouds;
    Transform _backdrop;     // in-game jungle scene behind the gameplay
    Tree[] _trees;

    int _score;
    int _best;
    int _bananas;       // bananas collected this run
    int _totalBananas;  // lifetime bananas (auto-saved on death; drives skin unlocks)
    int _equipped;      // index into Monkeys of the equipped skin
    float _rightEdge;   // world x of the screen's right edge (recomputed per frame)
    float _tileW;       // ground tile width

    Material _treeMat;        // flat cartoon tree-pipe sprite (shared by all pipes)
    Material _bananaMat;      // flat cartoon banana sprite
    Material[] _powMat;       // power-up icon materials (shield / slow / magnet / 2x)
    Transform _shieldBubble;  // follows the monkey while shielded
    bool _shield;
    float _slowTimer, _magnetTimer, _x2Timer, _invuln;
    Texture2D[] _powTex;      // HUD power-up icons (lazy-loaded)
    // Monkey sprite (flat 2D billboard) — frames are per-skin, swapped on equip/flap.
    Renderer _monkeyRenderer;
    Material _monkeyMat;   // the unlit transparent sprite material (frames set as mainTexture)
    Vector3 _monkeyBaseScale;
    Texture2D _idleTex, _flapTex;
    float _flapAnim;    // >0 while the flap pose / squash-stretch plays out

    /// <summary>One tree pair: a root plus its precomputed gap centre, score flag,
    /// and the banana that sits in the gap for the monkey to collect.</summary>
    class Tree
    {
        public Transform root;
        public Transform top;
        public Transform bottom;
        public Transform banana;
        public float gapCenter;
        public float gap;        // the opening this tree was placed with (shrinks past tree 100)
        public bool scored;
        public bool collected;
        public Transform powerup; // optional power-up riding in the gap (instead of a banana)
        public int powerType;     // 0 shield, 1 slow, 2 magnet, 3 x2
        public bool hasPower;     // true while an uncollected power-up rides this tree
    }

    // ---- Setup --------------------------------------------------------------
    void Awake()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep; // keep the display awake while playing
        BuildWorld();
        ResetToReady();
    }

    /// <summary>Loads persisted best score, lifetime bananas, and equipped skin.</summary>
    void LoadPrefs()
    {
        _best = PlayerPrefs.GetInt("FlappyBest", 0);
        _totalBananas = PlayerPrefs.GetInt("TotalBananas", 0);
        _equipped = PlayerPrefs.GetInt("MonkeySkin", 0);
        if (_equipped < 0 || _equipped >= Monkeys.Length) _equipped = 0;
        if (Monkeys[_equipped].need > _totalBananas) _equipped = 0; // safety: never keep a locked skin
    }

    /// <summary>Tree/ground speed ramps gently with score, then caps out. A second,
    /// milder step-up kicks in after tree 100 so long runs stay challenging.</summary>
    float CurrentSpeed()
    {
        float s = ScrollSpeed + Mathf.Min(_score * 0.05f, 2.6f);
        if (_score > 100) s += Mathf.Min((_score - 100) * 0.03f, 1.6f);
        if (_slowTimer > 0f) s *= SlowFactor;
        return s;
    }

    /// <summary>The gap opening — full size until tree 100, then narrows a little
    /// (never below a fair minimum) so it gets a bit harder as well as faster.</summary>
    float CurrentGap()
    {
        if (_score <= 100) return TreeGap;
        return Mathf.Max(TreeGap - (_score - 100) * 0.01f, TreeGap - 0.7f);
    }

    // ---- Lifecycle transitions ---------------------------------------------
    void ResetToReady()
    {
        _state = State.Ready;
        _score = 0;
        _bananas = 0;
        _lastBananaBucket = 0;
        _cloudMsg = null; _cloudMsgT = 0f;
        _monkeyVel = 0f;
        _readyBaseY = 0.4f;
        _monkey.position = new Vector3(MonkeyX, _readyBaseY, 0f);
        _monkey.rotation = Quaternion.identity;
        _flapAnim = 0f;
        _monkey.localScale = _monkeyBaseScale;
        SetMonkeyFrame(false);
        _shield = false; _slowTimer = 0f; _magnetTimer = 0f; _x2Timer = 0f; _invuln = 0f;
        if (_shieldBubble != null) _shieldBubble.gameObject.SetActive(false);

        // Park the pool off-screen to the right, spaced out, with fresh gaps.
        _rightEdge = _cam.orthographicSize * _cam.aspect;
        float x = _rightEdge + 3f;
        for (int i = 0; i < _trees.Length; i++)
        {
            PlaceTree(_trees[i], x);
            _trees[i].scored = true; // no scoring until the run starts
            x += TreeSpacing;
        }
    }

    void StartPlaying()
    {
        _state = State.Playing;
        _monkeyVel = FlapVelocity;
        TriggerFlap();
        for (int i = 0; i < _trees.Length; i++) _trees[i].scored = false;
    }

    void Die()
    {
        _state = State.GameOver;

        // Auto-save: bank this run's bananas into the lifetime total immediately.
        _totalBananas += _bananas;
        PlayerPrefs.SetInt("TotalBananas", _totalBananas);
        if (_score > _best)
        {
            _best = _score;
            PlayerPrefs.SetInt("FlappyBest", _best);
        }
        PlayerPrefs.Save();
        if (DebugScore) Debug.Log($"[JungleHop] autosave: totalBananas={_totalBananas} (+{_bananas} this run)");
    }

    /// <summary>Loads the equipped skin's idle/flap sprite frames and shows the
    /// current one. Called on build and whenever a skin is equipped.</summary>
    void ApplyMonkeyColor()
    {
        if (_monkeyRenderer == null) return;
        string key = Monkeys[_equipped].name.ToLower();
        _idleTex = Resources.Load<Texture2D>("Monkey/" + key + "_idle");
        _flapTex = Resources.Load<Texture2D>("Monkey/" + key + "_flap");
        // A fresh Quad ships with an opaque default material, which would render the
        // sprite's transparent pixels as solid black. Force the unlit transparent
        // sprite material instead.
        if (_monkeyMat == null) _monkeyMat = Art.SpriteMat(_idleTex);
        _monkeyRenderer.sharedMaterial = _monkeyMat;
        SetMonkeyFrame(_flapAnim > 0f);
    }

    /// <summary>Swaps the billboard between the idle and flap frames.</summary>
    void SetMonkeyFrame(bool flap)
    {
        if (_monkeyMat != null) _monkeyMat.mainTexture = (flap && _flapTex != null) ? _flapTex : _idleTex;
    }

    /// <summary>Kicks off the flap pose + squash-stretch (called on every flap).</summary>
    void TriggerFlap()
    {
        _flapAnim = FlapAnimDur;
        SetMonkeyFrame(true);
    }

    /// <summary>Per-frame sprite animation: hold the flap pose briefly, then ease back
    /// to idle with a squash-stretch pop that settles to the base scale.</summary>
    void AnimateMonkey(float dt)
    {
        if (_monkey == null) return;
        if (_flapAnim > 0f)
        {
            _flapAnim -= dt;
            if (_flapAnim <= 0f) { _flapAnim = 0f; SetMonkeyFrame(false); }
        }
        float s = FlapAnimDur > 0f ? _flapAnim / FlapAnimDur : 0f; // 1 right after a flap -> 0
        Vector3 sc = _monkeyBaseScale;
        sc.y *= 1f + 0.20f * s;
        sc.x *= 1f - 0.12f * s;
        _monkey.localScale = sc;
    }

    /// <summary>Equips a skin if it's unlocked, persisting the choice.</summary>
    void EquipSkin(int index)
    {
        if (index < 0 || index >= Monkeys.Length) return;
        if (Monkeys[index].need > _totalBananas) return; // still locked
        _equipped = index;
        PlayerPrefs.SetInt("MonkeySkin", index);
        PlayerPrefs.Save();
        ApplyMonkeyColor();
        if (DebugScore) Debug.Log($"[JungleHop] equipped {Monkeys[index].name} monkey");
    }

    void PlaceTree(Tree t, float x)
    {
        float gap = CurrentGap();
        t.gap = gap;
        float min = GroundTop + gap * 0.5f + 0.6f;
        float max = OrthoSize - gap * 0.5f - 0.6f;
        t.gapCenter = Random.Range(min, max);
        t.root.position = new Vector3(x, 0f, 0.5f);
        t.top.localPosition = new Vector3(0f, t.gapCenter + gap * 0.5f + 7f, 0f);
        t.bottom.localPosition = new Vector3(0f, t.gapCenter - gap * 0.5f - 7f, 0f);
        t.scored = false; // a freshly (re)placed tree is scoreable again; recycling forgot this and capped score at TreeCount

        // The gap carries either a banana or (occasionally) a power-up.
        if (Random.value < PowSpawnChance)
        {
            t.hasPower = true;
            t.powerType = Random.Range(0, 4);
            t.powerup.GetComponent<Renderer>().sharedMaterial = _powMat[t.powerType];
            t.powerup.localPosition = new Vector3(0f, t.gapCenter, -0.5f);
            t.powerup.localScale = new Vector3(PowSpriteSize, PowSpriteSize, 1f);
            t.powerup.gameObject.SetActive(true);
            t.banana.gameObject.SetActive(false);
            t.collected = true;  // no banana on a power-up tree
        }
        else
        {
            t.hasPower = false;
            t.powerup.gameObject.SetActive(false);
            t.banana.localPosition = new Vector3(0f, t.gapCenter, -0.5f);
            t.banana.gameObject.SetActive(true);
            t.collected = false;
        }
    }

    /// <summary>
    /// Recovers from a domain reload during Play mode: Unity nulls non-serialized
    /// fields (_cam, _monkey, _trees) without re-running Awake, so we rebuild from
    /// scratch. Everything the game spawns is parented to this object, so clearing
    /// our children before rebuilding avoids leftover duplicates.
    /// </summary>
    void Rebuild()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
        BuildWorld();
        ResetToReady();
    }

    // ---- Per-frame ----------------------------------------------------------
    void Update()
    {
        // Heal after an edit-while-playing domain reload. This project has
        // "Disable Domain Reload" enabled, so a recompile can wipe an arbitrary
        // subset of these non-serialized fields without re-running Awake.
        if (_cam == null || _monkey == null || _trees == null || _trees[0].root == null)
        {
            Rebuild();
            return;
        }

        _rightEdge = _cam.orthographicSize * _cam.aspect;

        // Corner menu button: pauses while playing, or opens skins/settings when not
        // playing — so the menu is reachable without being mid-run. Its click never
        // doubles as a flap (guarded below).
        bool mouseDown = Input.GetMouseButtonDown(0);
        bool cornerBtn = mouseDown && !_inSettings && PointInPauseButton(Input.mousePosition)
                         && (_state == State.Playing || _state == State.Ready || _state == State.GameOver);
        if (cornerBtn)
        {
            if (_state == State.Playing) { _state = State.Paused; _inSettings = false; }
            else { _inSettings = true; } // Ready / GameOver → straight into settings
            if (DebugScore) Debug.Log($"[JungleHop] menu open: state={_state} settings={_inSettings}");
        }

        // Esc closes an open menu, otherwise toggles pause during play.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_inSettings) _inSettings = false;
            else if (_state == State.Playing) _state = State.Paused;
            else if (_state == State.Paused) _state = State.Playing;
        }

        // A flap is Space / touch / left-click — never while a menu is open, while
        // paused, or when the click landed on the corner button.
        bool flap = !_inSettings && _state != State.Paused
                    && (Input.GetKeyDown(KeyCode.Space)
                        || (mouseDown && !cornerBtn)
                        || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began));

        switch (_state)
        {
            case State.Ready:
                _monkey.position = new Vector3(MonkeyX, _readyBaseY + Mathf.Sin(Time.time * 3f) * 0.22f, 0f);
                if (flap) StartPlaying();
                break;

            case State.Playing:
                if (flap) { _monkeyVel = FlapVelocity; TriggerFlap(); }
                Simulate(Time.deltaTime);
                break;

            case State.Paused:
                break; // world frozen; the pause/settings menu is drawn in OnGUI

            case State.GameOver:
                // Small settle so the monkey visibly drops onto the ground.
                if (_monkey.position.y > GroundTop + MonkeyHalf)
                {
                    _monkeyVel += Gravity * Time.deltaTime;
                    Vector3 p = _monkey.position;
                    p.y = Mathf.Max(GroundTop + MonkeyHalf, p.y + _monkeyVel * Time.deltaTime);
                    _monkey.position = p;
                }
                if (flap) ResetToReady();
                break;
        }

        AnimateMonkey(Time.deltaTime);
        ScrollDecor(Time.deltaTime, (_state == State.GameOver || _state == State.Paused) ? 0f : 1f);
    }

    void Simulate(float dt)
    {
        // Monkey physics.
        _monkeyVel += Gravity * dt;
        Vector3 pos = _monkey.position;
        pos.y += _monkeyVel * dt;

        // Ceiling clamp — you can't leave the top of the screen.
        float ceiling = OrthoSize - MonkeyHalf;
        if (pos.y > ceiling) { pos.y = ceiling; _monkeyVel = Mathf.Min(_monkeyVel, 0f); }
        _monkey.position = pos;

        // Tilt with velocity for that classic flappy feel.
        float angle = Mathf.Clamp(_monkeyVel * 5f, -70f, 28f);
        _monkey.rotation = Quaternion.Lerp(_monkey.rotation, Quaternion.Euler(0f, 0f, angle), dt * 10f);

        UpdatePowerups(dt);

        // Move trees, recycle, score, and collide.
        float leftmostRecycleX = -_rightEdge - TreeHalfW - 1f;
        for (int i = 0; i < _trees.Length; i++)
        {
            Tree t = _trees[i];
            Vector3 rp = t.root.position;
            rp.x -= CurrentSpeed() * dt;
            t.root.position = rp;

            if (rp.x < leftmostRecycleX)
            {
                PlaceTree(t, RightmostTreeX() + TreeSpacing);
            }
            else if (!t.scored && rp.x < MonkeyX)
            {
                // Score only when the tree actually crosses the monkey — not on the
                // same frame it recycled (rp is stale there, which double-counted).
                t.scored = true;
                _score++;
                if (DebugScore) Debug.Log($"[JungleHop] score={_score} (tree {i} crossed monkey at x={rp.x:F2})");
            }

            // Banana pickup — the magnet pulls nearby ones in; 2x doubles the gain.
            if (!t.collected)
            {
                Vector3 bp = t.banana.position;
                float cr = BananaCollectR;
                if (_magnetTimer > 0f)
                {
                    float d = Vector2.Distance(new Vector2(bp.x, bp.y), new Vector2(MonkeyX, pos.y));
                    if (d < MagnetRange)
                    {
                        t.banana.position = Vector3.MoveTowards(bp, new Vector3(MonkeyX, pos.y, bp.z), 9f * dt);
                        bp = t.banana.position;
                        cr = BananaCollectR + 0.2f;
                    }
                }
                if (Mathf.Abs(bp.x - MonkeyX) < cr && Mathf.Abs(bp.y - pos.y) < cr)
                {
                    t.collected = true;
                    t.banana.gameObject.SetActive(false);
                    _bananas += _x2Timer > 0f ? 2 : 1;
                    PlayBanana();
                    // Every 3rd banana, float a motivating message up in the clouds.
                    int bucket = _bananas / 3;
                    if (bucket > _lastBananaBucket) { _lastBananaBucket = bucket; TriggerCloudMessage(); }
                }
                else
                {
                    float sx = BananaSpriteSize * Mathf.Cos(Time.time * 4f + t.root.position.x);
                    t.banana.localScale = new Vector3(sx, BananaSpriteSize, 1f);
                }
            }

            // Power-up pickup.
            if (t.hasPower)
            {
                Vector3 pp = t.powerup.position;
                if (Mathf.Abs(pp.x - MonkeyX) < BananaCollectR + 0.2f && Mathf.Abs(pp.y - pos.y) < BananaCollectR + 0.2f)
                {
                    t.hasPower = false;
                    t.powerup.gameObject.SetActive(false);
                    ActivatePower(t.powerType);
                }
                else
                {
                    float sx = PowSpriteSize * Mathf.Cos(Time.time * 3f + t.root.position.x);
                    t.powerup.localScale = new Vector3(sx, PowSpriteSize, 1f);
                }
            }

            // Collision — the shield absorbs one hit and grants brief invulnerability.
            if (_invuln <= 0f && Overlaps(t, pos.y))
            {
                if (_shield) { _shield = false; _invuln = 0.9f; }
                else { Die(); return; }
            }
        }

        // Ground / floor.
        if (pos.y - MonkeyHalf <= GroundTop) Die();
    }

    /// <summary>Applies a collected power-up's effect.</summary>
    void ActivatePower(int type)
    {
        switch (type)
        {
            case 0: _shield = true; break;
            case 1: _slowTimer = PowSlowDur; break;
            case 2: _magnetTimer = PowMagnetDur; break;
            case 3: _x2Timer = Pow2xDur; break;
        }
        PlayBanana();
    }

    /// <summary>Ticks power-up timers and keeps the shield bubble on the monkey.</summary>
    void UpdatePowerups(float dt)
    {
        if (_slowTimer > 0f) _slowTimer -= dt;
        if (_magnetTimer > 0f) _magnetTimer -= dt;
        if (_x2Timer > 0f) _x2Timer -= dt;
        if (_invuln > 0f) _invuln -= dt;
        if (_cloudMsgT > 0f) { _cloudMsgT -= dt; _cloudMsgDrift += dt; }
        if (_shieldBubble != null)
        {
            bool on = _shield || _invuln > 0f;
            if (_shieldBubble.gameObject.activeSelf != on) _shieldBubble.gameObject.SetActive(on);
            if (on) _shieldBubble.position = new Vector3(_monkey.position.x, _monkey.position.y, -0.1f);
        }
    }

    float RightmostTreeX()
    {
        float max = float.NegativeInfinity;
        for (int i = 0; i < _trees.Length; i++)
            if (_trees[i].root.position.x > max) max = _trees[i].root.position.x;
        return max;
    }

    bool Overlaps(Tree t, float monkeyY)
    {
        float dx = Mathf.Abs(t.root.position.x - MonkeyX);
        if (dx > TreeHalfW + MonkeyHalf) return false; // no horizontal overlap
        bool hitTop = monkeyY + MonkeyHalf > t.gapCenter + t.gap * 0.5f;
        bool hitBottom = monkeyY - MonkeyHalf < t.gapCenter - t.gap * 0.5f;
        return hitTop || hitBottom;
    }

    void ScrollDecor(float dt, float scale)
    {
        float move = CurrentSpeed() * dt * scale;
        if (_clouds != null)
        {
            for (int i = 0; i < _clouds.Length; i++)
            {
                if (_clouds[i] == null) continue;
                Vector3 c = _clouds[i].position;
                c.x -= move * 0.12f; // slowest layer — far-away drift
                if (c.x < -_rightEdge - 4f) c.x = _rightEdge + 4f;
                _clouds[i].position = c;
            }
        }
    }

    /// <summary>Screen-space hit test for the top-left pause button.</summary>
    bool PointInPauseButton(Vector3 mousePos)
    {
        float x = mousePos.x;
        float yTop = Screen.height - mousePos.y;               // Input origin is bottom-left; GUI is top-left
        float bx = Screen.width - PauseBtnSize - PauseBtnMargin; // top-right corner
        return x >= bx && x <= bx + PauseBtnSize
            && yTop >= PauseBtnY && yTop <= PauseBtnY + PauseBtnSize;
    }
}
