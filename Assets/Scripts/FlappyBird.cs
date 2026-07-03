using UnityEngine;

/// <summary>
/// Self-contained Flappy Bird clone. A single MonoBehaviour builds the whole
/// world at runtime (orthographic camera, bird, scrolling pipes, ground, clouds)
/// and runs the Ready / Playing / GameOver state machine. Drop this on one empty
/// GameObject in a scene and press Play — nothing else is required.
///
/// Controls: SPACE / left-click / touch to flap (and to start or restart).
/// Built with the Built-in render pipeline; reuses <see cref="Art"/> for
/// materials and collider-free primitives.
/// </summary>
public class FlappyBird : MonoBehaviour
{
    // ---- Tunables -----------------------------------------------------------
    const float OrthoSize = 5f;        // half-height of the view; y spans -5..5
    const float GroundTop = -4f;       // world y of the ground surface
    const float BirdX = -3.2f;         // bird is fixed on x, world scrolls past
    const float BirdHalf = 0.28f;      // half-extent used for collision

    const float Gravity = -20f;
    const float FlapVelocity = 7f;

    const float ScrollSpeed = 3.6f;    // pipe / ground travel speed
    const float PipeHalfW = 0.8f;      // half pipe width
    const float PipeGap = 3.4f;        // vertical opening the bird flies through
    const float PipeSpacing = 3.7f;    // horizontal distance between pairs
    const int PipeCount = 6;

    // ---- State --------------------------------------------------------------
    enum State { Ready, Playing, GameOver }
    State _state = State.Ready;

    Camera _cam;
    Transform _bird;
    float _birdVel;
    float _readyBaseY;

    Transform[] _ground = new Transform[2];
    Transform[] _clouds = new Transform[4];
    Pipe[] _pipes;

    int _score;
    int _best;
    float _rightEdge;   // world x of the screen's right edge (recomputed per frame)
    float _tileW;       // ground tile width

