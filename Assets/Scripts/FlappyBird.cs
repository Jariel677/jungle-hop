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

    const float Gravity = -20f;
    const float FlapVelocity = 7f;

    const float ScrollSpeed = 3.6f;    // tree / ground travel speed
    const float TreeHalfW = 0.8f;      // half trunk width
    const float TreeGap = 3.4f;        // vertical opening the monkey flies through
    const float TreeSpacing = 3.7f;    // horizontal distance between tree pairs
    const int TreeCount = 6;

    const float BananaCollectR = 0.55f; // monkey-to-banana distance that counts as a pickup

    const string GameTitle = "JUNGLE HOP"; // shown on the start screen (edit to rename the game)

    // On-screen pause/menu button (top-right); its click is handled in Update, drawn in OnGUI.
    const float PauseBtnMargin = 14f, PauseBtnY = 14f, PauseBtnSize = 64f;

    // Selectable monkey skins: display name, fur colour, and lifetime bananas needed to unlock.
    static readonly (string name, Color color, int need)[] Monkeys =
    {
        ("Brown",  new Color(0.45f, 0.28f, 0.15f), 0),
        ("Yellow", new Color(0.95f, 0.80f, 0.15f), 50),
        ("Blue",   new Color(0.30f, 0.55f, 0.95f), 100),
        ("Green",  new Color(0.35f, 0.75f, 0.35f), 300),
    };

    // Flip to true to log score/banana/skin events to the Editor/Player log for verification.
    const bool DebugScore = true;

    // ---- State --------------------------------------------------------------
    enum State { Ready, Playing, GameOver, Paused }
    State _state = State.Ready;
    bool _inSettings;   // true when the settings sub-panel is open over the pause menu

    Camera _cam;
    Transform _monkey;
    float _monkeyVel;
    float _readyBaseY;

    Transform[] _ground = new Transform[2];
    Transform[] _bgFoliage = new Transform[5];
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

    Material _trunkMat;
    Material _foliageMat;
    Material _foliageDarkMat; // canopy lowlights for depth
    Material _woodCapMat;     // light cut-log end
    Material _woodRingMat;    // cut-log inner ring
    Material _vineMat;        // hanging vines
    Material _bananaMat;
    Material _bananaTipMat;   // banana stem / tip
    Material _furMat;   // monkey fur; recoloured when a skin is equipped

    /// <summary>One tree pair: a root plus its precomputed gap centre, score flag,
    /// and the banana that sits in the gap for the monkey to collect.</summary>
    class Tree
    {
        public Transform root;
        public Transform top;
        public Transform bottom;
        public Transform banana;
        public float gapCenter;
        public bool scored;
        public bool collected;
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

    /// <summary>Tree/ground speed ramps gently with score, then caps out.</summary>
    float CurrentSpeed()
    {
        return ScrollSpeed + Mathf.Min(_score * 0.05f, 2.6f);
    }

    // ---- Lifecycle transitions ---------------------------------------------
    void ResetToReady()
    {
        _state = State.Ready;
        _score = 0;
        _bananas = 0;
        _monkeyVel = 0f;
        _readyBaseY = 0.4f;
        _monkey.position = new Vector3(MonkeyX, _readyBaseY, 0f);
        _monkey.rotation = Quaternion.identity;

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

    /// <summary>Recolours the monkey's fur to the equipped skin.</summary>
    void ApplyMonkeyColor()
    {
        if (_furMat != null) _furMat.color = Monkeys[_equipped].color;
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
        float min = GroundTop + TreeGap * 0.5f + 0.6f;
        float max = OrthoSize - TreeGap * 0.5f - 0.6f;
        t.gapCenter = Random.Range(min, max);
        t.root.position = new Vector3(x, 0f, 0.5f);
        t.top.localPosition = new Vector3(0f, t.gapCenter + TreeGap * 0.5f + 7f, 0f);
        t.bottom.localPosition = new Vector3(0f, t.gapCenter - TreeGap * 0.5f - 7f, 0f);
        t.scored = false; // a freshly (re)placed tree is scoreable again; recycling forgot this and capped score at TreeCount

        // Drop a fresh banana in the gap (local z -0.5 puts it on the monkey's z=0 plane).
        t.banana.localPosition = new Vector3(0f, t.gapCenter, -0.5f);
        t.banana.gameObject.SetActive(true);
        t.collected = false;
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
                if (flap) _monkeyVel = FlapVelocity;
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

            // Banana pickup: collect on overlap, otherwise spin it to catch the eye.
            if (!t.collected)
            {
                Vector3 bp = t.banana.position;
                if (Mathf.Abs(bp.x - MonkeyX) < BananaCollectR && Mathf.Abs(bp.y - pos.y) < BananaCollectR)
                {
                    t.collected = true;
                    t.banana.gameObject.SetActive(false);
                    _bananas++;
                    PlayBanana();
                    if (DebugScore) Debug.Log($"[JungleHop] banana={_bananas} (collected from tree {i})");
                }
                else
                {
                    t.banana.Rotate(0f, 220f * dt, 0f);
                }
            }

            if (Overlaps(t, pos.y)) { Die(); return; }
        }

        // Ground / floor.
        if (pos.y - MonkeyHalf <= GroundTop) Die();
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
        bool hitTop = monkeyY + MonkeyHalf > t.gapCenter + TreeGap * 0.5f;
        bool hitBottom = monkeyY - MonkeyHalf < t.gapCenter - TreeGap * 0.5f;
        return hitTop || hitBottom;
    }

    void ScrollDecor(float dt, float scale)
    {
        float move = CurrentSpeed() * dt * scale;
        for (int i = 0; i < _ground.Length; i++)
        {
            Vector3 g = _ground[i].position;
            g.x -= move;
            if (g.x <= -_tileW) g.x += _tileW * 2f;
            _ground[i].position = g;
        }
        for (int i = 0; i < _bgFoliage.Length; i++)
        {
            Vector3 c = _bgFoliage[i].position;
            c.x -= move * 0.3f;
            if (c.x < -_rightEdge - 3f) c.x = _rightEdge + 3f;
            _bgFoliage[i].position = c;
        }
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
