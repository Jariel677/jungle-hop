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
        Material dirt = Art.Mat(new Color(0.42f, 0.30f, 0.16f), 0f, 0.1f);   // jungle earth
        Material moss = Art.Mat(new Color(0.24f, 0.50f, 0.20f), 0f, 0.1f);   // mossy grass
        Material bushDark = Art.Mat(new Color(0.16f, 0.42f, 0.18f), 0f, 0.1f); // bg foliage
        _trunkMat = Art.Mat(new Color(0.45f, 0.30f, 0.16f), 0f, 0.12f);      // tree trunk
        _foliageMat = Art.Mat(new Color(0.26f, 0.60f, 0.26f), 0f, 0.15f);    // tree leaves (bright)
        _foliageDarkMat = Art.Mat(new Color(0.16f, 0.44f, 0.18f), 0f, 0.12f);// canopy lowlights
        _woodCapMat = Art.Mat(new Color(0.80f, 0.62f, 0.38f), 0f, 0.1f);     // light cut-log end
        _woodRingMat = Art.Mat(new Color(0.55f, 0.38f, 0.20f), 0f, 0.1f);    // cut-log ring
        _vineMat = Art.Mat(new Color(0.20f, 0.46f, 0.16f), 0f, 0.12f);       // hanging vines
        _bananaMat = Art.Glow(new Color(1f, 0.85f, 0.2f), new Color(0.5f, 0.4f, 0.05f), 0.4f); // banana (glows so it pops)
        _bananaTipMat = Art.Mat(new Color(0.35f, 0.22f, 0.08f), 0f, 0.1f);   // banana tip / stem

        _furMat = Art.Mat(Monkeys[_equipped].color, 0f, 0.2f);              // monkey fur (skin-selectable)
        Material fur = _furMat;
        Material skin = Art.Mat(new Color(0.82f, 0.63f, 0.42f), 0f, 0.2f);   // face / muzzle
        Material eyeMat = Art.Mat(Color.black, 0f, 0.1f);

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

        // Monkey — rounded cartoon build from spheres/capsules. Faces -Z (camera).
        // Fixed x; y is simulated. Collision still uses the MonkeyHalf constant.
        var monkey = new GameObject("Monkey").transform;
        monkey.SetParent(transform, true);
        Art.Solid(PrimitiveType.Sphere, monkey, new Vector3(0f, -0.02f, 0f), new Vector3(0.66f, 0.62f, 0.56f), fur, "body");
        Art.Solid(PrimitiveType.Sphere, monkey, new Vector3(0.02f, -0.04f, -0.24f), new Vector3(0.42f, 0.40f, 0.24f), skin, "belly");
        Art.Solid(PrimitiveType.Sphere, monkey, new Vector3(0.14f, 0.40f, -0.04f), new Vector3(0.54f, 0.52f, 0.52f), fur, "head");
        Art.Solid(PrimitiveType.Sphere, monkey, new Vector3(0.20f, 0.32f, -0.24f), new Vector3(0.36f, 0.30f, 0.24f), skin, "muzzle");
        Art.Solid(PrimitiveType.Sphere, monkey, new Vector3(-0.10f, 0.58f, 0f), new Vector3(0.22f, 0.22f, 0.14f), fur, "earL");
        Art.Solid(PrimitiveType.Sphere, monkey, new Vector3(0.40f, 0.58f, 0f), new Vector3(0.22f, 0.22f, 0.14f), fur, "earR");
        Art.Solid(PrimitiveType.Sphere, monkey, new Vector3(-0.10f, 0.58f, -0.07f), new Vector3(0.12f, 0.12f, 0.10f), skin, "earInL");
        Art.Solid(PrimitiveType.Sphere, monkey, new Vector3(0.40f, 0.58f, -0.07f), new Vector3(0.12f, 0.12f, 0.10f), skin, "earInR");
        Art.Solid(PrimitiveType.Sphere, monkey, new Vector3(0.09f, 0.45f, -0.28f), new Vector3(0.10f, 0.12f, 0.08f), eyeMat, "eyeL");
        Art.Solid(PrimitiveType.Sphere, monkey, new Vector3(0.31f, 0.45f, -0.28f), new Vector3(0.10f, 0.12f, 0.08f), eyeMat, "eyeR");
        Art.Solid(PrimitiveType.Sphere, monkey, new Vector3(0.20f, 0.30f, -0.34f), new Vector3(0.08f, 0.06f, 0.06f), eyeMat, "nose");
        // Curling tail behind the body (two rounded capsules).
        var tail1 = Art.Solid(PrimitiveType.Capsule, monkey, new Vector3(-0.38f, -0.10f, 0.06f), new Vector3(0.13f, 0.16f, 0.13f), fur, "tail1");
        tail1.transform.localRotation = Quaternion.Euler(0f, 0f, 72f);
        var tail2 = Art.Solid(PrimitiveType.Capsule, monkey, new Vector3(-0.54f, 0.10f, 0.06f), new Vector3(0.13f, 0.16f, 0.13f), fur, "tail2");
        tail2.transform.localRotation = Quaternion.Euler(0f, 0f, 24f);
        monkey.position = new Vector3(MonkeyX, 0f, 0f);
        _monkey = monkey;

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
        Art.Solid(PrimitiveType.Cylinder, top, Vector3.zero, new Vector3(TreeHalfW * 2f, 7f, TreeHalfW * 2f), _trunkMat, "trunk");
        AddFoliage(top, -6.75f);

        var bottom = new GameObject("bottom").transform;
        bottom.SetParent(root, false);
        Art.Solid(PrimitiveType.Cylinder, bottom, Vector3.zero, new Vector3(TreeHalfW * 2f, 7f, TreeHalfW * 2f), _trunkMat, "trunk");
        AddFoliage(bottom, 6.75f);

        // Banana — a little glowing crescent (three tilted cubes) that rides with
        // the tree in the gap. PlaceTree drops it at the gap centre; collecting it
        // hides it until the tree recycles.
        var banana = new GameObject("banana").transform;
        banana.SetParent(root, false);
        var b1 = Art.Solid(PrimitiveType.Capsule, banana, new Vector3(-0.05f, 0.16f, 0f), new Vector3(0.17f, 0.14f, 0.17f), _bananaMat, "peel");
        b1.transform.localRotation = Quaternion.Euler(0f, 0f, 52f);
        Art.Solid(PrimitiveType.Capsule, banana, new Vector3(0.12f, 0f, 0f), new Vector3(0.17f, 0.16f, 0.17f), _bananaMat, "peel");
        var b3 = Art.Solid(PrimitiveType.Capsule, banana, new Vector3(-0.05f, -0.16f, 0f), new Vector3(0.17f, 0.14f, 0.17f), _bananaMat, "peel");
        b3.transform.localRotation = Quaternion.Euler(0f, 0f, -52f);
        // Little brown stem tips at each end.
        Art.Solid(PrimitiveType.Sphere, banana, new Vector3(-0.14f, 0.30f, 0f), new Vector3(0.10f, 0.10f, 0.10f), _bananaTipMat, "tip");
        Art.Solid(PrimitiveType.Sphere, banana, new Vector3(-0.14f, -0.30f, 0f), new Vector3(0.10f, 0.10f, 0.10f), _bananaTipMat, "tip");

        return new Tree { root = root, top = top, bottom = bottom, banana = banana, gapCenter = 0f, scored = true, collected = true };
    }

    /// <summary>Dresses the mouth end of a trunk as a cartoon cut-log: a light
    /// wood-ring end facing the camera, a small leafy collar, and — on the upper
    /// log — vines drooping into the gap. Matches the reference art.</summary>
    void AddFoliage(Transform trunk, float y)
    {
        float s = Mathf.Sign(y); // +1 = lower log (mouth up), -1 = upper log (mouth down)

        // Camera-facing cut-log end: light disc + darker inner ring (flattened spheres).
        Art.Solid(PrimitiveType.Sphere, trunk, new Vector3(0f, y, -0.48f), new Vector3(1.7f, 1.7f, 0.14f), _woodCapMat, "cut");
        Art.Solid(PrimitiveType.Sphere, trunk, new Vector3(0f, y, -0.52f), new Vector3(1.0f, 1.0f, 0.14f), _woodRingMat, "ring");

        // Leafy collar spilling out of the mouth, toward the gap.
        Art.Solid(PrimitiveType.Sphere, trunk, new Vector3(-0.7f, y + s * 0.35f, -0.16f), new Vector3(0.95f, 0.7f, 0.9f), _foliageMat, "leaf");
        Art.Solid(PrimitiveType.Sphere, trunk, new Vector3(0.7f, y + s * 0.35f, -0.16f), new Vector3(0.95f, 0.7f, 0.9f), _foliageDarkMat, "leaf");
        Art.Solid(PrimitiveType.Sphere, trunk, new Vector3(0f, y + s * 0.5f, -0.2f), new Vector3(1.25f, 0.8f, 0.95f), _foliageMat, "leaf");

        // Hanging vines only on the upper log (mouth points down into the gap).
        if (s < 0f)
        {
            Art.Solid(PrimitiveType.Capsule, trunk, new Vector3(-0.42f, y - 0.55f, -0.25f), new Vector3(0.07f, 0.30f, 0.07f), _vineMat, "vine");
            Art.Solid(PrimitiveType.Sphere, trunk, new Vector3(-0.42f, y - 0.95f, -0.25f), new Vector3(0.24f, 0.24f, 0.22f), _foliageMat, "vineLeaf");
            Art.Solid(PrimitiveType.Capsule, trunk, new Vector3(0.36f, y - 0.75f, -0.25f), new Vector3(0.07f, 0.40f, 0.07f), _vineMat, "vine");
            Art.Solid(PrimitiveType.Sphere, trunk, new Vector3(0.36f, y - 1.25f, -0.25f), new Vector3(0.26f, 0.26f, 0.22f), _foliageMat, "vineLeaf");
        }
    }
}
