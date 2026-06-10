using UnityEngine;

/// <summary>
/// The runner. Auto-moves forward, switches lanes, jumps, slides, and flies
/// during the Jetpack power-up. The visible character and hoverboard are built
/// from the equipped shop items and can be rebuilt live.
/// </summary>
public class PlayerController : MonoBehaviour
{
    const float JumpVelocity = 9.6f;
    const float Gravity = -27f;
    const float SlideTime = 0.7f;
    const float LaneLerp = 13f;
    const float JetpackHeight = 3.6f;

    int _lane = 1;
    float _vy, _jumpOffset;
    bool _grounded = true, _sliding;
    float _slideTimer;
    float _halfHeight = 1f;

    Transform _rig, _torso, _armL, _armR, _legL, _legR;
    float _runPhase;
    float _landSquash;

    GameObject _shieldBubble;
    GameObject _magnetRing;
    float _shieldPhase, _magnetPhase;

    bool _swiping;
    Vector3 _swipeStart;
    Vector2 _touchStart;

    /// <summary>Axis-aligned box used for hazard/coin overlap tests.</summary>
    public Bounds CollisionBounds
    {
        get { return new Bounds(transform.position, new Vector3(0.9f, _halfHeight * 2f, 0.9f)); }
    }

    void Start()
    {
        transform.position = new Vector3(GameManager.LaneX[_lane], 1f, 0f);
        BuildCharacter();
        BuildTrail();
    }

