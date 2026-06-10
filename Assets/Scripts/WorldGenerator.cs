using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedurally streams the world: recycling detailed ground tiles, a scrolling
/// city of buildings and lamps, hazard/coin/power-up rows, and per-frame
/// collision, magnet and pickup resolution.
/// </summary>
public class WorldGenerator : MonoBehaviour
{
    public PlayerController player;

    const float TileLength = 24f;
    const float TileWidth = 11f;
    const int TileCount = 9;

    const float RowGap = 13f;
    const float PropGap = 11f;
    const float SpawnAhead = 150f;
    const float DespawnBehind = 26f;

    class Hazard { public GameObject go; public Bounds bounds; public int kind; public float centerY; public bool passed; }
    class Pickup { public GameObject go; public float baseY; }
    class Power { public GameObject go; public GameManager.PowerUp type; public float baseY; }

    readonly List<Transform> _tiles = new List<Transform>();
    readonly List<Hazard> _hazards = new List<Hazard>();
    readonly List<Pickup> _coins = new List<Pickup>();
    readonly List<Power> _powers = new List<Power>();
    readonly List<GameObject> _props = new List<GameObject>();

    float _nextTileZ, _nextRowZ = 36f, _nextPropZ = 12f;
    int _propParity;

    Material _track, _sleeper, _line, _curb;
    Material[] _building;
    Material _window, _pole, _lamp;
    Material _barrier, _stripe, _train, _trainTrim, _trainWin, _gate, _sign;
    Material _coinMat, _powerCore;
    Material[] _powerMat;
    Material _markJump, _markSlide, _markDodge;

    GameObject[] _buildings;
    GameObject[] _cars;

    void Start()
    {
        BuildPalette();
        _buildings = LoadBuildings();
        _cars = LoadCars();
        _nextTileZ = -TileLength;
        for (int i = 0; i < TileCount; i++) SpawnTile();
    }

    GameObject[] LoadBuildings()
    {
        string[] names =
        {
            "building-a", "building-b", "building-c", "building-d", "building-e",
            "building-f", "building-g", "building-h", "building-i", "building-j",
            "building-k", "building-l", "building-m", "building-n",
            "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c",
            "building-skyscraper-d", "building-skyscraper-e",
        };
        List<GameObject> list = new List<GameObject>();
        foreach (string n in names)
        {
            GameObject g = Resources.Load<GameObject>("CityKit/" + n);
            if (g != null) list.Add(g);
        }
        return list.ToArray();
    }

    GameObject[] LoadCars()
    {
        string[] names =
        {
            "sedan", "sedan-sports", "suv", "suv-luxury", "taxi", "police",
            "hatchback-sports", "ambulance", "delivery", "garbage-truck",
            "firetruck", "truck-flat", "van",
        };
        List<GameObject> list = new List<GameObject>();
        foreach (string n in names)
        {
            GameObject g = Resources.Load<GameObject>("CarKit/" + n);
            if (g != null) list.Add(g);
        }
        return list.ToArray();
    }

