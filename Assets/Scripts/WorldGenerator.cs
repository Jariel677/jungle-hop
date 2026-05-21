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

    class Hazard { public GameObject go; public Bounds bounds; }
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

    GameObject[] _buildings;

    void Start()
    {
        BuildPalette();
        _buildings = LoadBuildings();
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

        _powerMat = new Material[5];
        for (int i = 1; i <= 4; i++)
        {
            Color c = GameManager.PowerColor((GameManager.PowerUp)i);
            _powerMat[i] = Art.Glow(c, c * 0.7f, 0.75f);
        }
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
        if (_propParity++ % 2 == 0)
        {
            SpawnLamp(-1, z + PropGap * 0.5f);
            SpawnLamp(1, z + PropGap * 0.5f);
        }
    }

    void SpawnBuilding(int side, float z)
    {
        if (_buildings != null && _buildings.Length > 0)
        {
            GameObject prefab = _buildings[Random.Range(0, _buildings.Length)];
            GameObject kb = Instantiate(prefab);
            kb.name = "Building";
            kb.transform.SetParent(transform);
            float sc = Random.Range(4.5f, 8.5f);
            kb.transform.localScale = new Vector3(sc, sc * Random.Range(0.85f, 1.6f), sc);
            kb.transform.position = new Vector3(side * Random.Range(9f, 17f),
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
        int hazardCount = Random.value < 0.25f ? 0 : (Random.value < 0.62f ? 1 : 2);

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
            SpawnHazard(lanes[i], z);
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
            GameManager.PowerUp type = (GameManager.PowerUp)Random.Range(1, 5);
            SpawnPowerUp(coinLane, z + 7f, type);
        }
    }

    void SpawnHazard(int lane, float z)
    {
        // kind 0 = low barrier (jump), 1 = train (switch lane), 2 = gate (slide)
        int kind = Random.value < 0.45f ? 0 : (Random.value < 0.72f ? 1 : 2);

        Vector3 size, center;
        if (kind == 0)
        {
            size = new Vector3(2.0f, 1.0f, 1.5f);
            center = new Vector3(GameManager.LaneX[lane], 0.5f, z);
        }
        else if (kind == 1)
        {
            size = new Vector3(2.0f, 3.0f, 1.5f);
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
        else if (kind == 1) BuildTrain(go.transform);
        else BuildGate(go.transform);

        _hazards.Add(new Hazard { go = go, bounds = new Bounds(center, size) });
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

    void BuildTrain(Transform p)
    {
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0f, -0.05f, 0f),
                  new Vector3(1.9f, 2.5f, 1.4f), _train, "Body");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0f, 1.32f, 0f),
                  new Vector3(2.0f, 0.34f, 1.5f), _trainTrim, "Roof");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0f, -1.32f, 0f),
                  new Vector3(1.95f, 0.42f, 1.45f), _trainTrim, "Skirt");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0f, 0.55f, -0.71f),
                  new Vector3(1.45f, 0.62f, 0.06f), _trainWin, "WinFront");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(-0.96f, 0.55f, 0f),
                  new Vector3(0.06f, 0.62f, 1.0f), _trainWin, "WinL");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0.96f, 0.55f, 0f),
                  new Vector3(0.06f, 0.62f, 1.0f), _trainWin, "WinR");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(-0.55f, -0.62f, -0.72f),
                  new Vector3(0.24f, 0.24f, 0.06f), _lamp, "HeadL");
        Art.Solid(PrimitiveType.Cube, p, new Vector3(0.55f, -0.62f, -0.72f),
                  new Vector3(0.24f, 0.24f, 0.06f), _lamp, "HeadR");
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

    void SpawnCoin(int lane, float z)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Coin";
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(transform);
        go.transform.localScale = new Vector3(0.62f, 0.07f, 0.62f);
        go.transform.position = new Vector3(GameManager.LaneX[lane], 1.0f, z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = _coinMat;
        _coins.Add(new Pickup { go = go, baseY = 1.0f });
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
