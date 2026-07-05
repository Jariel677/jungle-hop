using UnityEngine;

/// <summary>
/// <see cref="FlappyBird"/> world construction: the one-time procedural build of
/// the camera, lighting, jungle floor, background foliage, monkey, and the pooled
/// trees (each with its collectible banana). Kept apart from the per-frame logic
/// so the core file stays focused on simulation. Same partial class.
/// </summary>
public partial class FlappyBird
{
    void BuildWorld()
    {
        LoadPrefs();

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
        camGo.AddComponent<AudioListener>(); // required to hear the banana SFX

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
        _treeMat = Art.SpriteMat(Resources.Load<Texture2D>("Trees/tree")); // flat cartoon tree-pipe sprite
        _bananaMat = Art.SpriteMat(Resources.Load<Texture2D>("Items/banana")); // flat cartoon banana sprite

        // In-game backdrop — the player's jungle scene image filling the view behind
        // the gameplay. Static (the world scrolls in front of it). The Ready screen's
        // menu art is drawn over the top in OnGUI, so this only shows once you start.
        Texture2D bgTex = Resources.Load<Texture2D>("JungleBG/gameplay");
        var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
        var bdc = backdrop.GetComponent<Collider>(); if (bdc != null) Destroy(bdc);
        backdrop.SetParent(transform, true);
        backdrop.GetComponent<Renderer>().sharedMaterial = Art.BackdropMat(bgTex);
        float re = _cam.orthographicSize * _cam.aspect;
        backdrop.localScale = new Vector3(re * 2f + 0.6f, OrthoSize * 2f + 0.6f, 1f);
        backdrop.position = new Vector3(0f, 0f, 9f);
        _backdrop = backdrop;

        // No 3D floor — the painted jungle backdrop supplies the ground, so it no
        // longer clashes with the flat tree sprites. The death line still lives at
        // GroundTop (see Simulate), so gameplay is unchanged.

        // Clouds — soft sprites from the UI pack drifting far back for extra depth.
        _clouds = new Transform[4];
        for (int i = 0; i < _clouds.Length; i++)
        {
            Texture2D ct = Tex("clouds/" + (i % 4 + 1));
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            var qc = q.GetComponent<Collider>(); if (qc != null) Destroy(qc);
            q.SetParent(transform, true);
            q.GetComponent<Renderer>().sharedMaterial = Art.SpriteMat(ct);
            float aspect = (ct != null && ct.height > 0) ? (float)ct.width / ct.height : 1.6f;
            float ch = 1.6f + (i % 2) * 0.7f;
            q.localScale = new Vector3(ch * aspect, ch, 1f);
            q.position = new Vector3(i * 5.5f - 7f, 2.4f + (i % 3) * 0.9f, 6f);
            _clouds[i] = q;
        }

        // Monkey — a flat 2D cartoon sprite on a camera-facing billboard, so it sits
        // in the cartoon world instead of reading as a 3D toy. Idle/flap frames (one
        // set per skin) are applied in ApplyMonkeyColor; animation lives in the core file.
        var monkey = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
        monkey.name = "Monkey";
        var mCol = monkey.GetComponent<Collider>(); if (mCol != null) Destroy(mCol);
        monkey.SetParent(transform, true);
        _monkeyRenderer = monkey.GetComponent<Renderer>();
        _monkeyBaseScale = new Vector3(MonkeySpriteSize, MonkeySpriteSize, 1f);
        monkey.localScale = _monkeyBaseScale;
        monkey.position = new Vector3(MonkeyX, 0f, 0f);
        _monkey = monkey;
        ApplyMonkeyColor();   // loads the equipped skin's frames and sets the idle sprite

        // Power-up icon materials (one per type) + the shield bubble that follows the monkey.
        _powMat = new Material[4];
        _powMat[0] = Art.SpriteMat(Resources.Load<Texture2D>("Items/pow_shield"));
        _powMat[1] = Art.SpriteMat(Resources.Load<Texture2D>("Items/pow_slow"));
        _powMat[2] = Art.SpriteMat(Resources.Load<Texture2D>("Items/pow_magnet"));
        _powMat[3] = Art.SpriteMat(Resources.Load<Texture2D>("Items/pow_2x"));
        var bubble = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
        bubble.name = "shieldBubble";
        var bubCol = bubble.GetComponent<Collider>(); if (bubCol != null) Destroy(bubCol);
        bubble.SetParent(transform, true);
        bubble.GetComponent<Renderer>().sharedMaterial = Art.SpriteMat(Resources.Load<Texture2D>("Items/pow_bubble"));
        bubble.localScale = new Vector3(MonkeySpriteSize * 0.95f, MonkeySpriteSize * 0.95f, 1f);
        bubble.gameObject.SetActive(false);
        _shieldBubble = bubble;

        // Trees — a small pool recycled from right to left.
        _trees = new Tree[TreeCount];
        for (int i = 0; i < TreeCount; i++) _trees[i] = BuildTree(i);

        SetupAudio();
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
        AddTreeSprite(top, true);     // top pipe: leafy cap points down into the gap

        var bottom = new GameObject("bottom").transform;
        bottom.SetParent(root, false);
        AddTreeSprite(bottom, false); // bottom pipe: leafy cap points up into the gap

        // Banana — a flat cartoon sprite that rides in the gap. PlaceTree drops it at
        // the gap centre; collecting it hides it until the tree recycles. It fake-spins
        // (width flip) in the per-frame logic.
        var banana = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
        banana.name = "banana";
        var bCol = banana.GetComponent<Collider>(); if (bCol != null) Destroy(bCol);
        banana.SetParent(root, false);
        banana.GetComponent<Renderer>().sharedMaterial = _bananaMat;
        banana.localScale = new Vector3(BananaSpriteSize, BananaSpriteSize, 1f);

        // Optional power-up sprite (inactive until PlaceTree decides to spawn one).
        var powerup = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
        powerup.name = "powerup";
        var pCol = powerup.GetComponent<Collider>(); if (pCol != null) Destroy(pCol);
        powerup.SetParent(root, false);
        powerup.gameObject.SetActive(false);

        return new Tree { root = root, top = top, bottom = bottom, banana = banana, powerup = powerup, gapCenter = 0f, gap = TreeGap, scored = true, collected = true };
    }

    /// <summary>Adds the flat tree-pipe sprite as a camera-facing billboard centred on
    /// the container. The trunk runs off-screen; the leafy cap sits at the gap-facing
    /// end. The top pipe uses the same sprite flipped vertically.</summary>
    void AddTreeSprite(Transform parent, bool flip)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
        q.name = "treeSprite";
        var c = q.GetComponent<Collider>(); if (c != null) Destroy(c);
        q.SetParent(parent, false);
        q.GetComponent<Renderer>().sharedMaterial = _treeMat;
        q.localScale = new Vector3(TreeSpriteW, flip ? -TreeSpriteH : TreeSpriteH, 1f);
        q.localPosition = Vector3.zero;
    }
}
