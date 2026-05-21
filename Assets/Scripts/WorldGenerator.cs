using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedurally streams the world: recycling ground tiles, spawning hazard and
/// coin rows ahead of the runner, and resolving overlap collisions each frame.
/// </summary>
public class WorldGenerator : MonoBehaviour
{
    public PlayerController player;

    const float TileLength = 24f;
    const float TileWidth = 11f;
    const int TileCount = 9;

    const float RowGap = 13f;
    const float SpawnAhead = 140f;
    const float DespawnBehind = 22f;

    class Hazard { public GameObject go; public Bounds bounds; }
    class Pickup { public GameObject go; }

    readonly List<Transform> _tiles = new List<Transform>();
    readonly List<Hazard> _hazards = new List<Hazard>();
    readonly List<Pickup> _coins = new List<Pickup>();

    float _nextTileZ;
    float _nextRowZ = 34f;
    int _tileParity;

    static readonly Color TileA = new Color(0.31f, 0.33f, 0.39f);
    static readonly Color TileB = new Color(0.26f, 0.28f, 0.33f);

    void Start()
    {
        _nextTileZ = -TileLength;
        for (int i = 0; i < TileCount; i++) SpawnTile();
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

        CullAndSpin(pz);

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentState != GameManager.State.Playing) return;

        Bounds pb = player.CollisionBounds;

        for (int i = 0; i < _hazards.Count; i++)
        {
            if (_hazards[i].go != null && pb.Intersects(_hazards[i].bounds))
            {
                gm.GameOver();
                return;
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
                Destroy(cg);
                _coins.RemoveAt(i);
            }
        }
    }

    void RecycleTiles(float pz)
    {
        for (int i = 0; i < _tiles.Count; i++)
        {
            Transform t = _tiles[i];
            if (t.position.z + TileLength * 0.5f < pz - DespawnBehind)
            {
                t.position = new Vector3(0f, -0.5f, _nextTileZ + TileLength * 0.5f);
                _nextTileZ += TileLength;
                Recolor(t.gameObject, (_tileParity++ % 2 == 0) ? TileA : TileB);
            }
        }
    }

    void CullAndSpin(float pz)
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
        for (int i = _coins.Count - 1; i >= 0; i--)
        {
            if (_coins[i].go == null) { _coins.RemoveAt(i); continue; }
            if (_coins[i].go.transform.position.z < pz - DespawnBehind)
            {
                Destroy(_coins[i].go);
                _coins.RemoveAt(i);
                continue;
            }
            _coins[i].go.transform.Rotate(0f, 230f * Time.deltaTime, 0f, Space.World);
        }
    }

    void SpawnTile()
    {
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name = "Tile";
        tile.transform.SetParent(transform);
        tile.transform.localScale = new Vector3(TileWidth, 1f, TileLength);
        tile.transform.position = new Vector3(0f, -0.5f, _nextTileZ + TileLength * 0.5f);
        Recolor(tile, (_tileParity++ % 2 == 0) ? TileA : TileB);
        _tiles.Add(tile.transform);
        _nextTileZ += TileLength;
    }

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

        // Lay a short coin run down the first lane that stays open.
        if (Random.value < 0.82f)
        {
            int coinLane = -1;
            for (int i = 0; i < 3; i++)
            {
                if (!blocked[lanes[i]]) { coinLane = lanes[i]; break; }
            }
            if (coinLane >= 0)
            {
                int n = Random.Range(3, 6);
                for (int k = 0; k < n; k++)
                    SpawnCoin(coinLane, z + k * 2.4f);
            }
        }
    }

    void SpawnHazard(int lane, float z)
    {
        // kind 0 = low (jump over), 1 = tall (switch lane), 2 = overhead bar (slide under)
        int kind = Random.value < 0.45f ? 0 : (Random.value < 0.72f ? 1 : 2);

        Vector3 size, center;
        Color col;
        if (kind == 0)
        {
            size = new Vector3(2.0f, 1.0f, 1.5f);
            center = new Vector3(GameManager.LaneX[lane], 0.5f, z);
            col = new Color(1f, 0.55f, 0.1f);
        }
        else if (kind == 1)
        {
            size = new Vector3(2.0f, 3.0f, 1.5f);
            center = new Vector3(GameManager.LaneX[lane], 1.5f, z);
            col = new Color(0.88f, 0.16f, 0.16f);
        }
        else
        {
            size = new Vector3(2.0f, 1.6f, 1.5f);
            center = new Vector3(GameManager.LaneX[lane], 2.4f, z);
            col = new Color(0.62f, 0.26f, 0.86f);
        }

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Hazard";
        go.transform.SetParent(transform);
        go.transform.localScale = size;
        go.transform.position = center;
        Recolor(go, col);

        _hazards.Add(new Hazard { go = go, bounds = new Bounds(center, size) });
    }

    void SpawnCoin(int lane, float z)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Coin";
        go.transform.SetParent(transform);
        go.transform.localScale = new Vector3(0.55f, 0.06f, 0.55f);
        go.transform.position = new Vector3(GameManager.LaneX[lane], 0.95f, z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Recolor(go, new Color(1f, 0.84f, 0.15f));
        _coins.Add(new Pickup { go = go });
    }

    static void Recolor(GameObject go, Color c)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = c;
    }
}