    void BuildTrail()
    {
        GameObject t = new GameObject("SpeedTrail");
        t.transform.SetParent(transform, false);
        t.transform.localPosition = new Vector3(0f, -0.25f, -0.35f);
        TrailRenderer tr = t.AddComponent<TrailRenderer>();
        tr.time = 0.32f;
        tr.startWidth = 0.55f;
        tr.endWidth = 0.03f;
        tr.minVertexDistance = 0.08f;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh != null) tr.material = new Material(sh);
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.45f, 0.85f, 1f), 0f),
                new GradientColorKey(new Color(0.2f, 0.5f, 1f), 1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.5f, 0f),
                new GradientAlphaKey(0f, 1f),
            });
        tr.colorGradient = g;
    }

    /// <summary>A collider-free translucent, faintly-glowing material for power-up auras.</summary>
    static Material AuraMat(Color tint, float alpha)
    {
        Material m = new Material(Shader.Find("Standard"));
        m.color = new Color(tint.r, tint.g, tint.b, alpha);
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", tint * 0.45f);
        m.renderQueue = 3000;
        return m;
    }

    /// <summary>Shows / hides a translucent protective bubble while the shield is active.</summary>
    void UpdateShield(bool on)
    {
        if (on && _shieldBubble == null)
        {
            _shieldBubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _shieldBubble.name = "ShieldBubble";
            Collider col = _shieldBubble.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _shieldBubble.transform.SetParent(transform, false);
            _shieldBubble.transform.localPosition = Vector3.zero;
            Renderer r = _shieldBubble.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = AuraMat(GameManager.PowerColor(GameManager.PowerUp.Shield), 0.22f);
        }

        if (_shieldBubble != null)
        {
            if (_shieldBubble.activeSelf != on) _shieldBubble.SetActive(on);
            if (on)
            {
                _shieldPhase += Time.deltaTime * 3f;
                float s = 2.3f + Mathf.Sin(_shieldPhase) * 0.08f;
                _shieldBubble.transform.localScale = new Vector3(s, s, s);
            }
        }
    }

    /// <summary>Shows / hides a glowing ground ring while the coin magnet is active.</summary>
    void UpdateMagnet(bool on)
    {
        if (on && _magnetRing == null)
        {
            _magnetRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _magnetRing.name = "MagnetRing";
            Collider col = _magnetRing.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _magnetRing.transform.SetParent(transform, false);
            _magnetRing.transform.localPosition = new Vector3(0f, -0.85f, 0f);
            Renderer r = _magnetRing.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = AuraMat(GameManager.PowerColor(GameManager.PowerUp.Magnet), 0.18f);
        }

        if (_magnetRing != null)
        {
            if (_magnetRing.activeSelf != on) _magnetRing.SetActive(on);
            if (on)
            {
                _magnetPhase += Time.deltaTime * 2.2f;
                float s = 2.6f + Mathf.Sin(_magnetPhase) * 0.45f;
                _magnetRing.transform.localScale = new Vector3(s, 0.03f, s);
                _magnetRing.transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.Self);
            }
        }
    }

    /// <summary>Rebuilds the character + board from the currently equipped items.</summary>
    public void RebuildAppearance()
    {
        if (_rig != null) Destroy(_rig.gameObject);
        BuildCharacter();
    }

    // ----------------------------------------------------------- character
    void BuildCharacter()
    {
        CharacterDef cd = Catalog.Character(GameData.EquippedCharacter);
        Material shirt = Art.Mat(cd.shirt, 0.0f, 0.35f);
        Material pants = Art.Mat(cd.pants, 0.0f, 0.3f);
        Material skin = Art.Mat(cd.skin, 0.0f, 0.25f);
        Material shoe = Art.Mat(new Color(0.11f, 0.11f, 0.13f), 0.1f, 0.45f);

        GameObject rig = new GameObject("Rig");
        rig.transform.SetParent(transform, false);
        _rig = rig.transform;

        _torso = Art.Solid(PrimitiveType.Cube, _rig, new Vector3(0f, 1.12f, 0f),
                           new Vector3(0.72f, 0.92f, 0.46f), shirt, "Torso").transform;
        Art.Solid(PrimitiveType.Sphere, _rig, new Vector3(0f, 1.78f, 0f),
                  new Vector3(0.56f, 0.58f, 0.56f), skin, "Head");
        Art.Solid(PrimitiveType.Cube, _rig, new Vector3(0f, 1.98f, -0.03f),
                  new Vector3(0.6f, 0.17f, 0.6f), shirt, "Cap");
        Art.Solid(PrimitiveType.Cube, _rig, new Vector3(0f, 1.93f, 0.27f),
                  new Vector3(0.58f, 0.11f, 0.22f), shirt, "CapBrim");

        _armL = Limb(new Vector3(-0.49f, 1.45f, 0f), shirt, skin);
        _armR = Limb(new Vector3(0.49f, 1.45f, 0f), shirt, skin);
        _legL = Leg(new Vector3(-0.19f, 0.68f, 0f), pants, shoe);
        _legR = Leg(new Vector3(0.19f, 0.68f, 0f), pants, shoe);

        BuildBoard();
    }

    void BuildBoard()
    {
        BoardDef bd = Catalog.Board(GameData.EquippedBoard);
        if (bd.id == 0) return;

        Material deck = Art.Mat(bd.deck, 0.2f, 0.55f);
        Material trim = Art.Mat(bd.trim, 0.4f, 0.6f);

        GameObject board = new GameObject("Board");
        board.transform.SetParent(_rig, false);
        board.transform.localPosition = new Vector3(0f, 0.12f, 0.05f);

        Art.Solid(PrimitiveType.Cube, board.transform, Vector3.zero,
                  new Vector3(0.98f, 0.14f, 2.0f), deck, "Deck");
        Art.Solid(PrimitiveType.Cube, board.transform, new Vector3(0f, 0.02f, 0.92f),
                  new Vector3(0.98f, 0.16f, 0.18f), trim, "Nose");
        Art.Solid(PrimitiveType.Cube, board.transform, new Vector3(0f, 0.02f, -0.92f),
                  new Vector3(0.98f, 0.16f, 0.18f), trim, "Tail");
        Art.Solid(PrimitiveType.Cube, board.transform, new Vector3(-0.32f, -0.12f, 0.5f),
                  new Vector3(0.16f, 0.16f, 0.16f), trim, "Bolt1");
        Art.Solid(PrimitiveType.Cube, board.transform, new Vector3(0.32f, -0.12f, 0.5f),
                  new Vector3(0.16f, 0.16f, 0.16f), trim, "Bolt2");
        Art.Solid(PrimitiveType.Cube, board.transform, new Vector3(-0.32f, -0.12f, -0.5f),
                  new Vector3(0.16f, 0.16f, 0.16f), trim, "Bolt3");
        Art.Solid(PrimitiveType.Cube, board.transform, new Vector3(0.32f, -0.12f, -0.5f),
                  new Vector3(0.16f, 0.16f, 0.16f), trim, "Bolt4");
    }

    Transform Limb(Vector3 shoulder, Material sleeve, Material hand)
    {
        GameObject pivot = new GameObject("Arm");
        pivot.transform.SetParent(_rig, false);
        pivot.transform.localPosition = shoulder;
        Art.Solid(PrimitiveType.Capsule, pivot.transform, new Vector3(0f, -0.32f, 0f),
                  new Vector3(0.2f, 0.32f, 0.2f), sleeve, "Upper");
        Art.Solid(PrimitiveType.Sphere, pivot.transform, new Vector3(0f, -0.62f, 0f),
                  new Vector3(0.21f, 0.21f, 0.21f), hand, "Hand");
        return pivot.transform;
    }

    Transform Leg(Vector3 hip, Material thigh, Material shoe)
    {
        GameObject pivot = new GameObject("Leg");
        pivot.transform.SetParent(_rig, false);
        pivot.transform.localPosition = hip;
        Art.Solid(PrimitiveType.Capsule, pivot.transform, new Vector3(0f, -0.32f, 0f),
                  new Vector3(0.25f, 0.34f, 0.25f), thigh, "Upper");
        Art.Solid(PrimitiveType.Cube, pivot.transform, new Vector3(0f, -0.64f, 0.08f),
                  new Vector3(0.27f, 0.16f, 0.42f), shoe, "Shoe");
        return pivot.transform;
    }

    // ---------------------------------------------------------------- loop
    void Update()
    {
        GameManager gm = GameManager.Instance;
        bool playing = gm != null && gm.CurrentState == GameManager.State.Playing && !gm.IsPaused;
        bool jetpack = gm != null && gm.ActivePower == GameManager.PowerUp.Jetpack;

        if (playing && !jetpack) HandleInput();

        UpdateShield(gm != null && gm.ActivePower == GameManager.PowerUp.Shield);
        UpdateMagnet(gm != null && gm.ActivePower == GameManager.PowerUp.Magnet);

        if (_sliding)
        {
            _slideTimer -= Time.deltaTime;
            if (_slideTimer <= 0f) _sliding = false;
        }
        if (_landSquash > 0f) _landSquash -= Time.deltaTime * 5f;
        _halfHeight = _sliding ? 0.5f : 1f;

        if (jetpack)
        {
            _sliding = false;
            _vy = 0f;
            _jumpOffset = Mathf.Lerp(_jumpOffset, JetpackHeight, 4.5f * Time.deltaTime);
            _grounded = false;
        }
        else
        {
            bool wasAir = !_grounded;
            _vy += Gravity * Time.deltaTime;
            _jumpOffset += _vy * Time.deltaTime;
            if (_jumpOffset <= 0f)
            {
                _jumpOffset = 0f;
                _vy = 0f;
                if (wasAir && playing) { Effects.DustPuff(FeetPos()); _landSquash = 1f; }
                _grounded = true;
            }
            else
            {
                _grounded = false;
            }
        }

        float speed = gm != null ? gm.CurrentSpeed : 0f;
        float x = Mathf.Lerp(transform.position.x, GameManager.LaneX[_lane], LaneLerp * Time.deltaTime);
        float y = _halfHeight + _jumpOffset;
        float z = transform.position.z + speed * Time.deltaTime;
        transform.position = new Vector3(x, y, z);

        Animate(playing, speed);
    }

    Vector3 FeetPos()
    {
        return transform.position - new Vector3(0f, _halfHeight, 0f);
    }

    void Animate(bool playing, float speed)
    {
        if (_rig == null) return;

        float sq = Mathf.Clamp01(_landSquash);
        float sxz = 1f + 0.16f * sq;
        _rig.localScale = new Vector3(sxz, _halfHeight * (1f - 0.2f * sq), sxz);
        _rig.localPosition = new Vector3(0f, -_halfHeight, 0f);

        float lean = Mathf.Clamp((transform.position.x - GameManager.LaneX[_lane]) * 7f, -22f, 22f);
        float pitch = _sliding ? 26f : (!_grounded ? -10f : 0f);
        _rig.localRotation = Quaternion.Slerp(_rig.localRotation,
            Quaternion.Euler(pitch, 0f, lean), 12f * Time.deltaTime);

        if (!_grounded)
        {
            PoseLimbs(-40f, -40f, -60f, 30f);
        }
        else if (_sliding)
        {
            PoseLimbs(-95f, -95f, -25f, 40f);
        }
        else
        {
            float freq = playing ? (7f + speed * 0.42f) : 2.4f;
            _runPhase += freq * Time.deltaTime;
            float amp = playing ? 55f : 9f;
            float s = Mathf.Sin(_runPhase) * amp;
            _armL.localRotation = Quaternion.Euler(s, 0f, 0f);
            _armR.localRotation = Quaternion.Euler(-s, 0f, 0f);
            _legL.localRotation = Quaternion.Euler(-s, 0f, 0f);
            _legR.localRotation = Quaternion.Euler(s, 0f, 0f);
            if (_torso != null)
                _torso.localPosition = new Vector3(0f, 1.12f + Mathf.Abs(Mathf.Sin(_runPhase)) * 0.05f, 0f);
        }
    }

    void PoseLimbs(float armL, float armR, float legL, float legR)
    {
        float k = 11f * Time.deltaTime;
        _armL.localRotation = Quaternion.Slerp(_armL.localRotation, Quaternion.Euler(armL, 0f, 0f), k);
        _armR.localRotation = Quaternion.Slerp(_armR.localRotation, Quaternion.Euler(armR, 0f, 0f), k);
        _legL.localRotation = Quaternion.Slerp(_legL.localRotation, Quaternion.Euler(legL, 0f, 0f), k);
        _legR.localRotation = Quaternion.Slerp(_legR.localRotation, Quaternion.Euler(legR, 0f, 0f), k);
    }

    // --------------------------------------------------------------- input
    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Move(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Move(1);
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Jump();
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.LeftShift)) Slide();

        if (Input.GetMouseButtonDown(0)) { _swiping = true; _swipeStart = Input.mousePosition; }
        else if (Input.GetMouseButtonUp(0) && _swiping)
        {
            _swiping = false;
            Swipe(Input.mousePosition - _swipeStart);
        }

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began) _touchStart = t.position;
            else if (t.phase == TouchPhase.Ended) Swipe(t.position - _touchStart);
        }
    }

    void Swipe(Vector2 d)
    {
        float min = Mathf.Max(40f, Screen.height * 0.04f);
        if (d.magnitude < min) return;
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) Move(d.x > 0f ? 1 : -1);
        else if (d.y > 0f) Jump();
        else Slide();
    }

    void Move(int dir) { _lane = Mathf.Clamp(_lane + dir, 0, 2); }

    void Jump()
    {
        if (!_grounded || _sliding) return;
        bool sneakers = GameManager.Instance != null
                        && GameManager.Instance.ActivePower == GameManager.PowerUp.Sneakers;
        _vy = JumpVelocity * (sneakers ? 1.45f : 1f);
        _grounded = false;
        Effects.DustPuff(FeetPos());
        if (GameManager.Instance != null && GameManager.Instance.Cam != null)
            GameManager.Instance.Cam.Punch(3.5f);
        if (AudioManager.Instance != null) AudioManager.Instance.Jump();
    }

    void Slide()
    {
        if (!_grounded || _sliding) return;
        _sliding = true;
        _slideTimer = SlideTime;
        if (AudioManager.Instance != null) AudioManager.Instance.Slide();
    }

    /// <summary>Flings the runner upward — used by launch ramps in the world.</summary>
    public void Launch(float velocity)
    {
        _sliding = false;
        _vy = velocity;
        _grounded = false;
        Effects.DustPuff(FeetPos());
        if (GameManager.Instance != null && GameManager.Instance.Cam != null)
            GameManager.Instance.Cam.Punch(6.5f);
        if (AudioManager.Instance != null) AudioManager.Instance.Jump();
    }
}
