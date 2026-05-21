using UnityEngine;

/// <summary>
/// The runner. Auto-moves forward, switches between three lanes, jumps and
/// slides. Movement is fully code-driven (kinematic) — collisions are resolved
/// by <see cref="WorldGenerator"/> against <see cref="CollisionBounds"/>.
/// </summary>
public class PlayerController : MonoBehaviour
{
    const float JumpVelocity = 9.6f;
    const float Gravity = -27f;
    const float SlideTime = 0.7f;
    const float LaneLerp = 13f;

    int _lane = 1;
    float _vy;
    float _jumpOffset;
    bool _grounded = true;
    bool _sliding;
    float _slideTimer;
    float _halfHeight = 1f;

    Transform _visual;
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

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cap.name = "Body";
        Collider col = cap.GetComponent<Collider>();
        if (col != null) Destroy(col);
        cap.transform.SetParent(transform, false);
        cap.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
        Renderer r = cap.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.2f, 0.56f, 1f);
        _visual = cap.transform;
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        bool playing = gm != null && gm.CurrentState == GameManager.State.Playing;

        if (playing) HandleInput();

        if (_sliding)
        {
            _slideTimer -= Time.deltaTime;
            if (_slideTimer <= 0f) _sliding = false;
        }
        _halfHeight = _sliding ? 0.5f : 1f;

        // Vertical jump arc (manual integration — no physics engine).
        _vy += Gravity * Time.deltaTime;
        _jumpOffset += _vy * Time.deltaTime;
        if (_jumpOffset <= 0f) { _jumpOffset = 0f; _vy = 0f; _grounded = true; }
        else _grounded = false;

        float speed = gm != null ? gm.CurrentSpeed : 0f;
        float x = Mathf.Lerp(transform.position.x, GameManager.LaneX[_lane], LaneLerp * Time.deltaTime);
        float y = _halfHeight + _jumpOffset;
        float z = transform.position.z + speed * Time.deltaTime;
        transform.position = new Vector3(x, y, z);

        if (_visual != null)
            _visual.localScale = new Vector3(0.9f, _halfHeight, 0.9f);
    }

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
    void Jump() { if (_grounded && !_sliding) { _vy = JumpVelocity; _grounded = false; } }
    void Slide() { if (_grounded && !_sliding) { _sliding = true; _slideTimer = SlideTime; } }
}
