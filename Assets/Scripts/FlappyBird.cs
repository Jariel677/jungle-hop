using UnityEngine;

/// <summary>
/// Self-contained Flappy Bird–style game with a jungle reskin: a monkey flaps
/// through gaps between trees over a jungle backdrop. A single MonoBehaviour
/// builds the whole world at runtime (orthographic camera, monkey, scrolling
/// trees, jungle floor, background foliage) and runs the Ready / Playing /
/// GameOver state machine. Drop this on one empty GameObject and press Play.
///
/// Controls: SPACE / left-click / touch to flap (and to start or restart).
/// Built with the Built-in render pipeline; reuses <see cref="Art"/> for
/// materials and collider-free primitives. The class name is kept as
/// <c>FlappyBird</c> so the existing scene reference stays wired.
/// </summary>
public class FlappyBird : MonoBehaviour
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

    // ---- State --------------------------------------------------------------
    enum State { Ready, Playing, GameOver }
    State _state = State.Ready;

    Camera _cam;
    Transform _monkey;
    float _monkeyVel;
    float _readyBaseY;

    Transform[] _ground = new Transform[2];
    Transform[] _bgFoliage = new Transform[5];
    Tree[] _trees;

    int _score;
    int _best;
    float _rightEdge;   // world x of the screen's right edge (recomputed per frame)
    float _tileW;       // ground tile width

    Material _trunkMat;
    Material _foliageMat;

    /// <summary>One tree pair: a root plus its precomputed gap centre and score flag.</summary>
    class Tree
    {
        public Transform root;
        public Transform top;
        public Transform bottom;
        public float gapCenter;
        public bool scored;
    }

    // ---- Setup --------------------------------------------------------------
    void Awake()
    {
        _best = PlayerPrefs.GetInt("FlappyBest", 0);
        BuildWorld();
        ResetToReady();
    }

    void BuildWorld()
    {
        // Camera — orthographic, looking down +Z at the z=0 play plane.
        var camGo = new GameObject("Main Camera");
        camGo.transform.SetParent(transform, true);
        camGo.tag = "MainCamera";
        _cam = camGo.AddComponent<Camera>();
        _cam.orthographic = true;
        _cam.orthographicSize = OrthoSize;
        _cam.transform.position = new Vector3(0f, 0f, -10f);
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = 50f;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.53f, 0.73f, 0.48f); // hazy jungle sky

        // Lighting — flat ambient plus a soft key light so Standard reads as flat.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.85f, 0.85f, 0.85f);
        var lightGo = new GameObject("Sun");
        lightGo.transform.SetParent(transform, true);
        var sun = lightGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.8f;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        _tileW = 2f * OrthoSize * 2f; // generous width; covers wide aspect ratios

        // Materials.
        Material dirt = Art.Mat(new Color(0.42f, 0.30f, 0.16f), 0f, 0.1f);   // jungle earth
        Material moss = Art.Mat(new Color(0.24f, 0.50f, 0.20f), 0f, 0.1f);   // mossy grass
        Material bushDark = Art.Mat(new Color(0.16f, 0.42f, 0.18f), 0f, 0.1f); // bg foliage
        _trunkMat = Art.Mat(new Color(0.45f, 0.30f, 0.16f), 0f, 0.12f);      // tree trunk
        _foliageMat = Art.Mat(new Color(0.22f, 0.55f, 0.22f), 0f, 0.15f);    // tree leaves

        Material fur = Art.Mat(new Color(0.45f, 0.28f, 0.15f), 0f, 0.2f);    // monkey fur
        Material skin = Art.Mat(new Color(0.82f, 0.63f, 0.42f), 0f, 0.2f);   // face / muzzle
        Material eyeMat = Art.Mat(Color.black, 0f, 0.1f);

        // Jungle floor — two tiles that scroll and recycle to fake infinite terrain.
        for (int i = 0; i < 2; i++)
        {
            var g = new GameObject("Ground" + i).transform;
            g.SetParent(transform, true);
            Art.Solid(PrimitiveType.Cube, g, new Vector3(0f, -0.6f, 0f),
                      new Vector3(_tileW, 1.2f, 1f), dirt, "dirt");
            Art.Solid(PrimitiveType.Cube, g, new Vector3(0f, 0.05f, -0.05f),
                      new Vector3(_tileW, 0.22f, 1f), moss, "moss");
            g.position = new Vector3(i * _tileW, GroundTop, 1f);
            _ground[i] = g;
        }

        // Background foliage — dark-green bush clumps drifting slowly for parallax.
        for (int i = 0; i < _bgFoliage.Length; i++)
        {
            var c = new GameObject("Bush" + i).transform;
            c.SetParent(transform, true);
            Art.Solid(PrimitiveType.Cube, c, Vector3.zero, new Vector3(2.0f, 1.1f, 1f), bushDark, "leaves");
            Art.Solid(PrimitiveType.Cube, c, new Vector3(0.9f, -0.2f, 0f), new Vector3(1.3f, 0.8f, 1f), bushDark, "leaves");
            Art.Solid(PrimitiveType.Cube, c, new Vector3(-0.9f, -0.2f, 0f), new Vector3(1.2f, 0.7f, 1f), bushDark, "leaves");
            c.position = new Vector3(i * 4.2f - 6f, 2.6f - (i % 3) * 1.4f, 5f);
            _bgFoliage[i] = c;
        }

        // Monkey — body, head, face, ears, eyes, tail. Fixed x; y is simulated.
        var monkey = new GameObject("Monkey").transform;
        monkey.SetParent(transform, true);
        Art.Solid(PrimitiveType.Cube, monkey, Vector3.zero, new Vector3(0.6f, 0.58f, 0.5f), fur, "body");
        Art.Solid(PrimitiveType.Cube, monkey, new Vector3(0.02f, 0.02f, -0.26f), new Vector3(0.34f, 0.34f, 0.12f), skin, "belly");
        Art.Solid(PrimitiveType.Cube, monkey, new Vector3(0.16f, 0.34f, -0.05f), new Vector3(0.5f, 0.46f, 0.46f), fur, "head");
        Art.Solid(PrimitiveType.Cube, monkey, new Vector3(0.26f, 0.28f, -0.26f), new Vector3(0.32f, 0.30f, 0.12f), skin, "face");
        Art.Solid(PrimitiveType.Cube, monkey, new Vector3(-0.06f, 0.54f, 0f), new Vector3(0.16f, 0.18f, 0.12f), fur, "earL");
        Art.Solid(PrimitiveType.Cube, monkey, new Vector3(0.38f, 0.54f, 0f), new Vector3(0.16f, 0.18f, 0.12f), fur, "earR");
        Art.Solid(PrimitiveType.Cube, monkey, new Vector3(0.18f, 0.40f, -0.30f), new Vector3(0.07f, 0.09f, 0.05f), eyeMat, "eyeL");
        Art.Solid(PrimitiveType.Cube, monkey, new Vector3(0.34f, 0.40f, -0.30f), new Vector3(0.07f, 0.09f, 0.05f), eyeMat, "eyeR");
        Art.Solid(PrimitiveType.Cube, monkey, new Vector3(0.30f, 0.24f, -0.30f), new Vector3(0.05f, 0.05f, 0.05f), eyeMat, "nose");
        // Curling tail behind the body.
        Art.Solid(PrimitiveType.Cube, monkey, new Vector3(-0.34f, -0.12f, 0.05f), new Vector3(0.32f, 0.12f, 0.12f), fur, "tail1");
        Art.Solid(PrimitiveType.Cube, monkey, new Vector3(-0.52f, 0.06f, 0.05f), new Vector3(0.12f, 0.30f, 0.12f), fur, "tail2");
        monkey.position = new Vector3(MonkeyX, 0f, 0f);
        _monkey = monkey;

        // Trees — a small pool recycled from right to left.
        _trees = new Tree[TreeCount];
        for (int i = 0; i < TreeCount; i++) _trees[i] = BuildTree(i);
    }

    Tree BuildTree(int index)
    {
        var root = new GameObject("Tree" + index).transform;
        root.SetParent(transform, true);

        // top / bottom are *unscaled* containers so their children (a tall trunk
        // plus a bushy foliage cap) keep correct world sizes. The trunk is 14 tall
        // so it always runs off the edge of the screen; foliage sits at the mouth.
        var top = new GameObject("top").transform;
        top.SetParent(root, false);
        Art.Solid(PrimitiveType.Cube, top, Vector3.zero, new Vector3(TreeHalfW * 2f, 14f, 0.9f), _trunkMat, "trunk");
        AddFoliage(top, -6.75f);

        var bottom = new GameObject("bottom").transform;
        bottom.SetParent(root, false);
        Art.Solid(PrimitiveType.Cube, bottom, Vector3.zero, new Vector3(TreeHalfW * 2f, 14f, 0.9f), _trunkMat, "trunk");
        AddFoliage(bottom, 6.75f);

        return new Tree { root = root, top = top, bottom = bottom, gapCenter = 0f, scored = true };
    }

    /// <summary>A bushy green cap of leaves at the mouth end of a trunk.</summary>
    void AddFoliage(Transform trunk, float y)
    {
        Art.Solid(PrimitiveType.Cube, trunk, new Vector3(0f, y, -0.05f), new Vector3(2.0f, 0.7f, 1.15f), _foliageMat, "leaves");
        Art.Solid(PrimitiveType.Cube, trunk, new Vector3(0.7f, y, -0.1f), new Vector3(0.9f, 0.55f, 1.1f), _foliageMat, "leaves");
        Art.Solid(PrimitiveType.Cube, trunk, new Vector3(-0.7f, y, -0.1f), new Vector3(0.9f, 0.55f, 1.1f), _foliageMat, "leaves");
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
        if (_score > _best)
        {
            _best = _score;
            PlayerPrefs.SetInt("FlappyBest", _best);
            PlayerPrefs.Save();
        }
    }

    void PlaceTree(Tree t, float x)
    {
        float min = GroundTop + TreeGap * 0.5f + 0.6f;
        float max = OrthoSize - TreeGap * 0.5f - 0.6f;
        t.gapCenter = Random.Range(min, max);
        t.root.position = new Vector3(x, 0f, 0.5f);
        t.top.localPosition = new Vector3(0f, t.gapCenter + TreeGap * 0.5f + 7f, 0f);
        t.bottom.localPosition = new Vector3(0f, t.gapCenter - TreeGap * 0.5f - 7f, 0f);
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
        bool flap = Input.GetKeyDown(KeyCode.Space)
                    || Input.GetMouseButtonDown(0)
                    || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);

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

        ScrollDecor(Time.deltaTime, _state == State.GameOver ? 0f : 1f);
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
                PlaceTree(t, RightmostTreeX() + TreeSpacing);

            if (!t.scored && rp.x < MonkeyX)
            {
                t.scored = true;
                _score++;
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
    }

    // ---- HUD (immediate-mode, matches the project's OnGUI style) -----------
    GUIStyle _big, _mid, _small;

    void OnGUI()
    {
        if (_big == null)
        {
            _big = new GUIStyle(GUI.skin.label) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        }
        _big.normal.textColor = Color.white;
        _mid.normal.textColor = Color.white;
        _small.normal.textColor = Color.white;

        float w = Screen.width, h = Screen.height;

        if (_state != State.GameOver)
            GUI.Label(new Rect(0f, h * 0.06f, w, 70f), _score.ToString(), _big);

        if (_state == State.Ready)
        {
            GUI.Label(new Rect(0f, h * 0.34f, w, 70f), "JUNGLE HOP", _big);
            GUI.Label(new Rect(0f, h * 0.46f, w, 40f), "SPACE / CLICK / TAP to flap", _small);
        }
        else if (_state == State.GameOver)
        {
            var box = new Rect(w * 0.5f - 170f, h * 0.30f, 340f, 240f);
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(box.x, box.y + 20f, box.width, 60f), "GAME OVER", _mid);
            GUI.Label(new Rect(box.x, box.y + 90f, box.width, 40f), "Score  " + _score, _mid);
            GUI.Label(new Rect(box.x, box.y + 135f, box.width, 40f), "Best  " + _best, _small);
            GUI.Label(new Rect(box.x, box.y + 185f, box.width, 40f), "CLICK / SPACE to play again", _small);
        }
    }
}