    void BuildPalette()
    {
        _track = Art.Mat(new Color(0.30f, 0.32f, 0.37f), 0.1f, 0.2f);
        _sleeper = Art.Mat(new Color(0.33f, 0.25f, 0.17f), 0f, 0.15f);
        _line = Art.Mat(new Color(0.95f, 0.85f, 0.32f), 0f, 0.3f);
        _curb = Art.Mat(new Color(0.42f, 0.44f, 0.48f), 0f, 0.3f);

        _building = new Material[]
        {
            Art.Mat(new Color(0.56f, 0.53f, 0.5f), 0f, 0.2f),
            Art.Mat(new Color(0.43f, 0.47f, 0.55f), 0f, 0.2f),
            Art.Mat(new Color(0.6f, 0.46f, 0.41f), 0f, 0.2f),
            Art.Mat(new Color(0.47f, 0.5f, 0.47f), 0f, 0.25f),
        };
        _window = Art.Glow(new Color(0.55f, 0.7f, 0.82f), new Color(0.33f, 0.45f, 0.55f), 0.65f);
        _pole = Art.Mat(new Color(0.24f, 0.25f, 0.29f), 0.6f, 0.5f);
        _lamp = Art.Glow(new Color(1f, 0.95f, 0.72f), new Color(1f, 0.86f, 0.45f), 0.7f);

        _barrier = Art.Mat(new Color(0.96f, 0.73f, 0.1f), 0f, 0.35f);
        _stripe = Art.Mat(new Color(0.13f, 0.13f, 0.14f), 0f, 0.3f);
        _train = Art.Mat(new Color(0.82f, 0.19f, 0.2f), 0.25f, 0.55f);
        _trainTrim = Art.Mat(new Color(0.17f, 0.18f, 0.21f), 0.3f, 0.5f);
        _trainWin = Art.Glow(new Color(0.55f, 0.85f, 0.95f), new Color(0.3f, 0.6f, 0.72f), 0.7f);
        _gate = Art.Mat(new Color(0.78f, 0.3f, 0.86f), 0.3f, 0.5f);
        _sign = Art.Mat(new Color(0.95f, 0.95f, 0.96f), 0f, 0.4f);

        _coinMat = Art.Glow(new Color(1f, 0.82f, 0.13f), new Color(0.85f, 0.6f, 0.05f), 0.85f);
        _powerCore = Art.Mat(new Color(0.97f, 0.97f, 1f), 0f, 0.6f);

        _powerMat = new Material[6];
        for (int i = 1; i <= 5; i++)
        {
            Color c = GameManager.PowerColor((GameManager.PowerUp)i);
            _powerMat[i] = Art.Glow(c, c * 0.7f, 0.75f);
        }

        // High-contrast accessibility markers — colour encodes the required action.
        _markJump = Art.Glow(new Color(0.25f, 1f, 0.4f), new Color(0.2f, 0.95f, 0.35f), 0.8f);
        _markSlide = Art.Glow(new Color(0.3f, 0.85f, 1f), new Color(0.25f, 0.78f, 0.97f), 0.8f);
        _markDodge = Art.Glow(new Color(1f, 0.32f, 0.86f), new Color(0.92f, 0.26f, 0.8f), 0.8f);
    }

    void Update()
    {
        if (player == null) return;
        float pz = player.transform.position.z;

        RecycleTiles(pz);

        while (_nextRowZ < pz + SpawnAhead)
        {
            SpawnRow(_nextRowZ);
            _nextRowZ += RowGap;
        }
        while (_nextPropZ < pz + SpawnAhead)
        {
            SpawnProps(_nextPropZ);
            _nextPropZ += PropGap;
        }

        CullAndAnimate(pz);

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentState != GameManager.State.Playing) return;

        Vector3 playerPos = player.transform.position;
        bool magnet = gm.ActivePower == GameManager.PowerUp.Magnet;
        if (magnet)
        {
            for (int i = 0; i < _coins.Count; i++)
            {
                Transform ct = _coins[i].go.transform;
                if (Mathf.Abs(ct.position.z - playerPos.z) < 16f)
                    ct.position = Vector3.MoveTowards(ct.position, playerPos, 17f * Time.deltaTime);
            }
        }

        Bounds pb = player.CollisionBounds;

        if (gm.ActivePower != GameManager.PowerUp.Jetpack)
        {
            for (int i = 0; i < _hazards.Count; i++)
            {
                if (_hazards[i].go != null && pb.Intersects(_hazards[i].bounds))
                {
                    if (gm.ConsumeShield())
                    {
                        Effects.Crash(_hazards[i].go.transform.position,
                                      GameManager.PowerColor(GameManager.PowerUp.Shield));
                        Destroy(_hazards[i].go);
                        _hazards.RemoveAt(i);
                        break;
                    }
                    gm.GameOver();
                    return;
                }
            }
        }

        for (int i = _coins.Count - 1; i >= 0; i--)
        {
            GameObject cg = _coins[i].go;
            if (cg == null) { _coins.RemoveAt(i); continue; }
            Bounds cb = new Bounds(cg.transform.position, new Vector3(0.95f, 0.95f, 0.95f));
            if (pb.Intersects(cb))
            {
                gm.AddCoin();
                Effects.CoinSparkle(cg.transform.position);
                Destroy(cg);
                _coins.RemoveAt(i);
            }
        }