    /// <summary>One pipe pair: a root plus its precomputed gap centre and score flag.</summary>
    class Pipe
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
        camGo.tag = "MainCamera";
        _cam = camGo.AddComponent<Camera>();
        _cam.orthographic = true;
        _cam.orthographicSize = OrthoSize;
        _cam.transform.position = new Vector3(0f, 0f, -10f);
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = 50f;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.35f, 0.65f, 0.92f); // sky blue

        // Lighting — flat ambient plus a soft key light so Standard reads as flat.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.85f, 0.85f, 0.85f);
        var lightGo = new GameObject("Sun");
        var sun = lightGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.8f;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        _tileW = 2f * OrthoSize * 2f; // generous width; covers wide aspect ratios

        // Materials.
        Material tan = Art.Mat(new Color(0.85f, 0.72f, 0.42f), 0f, 0.1f);
        Material darkTan = Art.Mat(new Color(0.55f, 0.42f, 0.22f), 0f, 0.1f);
        Material white = Art.Mat(Color.white, 0f, 0.1f);
        Material bodyMat = Art.Mat(new Color(1f, 0.86f, 0.2f), 0f, 0.2f);   // yellow
        Material beakMat = Art.Mat(new Color(0.95f, 0.5f, 0.1f), 0f, 0.2f); // orange
        Material eyeMat = Art.Mat(Color.black, 0f, 0.1f);
        _pipeMat = Art.Mat(new Color(0.36f, 0.72f, 0.3f), 0f, 0.15f);       // green

        // Ground — two tiles that scroll and recycle to fake infinite terrain.
        for (int i = 0; i < 2; i++)
        {
            var g = new GameObject("Ground" + i).transform;
            Art.Solid(PrimitiveType.Cube, g, new Vector3(0f, -0.6f, 0f),
                      new Vector3(_tileW, 1.2f, 1f), tan, "dirt");
            Art.Solid(PrimitiveType.Cube, g, new Vector3(0f, 0.05f, -0.05f),
                      new Vector3(_tileW, 0.18f, 1f), darkTan, "grass");
            g.position = new Vector3(i * _tileW, GroundTop, 1f);
            _ground[i] = g;
        }

        // Clouds — cosmetic parallax in the background.
        for (int i = 0; i < _clouds.Length; i++)
        {
            var c = new GameObject("Cloud" + i).transform;
            Art.Solid(PrimitiveType.Cube, c, Vector3.zero, new Vector3(1.6f, 0.7f, 1f), white, "puff");
            Art.Solid(PrimitiveType.Cube, c, new Vector3(0.7f, -0.1f, 0f), new Vector3(1.1f, 0.5f, 1f), white, "puff");
            Art.Solid(PrimitiveType.Cube, c, new Vector3(-0.7f, -0.1f, 0f), new Vector3(1f, 0.45f, 1f), white, "puff");
            c.position = new Vector3(i * 5f - 6f, 2.2f + (i % 2) * 1.1f, 4f);
            _clouds[i] = c;
        }

        // Bird — body, beak, eye. Fixed x; y is simulated.
        var bird = new GameObject("Bird").transform;
        Art.Solid(PrimitiveType.Cube, bird, Vector3.zero, new Vector3(0.62f, 0.5f, 0.5f), bodyMat, "body");
        Art.Solid(PrimitiveType.Cube, bird, new Vector3(-0.15f, -0.28f, -0.05f), new Vector3(0.5f, 0.18f, 0.4f), bodyMat, "wing");
        Art.Solid(PrimitiveType.Cube, bird, new Vector3(0.34f, -0.02f, -0.05f), new Vector3(0.22f, 0.14f, 0.3f), beakMat, "beak");
        Art.Solid(PrimitiveType.Cube, bird, new Vector3(0.16f, 0.12f, -0.26f), new Vector3(0.12f, 0.12f, 0.05f), eyeMat, "eye");
        bird.position = new Vector3(BirdX, 0f, 0f);
        _bird = bird;

        // Pipes — a small pool recycled from right to left.
        _pipes = new Pipe[PipeCount];
        for (int i = 0; i < PipeCount; i++) _pipes[i] = BuildPipe(i);
    }

    Material _pipeMat;

    Pipe BuildPipe(int index)
    {
        var root = new GameObject("Pipe" + index).transform;

        // top / bottom are *unscaled* containers so their children (a tall body
        // cube plus a lip cap) keep correct world sizes. The body is 14 tall so
        // it always runs off the edge of the screen; the lip sits at its mouth.
        var top = new GameObject("top").transform;
        top.SetParent(root, false);
        Art.Solid(PrimitiveType.Cube, top, Vector3.zero, new Vector3(PipeHalfW * 2f, 14f, 1f), _pipeMat, "body");
        Art.Solid(PrimitiveType.Cube, top, new Vector3(0f, -6.75f, -0.05f), new Vector3(1.85f, 0.5f, 1.1f), _pipeMat, "lip");

        var bottom = new GameObject("bottom").transform;
        bottom.SetParent(root, false);
        Art.Solid(PrimitiveType.Cube, bottom, Vector3.zero, new Vector3(PipeHalfW * 2f, 14f, 1f), _pipeMat, "body");
        Art.Solid(PrimitiveType.Cube, bottom, new Vector3(0f, 6.75f, -0.05f), new Vector3(1.85f, 0.5f, 1.1f), _pipeMat, "lip");

        return new Pipe { root = root, top = top, bottom = bottom, gapCenter = 0f, scored = true };
    }

    /// <summary>Pipe/ground speed ramps gently with score, then caps out.</summary>
    float CurrentSpeed()
    {
        return ScrollSpeed + Mathf.Min(_score * 0.05f, 2.6f);
    }

    // ---- Lifecycle transitions ---------------------------------------------
    void ResetToReady()
    {
        _state = State.Ready;
        _score = 0;
        _birdVel = 0f;
        _readyBaseY = 0.4f;
        _bird.position = new Vector3(BirdX, _readyBaseY, 0f);
        _bird.rotation = Quaternion.identity;

        // Park the pool off-screen to the right, spaced out, with fresh gaps.
        _rightEdge = _cam.orthographicSize * _cam.aspect;
        float x = _rightEdge + 3f;
        for (int i = 0; i < _pipes.Length; i++)
        {
            PlacePipe(_pipes[i], x);
            _pipes[i].scored = true; // no scoring until the run starts
            x += PipeSpacing;
        }
    }

    void StartPlaying()
    {
        _state = State.Playing;
        _birdVel = FlapVelocity;
        for (int i = 0; i < _pipes.Length; i++) _pipes[i].scored = false;
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

    void PlacePipe(Pipe p, float x)
    {
        float min = GroundTop + PipeGap * 0.5f + 0.6f;
        float max = OrthoSize - PipeGap * 0.5f - 0.6f;
        p.gapCenter = Random.Range(min, max);
        p.root.position = new Vector3(x, 0f, 0.5f);
        p.top.localPosition = new Vector3(0f, p.gapCenter + PipeGap * 0.5f + 7f, 0f);
        p.bottom.localPosition = new Vector3(0f, p.gapCenter - PipeGap * 0.5f - 7f, 0f);
    }

    // ---- Per-frame ----------------------------------------------------------
    void Update()
    {
        _rightEdge = _cam.orthographicSize * _cam.aspect;
        bool flap = Input.GetKeyDown(KeyCode.Space)
                    || Input.GetMouseButtonDown(0)
                    || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);

        switch (_state)
        {
            case State.Ready:
                _bird.position = new Vector3(BirdX, _readyBaseY + Mathf.Sin(Time.time * 3f) * 0.22f, 0f);
                if (flap) StartPlaying();
                break;

            case State.Playing:
                if (flap) _birdVel = FlapVelocity;
                Simulate(Time.deltaTime);
                break;

            case State.GameOver:
                // Small settle so the bird visibly drops onto the ground.
                if (_bird.position.y > GroundTop + BirdHalf)
                {
                    _birdVel += Gravity * Time.deltaTime;
                    Vector3 p = _bird.position;
                    p.y = Mathf.Max(GroundTop + BirdHalf, p.y + _birdVel * Time.deltaTime);
                    _bird.position = p;
                }
                if (flap) ResetToReady();
                break;
        }

        ScrollDecor(Time.deltaTime, _state == State.GameOver ? 0f : 1f);
    }

    void Simulate(float dt)
    {
        // Bird physics.
        _birdVel += Gravity * dt;
        Vector3 pos = _bird.position;
        pos.y += _birdVel * dt;

        // Ceiling clamp — you can't leave the top of the screen.
        float ceiling = OrthoSize - BirdHalf;
        if (pos.y > ceiling) { pos.y = ceiling; _birdVel = Mathf.Min(_birdVel, 0f); }
        _bird.position = pos;

        // Tilt with velocity for that classic flappy feel.
        float angle = Mathf.Clamp(_birdVel * 5f, -70f, 28f);
        _bird.rotation = Quaternion.Lerp(_bird.rotation, Quaternion.Euler(0f, 0f, angle), dt * 10f);

        // Move pipes, recycle, score, and collide.
        float leftmostRecycleX = -_rightEdge - PipeHalfW - 1f;
        for (int i = 0; i < _pipes.Length; i++)
        {
            Pipe p = _pipes[i];
            Vector3 rp = p.root.position;
            rp.x -= CurrentSpeed() * dt;
            p.root.position = rp;

            if (rp.x < leftmostRecycleX)
                PlacePipe(p, RightmostPipeX() + PipeSpacing);

            if (!p.scored && rp.x < BirdX)
            {
                p.scored = true;
                _score++;
            }

            if (Overlaps(p, pos.y)) { Die(); return; }
        }

        // Ground / floor.
        if (pos.y - BirdHalf <= GroundTop) Die();
    }

    float RightmostPipeX()
    {
        float max = float.NegativeInfinity;
        for (int i = 0; i < _pipes.Length; i++)
            if (_pipes[i].root.position.x > max) max = _pipes[i].root.position.x;
        return max;
    }

    bool Overlaps(Pipe p, float birdY)
    {
        float dx = Mathf.Abs(p.root.position.x - BirdX);
        if (dx > PipeHalfW + BirdHalf) return false; // no horizontal overlap
        bool hitTop = birdY + BirdHalf > p.gapCenter + PipeGap * 0.5f;
        bool hitBottom = birdY - BirdHalf < p.gapCenter - PipeGap * 0.5f;
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
        for (int i = 0; i < _clouds.Length; i++)
        {
            Vector3 c = _clouds[i].position;
            c.x -= move * 0.3f;
            if (c.x < -_rightEdge - 3f) c.x = _rightEdge + 3f;
            _clouds[i].position = c;
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
            GUI.Label(new Rect(0f, h * 0.34f, w, 70f), "FLAPPY", _big);
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