        for (int i = _powers.Count - 1; i >= 0; i--)
        {
            GameObject pg = _powers[i].go;
            if (pg == null) { _powers.RemoveAt(i); continue; }
            Bounds wb = new Bounds(pg.transform.position, new Vector3(1.3f, 1.3f, 1.3f));
            if (pb.Intersects(wb))
            {
                gm.ActivatePower(_powers[i].type);
                Effects.Pickup(pg.transform.position, GameManager.PowerColor(_powers[i].type));
                Destroy(pg);
                _powers.RemoveAt(i);
            }
        }

        // Near-miss combo: passing a hazard in your own lane means you jumped or
        // slid past it — reward the skilful dodge. Jetpack flies over everything,
        // so it does not count.
        if (gm.ActivePower != GameManager.PowerUp.Jetpack)
        {
            for (int i = 0; i < _hazards.Count; i++)
            {
                Hazard h = _hazards[i];
                if (h.go == null || h.passed) continue;
                if (h.go.transform.position.z < playerPos.z)
                {
                    h.passed = true;
                    if (Mathf.Abs(h.bounds.center.x - playerPos.x) < 1.3f)
                    {
                        gm.NearMiss();
                        Effects.CoinSparkle(h.bounds.center + new Vector3(0f, 0.4f, 0f));
                    }
                }
            }
        }
    }

    // --------------------------------------------------------------- track
    void RecycleTiles(float pz)
    {
        for (int i = 0; i < _tiles.Count; i++)
        {
            Transform t = _tiles[i];
            if (t.position.z + TileLength * 0.5f < pz - DespawnBehind)
            {
                t.position = new Vector3(0f, 0f, _nextTileZ + TileLength * 0.5f);
                _nextTileZ += TileLength;
            }
        }
    }

    void SpawnTile()
    {
        GameObject tile = new GameObject("Tile");
        tile.transform.SetParent(transform);
        tile.transform.position = new Vector3(0f, 0f, _nextTileZ + TileLength * 0.5f);

        Art.Solid(PrimitiveType.Cube, tile.transform, new Vector3(0f, -0.5f, 0f),
                  new Vector3(TileWidth, 1f, TileLength), _track, "Ground");
        Art.Solid(PrimitiveType.Cube, tile.transform, new Vector3(-TileWidth * 0.5f + 0.25f, 0.3f, 0f),
                  new Vector3(0.5f, 0.8f, TileLength), _curb, "CurbL");
        Art.Solid(PrimitiveType.Cube, tile.transform, new Vector3(TileWidth * 0.5f - 0.25f, 0.3f, 0f),
                  new Vector3(0.5f, 0.8f, TileLength), _curb, "CurbR");
        Art.Solid(PrimitiveType.Cube, tile.transform, new Vector3(-1.3f, 0.04f, 0f),
                  new Vector3(0.13f, 0.08f, TileLength), _line, "LineL");
        Art.Solid(PrimitiveType.Cube, tile.transform, new Vector3(1.3f, 0.04f, 0f),
                  new Vector3(0.13f, 0.08f, TileLength), _line, "LineR");

        const int ties = 6;
        for (int i = 0; i < ties; i++)
        {
            float lz = -TileLength * 0.5f + TileLength * (i + 0.5f) / ties;
            Art.Solid(PrimitiveType.Cube, tile.transform, new Vector3(0f, 0.05f, lz),
                      new Vector3(TileWidth - 1.7f, 0.1f, 0.55f), _sleeper, "Tie");
        }

        _tiles.Add(tile.transform);
        _nextTileZ += TileLength;
    }

    // ------------------------------------------------------------- scenery
    void SpawnProps(float z)
    {
        SpawnBuilding(-1, z);
        SpawnBuilding(1, z);
        if (_propParity % 2 == 0)
        {
            SpawnLamp(-1, z + PropGap * 0.5f);
            SpawnLamp(1, z + PropGap * 0.5f);
        }
        if (_cars != null && _cars.Length > 0 && Random.value < 0.75f)
            SpawnCar(_propParity % 2 == 0 ? -1 : 1, z);
        _propParity++;
    }

    void SpawnCar(int side, float z)
    {
        GameObject prefab = _cars[Random.Range(0, _cars.Length)];
        GameObject car = Instantiate(prefab);
        car.name = "Car";
        car.transform.SetParent(transform);
        float sc = Random.Range(2.5f, 3.3f);
        car.transform.localScale = new Vector3(sc, sc, sc);
        car.transform.position = new Vector3(side * Random.Range(7.4f, 9.6f),
                                             0f, z + Random.Range(-2f, 2f));
        car.transform.rotation = Quaternion.Euler(0f, Random.value < 0.5f ? 0f : 180f, 0f);
        _props.Add(car);
    }

    void SpawnBuilding(int side, float z)
    {
        if (_buildings != null && _buildings.Length > 0)
        {
            GameObject prefab = _buildings[Random.Range(0, _buildings.Length)];
            GameObject kb = Instantiate(prefab);
            kb.name = "Building";
            kb.transform.SetParent(transform);
            float sc = Random.Range(4f, 7f);
            kb.transform.localScale = new Vector3(sc, sc * Random.Range(0.9f, 1.7f), sc);
            kb.transform.position = new Vector3(side * Random.Range(11.5f, 21f),
                                                0f, z + Random.Range(-3f, 3f));
            kb.transform.rotation = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
            _props.Add(kb);
            return;
        }

        float w = Random.Range(3.8f, 7f);
        float d = Random.Range(4f, 8f);
        float h = Random.Range(6f, 24f);
        float x = side * (8f + Random.Range(0f, 7f) + w * 0.5f);

        GameObject b = new GameObject("Building");
        b.transform.SetParent(transform);
        b.transform.position = new Vector3(x, 0f, z + Random.Range(-3f, 3f));

        Material body = _building[Random.Range(0, _building.Length)];
        Art.Solid(PrimitiveType.Cube, b.transform, new Vector3(0f, h * 0.5f, 0f),
                  new Vector3(w, h, d), body, "Body");

        int floors = Mathf.Clamp(Mathf.RoundToInt(h / 3.4f), 2, 6);
        for (int i = 1; i < floors; i++)
        {
            float wy = h * i / floors;
            Art.Solid(PrimitiveType.Cube, b.transform,
                      new Vector3(-side * (w * 0.5f + 0.04f), wy, 0f),
                      new Vector3(0.1f, 0.55f, d * 0.78f), _window, "WinSide");
            Art.Solid(PrimitiveType.Cube, b.transform,
                      new Vector3(0f, wy, -(d * 0.5f + 0.04f)),
                      new Vector3(w * 0.78f, 0.55f, 0.1f), _window, "WinFront");
        }

        _props.Add(b);
    }

    void SpawnLamp(int side, float z)
    {
        GameObject lamp = new GameObject("Lamp");
        lamp.transform.SetParent(transform);
        lamp.transform.position = new Vector3(side * 6.3f, 0f, z);

        Art.Solid(PrimitiveType.Cylinder, lamp.transform, new Vector3(0f, 2.7f, 0f),
                  new Vector3(0.18f, 2.7f, 0.18f), _pole, "Pole");
        Art.Solid(PrimitiveType.Cube, lamp.transform, new Vector3(-side * 0.6f, 5.35f, 0f),
                  new Vector3(1.2f, 0.16f, 0.18f), _pole, "Arm");
        Art.Solid(PrimitiveType.Sphere, lamp.transform, new Vector3(-side * 1.05f, 5.2f, 0f),
                  new Vector3(0.45f, 0.4f, 0.45f), _lamp, "Bulb");

        _props.Add(lamp);
    }

    // ------------------------------------------------- hazards/coins/powers
    void SpawnRow(float z)
    {
        // Difficulty ramps with distance: fewer empty rows, more double-blocks
        // (but never all three lanes, so a path always exists).
        GameManager gm = GameManager.Instance;
        float diff = gm != null ? Mathf.Clamp01(gm.Distance / 2500f) : 0f;
        float zeroChance = Mathf.Lerp(0.30f, 0.10f, diff);
        float twoChance = Mathf.Lerp(0.16f, 0.50f, diff);
        float roll = Random.value;
        int hazardCount = roll < zeroChance ? 0 : (roll > 1f - twoChance ? 2 : 1);

        int[] lanes = { 0, 1, 2 };
        for (int i = 0; i < 3; i++)
        {
            int j = Random.Range(i, 3);
            int tmp = lanes[i]; lanes[i] = lanes[j]; lanes[j] = tmp;
        }

        bool[] blocked = new bool[3];
        for (int i = 0; i < hazardCount; i++)
        {
            blocked[lanes[i]] = true;
            int hk = SpawnHazard(lanes[i], z);
            // Tempt a jump: arc collectible coins over a low barrier.
            if (hk == 0 && Random.value < 0.5f) SpawnCoinArc(lanes[i], z);
        }

        int coinLane = -1;
        for (int i = 0; i < 3; i++)
        {
            if (!blocked[lanes[i]]) { coinLane = lanes[i]; break; }
        }

        if (coinLane >= 0 && Random.value < 0.82f)
        {
            int n = Random.Range(3, 6);
            for (int k = 0; k < n; k++)
                SpawnCoin(coinLane, z + k * 2.4f);
        }

        if (coinLane >= 0 && Random.value < 0.14f)
        {
            GameManager.PowerUp type = (GameManager.PowerUp)Random.Range(1, 6);
            SpawnPowerUp(coinLane, z + 7f, type);
        }
    }

    int SpawnHazard(int lane, float z)
    {
        // kind 0 = low barrier (jump), 1 = train (switch lane), 2 = gate (slide)
        int kind = Random.value < 0.45f ? 0 : (Random.value < 0.72f ? 1 : 2);

        Vector3 size, center;
        int trainCars = 1;
        if (kind == 0)
        {
            size = new Vector3(2.0f, 1.0f, 1.5f);
            center = new Vector3(GameManager.LaneX[lane], 0.5f, z);
        }
        else if (kind == 1)
        {
            // Long, multi-car trains appear more often as the run gets harder:
            // you must already be clear of the lane — no last-moment swerve.
            GameManager gm = GameManager.Instance;
            float diff = gm != null ? Mathf.Clamp01(gm.Distance / 2500f) : 0f;
            if (Random.value < 0.3f + diff * 0.4f) trainCars = Random.Range(2, 4);
            size = new Vector3(2.0f, 3.0f, 1.5f + (trainCars - 1) * 2.4f);
            center = new Vector3(GameManager.LaneX[lane], 1.5f, z);
        }
        else
        {
            size = new Vector3(2.0f, 1.6f, 1.5f);
            center = new Vector3(GameManager.LaneX[lane], 2.4f, z);
        }

        GameObject go = new GameObject("Hazard");
        go.transform.SetParent(transform);
        go.transform.position = center;

        if (kind == 0) BuildBarrier(go.transform);
        else if (kind == 1) BuildTrain(go.transform, trainCars);
        else BuildGate(go.transform);

        if (GameData.HighContrast)
            BuildHazardMarker(go.transform, kind, 4.4f - center.y);

        _hazards.Add(new Hazard { go = go, bounds = new Bounds(center, size), kind = kind, centerY = center.y });
        return kind;
    }

    /// <summary>Adds or removes high-contrast cues on all live hazards to match the setting.</summary>
    public void RefreshHazardCues()
    {
        bool on = GameData.HighContrast;
        for (int i = 0; i < _hazards.Count; i++)
        {
            GameObject go = _hazards[i].go;
            if (go == null) continue;

            bool hasCue = go.transform.Find("Cue") != null;
            if (on && !hasCue)
            {
                BuildHazardMarker(go.transform, _hazards[i].kind, 4.4f - _hazards[i].centerY);
            }
            else if (!on && hasCue)
            {
                for (int c = go.transform.childCount - 1; c >= 0; c--)
                {
                    Transform child = go.transform.GetChild(c);
                    if (child.name == "Cue") Destroy(child.gameObject);
                }
            }
        }
    }

    /// <summary>
    /// Floating action cue shown above a hazard in high-contrast mode: a green
    /// up-chevron (jump), cyan down-chevron (slide) or magenta X (switch lane).
    /// </summary>
    void BuildHazardMarker(Transform parent, int kind, float y)
    {
        Vector3 bar = new Vector3(0.16f, 0.78f, 0.16f);
        if (kind == 1) // train: switch lanes — an X
        {
            GameObject a = Art.Solid(PrimitiveType.Cube, parent, new Vector3(0f, y, 0f), bar, _markDodge, "Cue");
            a.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            GameObject b = Art.Solid(PrimitiveType.Cube, parent, new Vector3(0f, y, 0f), bar, _markDodge, "Cue");
            b.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            return;
        }

        // Chevron: up for jump (kind 0), down for slide (kind 2).
        Material m = kind == 0 ? _markJump : _markSlide;
        float left = kind == 0 ? -40f : 40f;
        GameObject l = Art.Solid(PrimitiveType.Cube, parent, new Vector3(-0.25f, y, 0f), bar, m, "Cue");
        l.transform.localRotation = Quaternion.Euler(0f, 0f, left);
        GameObject r = Art.Solid(PrimitiveType.Cube, parent, new Vector3(0.25f, y, 0f), bar, m, "Cue");
        r.transform.localRotation = Quaternion.Euler(0f, 0f, -left);
    }

    void BuildBarrier(Transform p)
    {
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0f, 0f, 0f),
                  new Vector3(1.95f, 0.92f, 1.05f), _barrier, "Base");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0f, 0.5f, 0f),
                  new Vector3(2.05f, 0.16f, 1.15f), _stripe, "Rail");
        for (int i = -1; i <= 1; i++)
            Art.Solid(PrimitiveType.Cube, p, new Vector3(i * 0.58f, -0.04f, -0.54f),
                      new Vector3(0.34f, 0.66f, 0.06f), _stripe, "Stripe");
    }

    void BuildTrain(Transform p, int cars)
    {
        if (cars < 1) cars = 1;
        // Cars run along Z; the front car (nearest the player, lowest local Z)
        // gets the windshield and headlights.
        for (int c = 0; c < cars; c++)
        {
            float cz = (c - (cars - 1) * 0.5f) * 2.4f;
            BuildTrainCar(p, cz, c == 0);
        }
    }

    void BuildTrainCar(Transform p, float cz, bool front)
    {
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0f, -0.05f, cz),
                  new Vector3(1.9f, 2.5f, 2.2f), _train, "Body");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0f, 1.32f, cz),
                  new Vector3(2.0f, 0.34f, 2.3f), _trainTrim, "Roof");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0f, -1.32f, cz),
                  new Vector3(1.95f, 0.42f, 2.25f), _trainTrim, "Skirt");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(-0.96f, 0.55f, cz),
                  new Vector3(0.06f, 0.62f, 1.5f), _trainWin, "WinL");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0.96f, 0.55f, cz),
                  new Vector3(0.06f, 0.62f, 1.5f), _trainWin, "WinR");

        if (front)
        {
            Art.Solid(PrimitiveType.Cube, p, new Vector3(0f, 0.55f, cz - 1.11f),
                      new Vector3(1.45f, 0.62f, 0.06f), _trainWin, "WinFront");
            Art.Solid(PrimitiveType.Cube, p, new Vector3(-0.55f, -0.62f, cz - 1.12f),
                      new Vector3(0.24f, 0.24f, 0.06f), _lamp, "HeadL");
            Art.Solid(PrimitiveType.Cube, p, new Vector3(0.55f, -0.62f, cz - 1.12f),
                      new Vector3(0.24f, 0.24f, 0.06f), _lamp, "HeadR");
        }
    }

    void BuildGate(Transform p)
    {
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0f, 0f, 0f),
                  new Vector3(2.5f, 1.5f, 0.6f), _gate, "Bar");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0f, 0f, -0.36f),
                  new Vector3(1.1f, 0.85f, 0.12f), _sign, "Sign");
        for (int s = -1; s <= 1; s += 2)
        {
            Art.Solid(PrimitiveType.Cube, p, new Vector3(s * 1.18f, -0.75f, 0f),
                      new Vector3(0.24f, 3.3f, 0.55f), _gate, "Post");
            Art.Solid(PrimitiveType.Cube, p, new Vector3(s * 1.18f, 0.86f, 0f),
                      new Vector3(0.34f, 0.2f, 0.65f), _stripe, "Cap");
        }
    }

    void SpawnCoin(int lane, float z) { SpawnCoin(lane, z, 1.0f); }

    void SpawnCoin(int lane, float z, float y)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Coin";
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(transform);
        go.transform.localScale = new Vector3(0.62f, 0.07f, 0.62f);
        go.transform.position = new Vector3(GameManager.LaneX[lane], y, z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = _coinMat;
        _coins.Add(new Pickup { go = go, baseY = y });
    }

    /// <summary>A shallow arc of coins peaking above a barrier, collected by jumping.</summary>
    void SpawnCoinArc(int lane, float z)
    {
        const int n = 5;
        for (int k = 0; k < n; k++)
        {
            float frac = k / (float)(n - 1);
            float zz = z - 3f + frac * 6f;
            float y = 1.5f + Mathf.Sin(frac * Mathf.PI) * 1.15f;
            SpawnCoin(lane, zz, y);
        }
    }

    void SpawnPowerUp(int lane, float z, GameManager.PowerUp type)
    {
        GameObject go = new GameObject("PowerUp");
        go.transform.SetParent(transform);
        go.transform.position = new Vector3(GameManager.LaneX[lane], 1.5f, z);

        Art.Solid(PrimitiveType.Cube, go.transform, Vector3.zero,
                  new Vector3(0.9f, 0.9f, 0.9f), _powerMat[(int)type], "Glow");
        GameObject core = Art.Solid(PrimitiveType.Cube, go.transform, Vector3.zero,
                  new Vector3(0.55f, 0.55f, 0.55f), _powerCore, "Core");
        core.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);

        _powers.Add(new Power { go = go, type = type, baseY = 1.5f });
    }

    // ------------------------------------------------------- cull / animate
    void CullAndAnimate(float pz)
    {
        for (int i = _hazards.Count - 1; i >= 0; i--)
        {
            if (_hazards[i].go == null) { _hazards.RemoveAt(i); continue; }
            if (_hazards[i].go.transform.position.z < pz - DespawnBehind)
            {
                Destroy(_hazards[i].go);
                _hazards.RemoveAt(i);
            }
        }

        for (int i = _props.Count - 1; i >= 0; i--)
        {
            if (_props[i] == null) { _props.RemoveAt(i); continue; }
            if (_props[i].transform.position.z < pz - DespawnBehind)
            {
                Destroy(_props[i]);
                _props.RemoveAt(i);
            }
        }

        float t = Time.time;
        for (int i = _coins.Count - 1; i >= 0; i--)
        {
            if (_coins[i].go == null) { _coins.RemoveAt(i); continue; }
            Transform ct = _coins[i].go.transform;
            if (ct.position.z < pz - DespawnBehind)
            {
                Destroy(_coins[i].go);
                _coins.RemoveAt(i);
                continue;
            }
            ct.Rotate(0f, 240f * Time.deltaTime, 0f, Space.World);
            Vector3 cp = ct.position;
            cp.y = _coins[i].baseY + Mathf.Sin(t * 3f + cp.z) * 0.12f;
            ct.position = cp;
        }

        for (int i = _powers.Count - 1; i >= 0; i--)
        {
            if (_powers[i].go == null) { _powers.RemoveAt(i); continue; }
            Transform wt = _powers[i].go.transform;
            if (wt.position.z < pz - DespawnBehind)
            {
                Destroy(_powers[i].go);
                _powers.RemoveAt(i);
                continue;
            }
            wt.Rotate(0f, 110f * Time.deltaTime, 0f, Space.World);
            Vector3 wp = wt.position;
            wp.y = _powers[i].baseY + Mathf.Sin(t * 2.4f + wp.z) * 0.18f;
            wt.position = wp;
        }
    }
}
