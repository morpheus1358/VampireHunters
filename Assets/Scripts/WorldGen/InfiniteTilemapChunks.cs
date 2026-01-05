using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class InfiniteTilemapChunks : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Tilemap tilemap;

    [Header("Tiles (3 grass variants)")]
    public TileBase grassA;
    public TileBase grassB;
    public TileBase grassC;

    [Header("Chunk Settings")]
    [Tooltip("Chunk size in tiles (you said 32).")]
    public int chunkSize = 32;

    [Tooltip("How many chunks to keep loaded in each direction from player chunk.")]
    public int viewDistanceChunks = 2; // 2 => loads (2*2+1)^2 = 25 chunks

    [Header("Noise / Randomness")]
    public int seed = 12345;

    [Tooltip("Bigger = larger patches, smaller = more speckle.")]
    public float noiseScale = 18f;

    [Range(0f, 1f)] public float thresholdA = 0.65f;
    [Range(0f, 1f)] public float thresholdB = 0.9f;
    // > thresholdB => C

    [Header("Props")]
    [Tooltip("Drop your 22 prop prefabs here.")]
    public GameObject[] propPrefabs;

    [Tooltip("Chance per tile to attempt placing a prop (0.03 = 3%).")]
    [Range(0f, 0.2f)] public float propChance = 0.03f;

    [Tooltip("Hard cap on props per chunk.")]
    public int maxPropsPerChunk = 25;

    [Tooltip("Optional parent to keep hierarchy clean (create empty 'Props' and assign).")]
    public Transform propsParent;

    [Tooltip("Small random offset so props don't look like perfect grid placement.")]
    [Range(0f, 0.5f)] public float propJitter = 0.2f;

    private Vector2Int _currentPlayerChunk;
    private readonly HashSet<Vector2Int> _loadedChunks = new();

    private readonly List<Vector3Int> _positionsBuffer = new(32 * 32);
    private readonly List<TileBase> _tilesBuffer = new(32 * 32);

    // Track spawned props per chunk so we can delete them when chunk unloads
    private readonly Dictionary<Vector2Int, List<GameObject>> _chunkProps = new();

    void Start()
    {
        if (!player || !tilemap || !grassA || !grassB || !grassC)
        {
            Debug.LogError("Assign player, tilemap, and all 3 grass tiles in the inspector.");
            enabled = false;
            return;
        }

        _currentPlayerChunk = WorldToChunk(player.position);
        UpdateLoadedChunks(force: true);
    }

    void Update()
    {
        var newChunk = WorldToChunk(player.position);
        if (newChunk != _currentPlayerChunk)
        {
            _currentPlayerChunk = newChunk;
            UpdateLoadedChunks(force: false);
        }
    }

    Vector2Int WorldToChunk(Vector3 worldPos)
    {
        Vector3Int cell = tilemap.WorldToCell(worldPos);
        int cx = FloorDiv(cell.x, chunkSize);
        int cy = FloorDiv(cell.y, chunkSize);
        return new Vector2Int(cx, cy);
    }

    void UpdateLoadedChunks(bool force)
    {
        HashSet<Vector2Int> needed = new();

        for (int dy = -viewDistanceChunks; dy <= viewDistanceChunks; dy++)
        {
            for (int dx = -viewDistanceChunks; dx <= viewDistanceChunks; dx++)
            {
                needed.Add(new Vector2Int(_currentPlayerChunk.x + dx, _currentPlayerChunk.y + dy));
            }
        }

        // Load missing
        foreach (var c in needed)
        {
            if (force || !_loadedChunks.Contains(c))
            {
                GenerateChunk(c);
                SpawnPropsForChunk(c);

                _loadedChunks.Add(c);
            }
        }

        // Unload chunks not needed
        List<Vector2Int> toRemove = null;
        foreach (var c in _loadedChunks)
        {
            if (!needed.Contains(c))
            {
                toRemove ??= new List<Vector2Int>();
                toRemove.Add(c);
            }
        }

        if (toRemove != null)
        {
            foreach (var c in toRemove)
            {
                ClearChunk(c);
                ClearPropsForChunk(c);

                _loadedChunks.Remove(c);
            }
        }
    }

    void GenerateChunk(Vector2Int chunkCoord)
    {
        _positionsBuffer.Clear();
        _tilesBuffer.Clear();

        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int cellX = startX + x;
                int cellY = startY + y;

                float n = SampleNoise(cellX, cellY);
                TileBase t = PickTile(n);

                _positionsBuffer.Add(new Vector3Int(cellX, cellY, 0));
                _tilesBuffer.Add(t);
            }
        }

        tilemap.SetTiles(_positionsBuffer.ToArray(), _tilesBuffer.ToArray());
    }

    void ClearChunk(Vector2Int chunkCoord)
    {
        _positionsBuffer.Clear();
        _tilesBuffer.Clear();

        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                _positionsBuffer.Add(new Vector3Int(startX + x, startY + y, 0));
                _tilesBuffer.Add(null);
            }
        }

        tilemap.SetTiles(_positionsBuffer.ToArray(), _tilesBuffer.ToArray());
    }

    void SpawnPropsForChunk(Vector2Int chunkCoord)
    {
        // Avoid double-spawning if chunk regenerates for any reason
        if (_chunkProps.ContainsKey(chunkCoord)) return;

        if (propPrefabs == null || propPrefabs.Length == 0) return;
        if (maxPropsPerChunk <= 0 || propChance <= 0f) return;

        // Deterministic random per chunk (so props are stable)
        int chunkSeed = seed ^ (chunkCoord.x * 73856093) ^ (chunkCoord.y * 19349663);
        System.Random rng = new System.Random(chunkSeed);

        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;

        int spawned = 0;
        var list = new List<GameObject>(Mathf.Min(maxPropsPerChunk, 64));
        _chunkProps[chunkCoord] = list;

        // Optional: you can bias prop placement using noise too.
        // For now: simple chance per tile until max reached.
        for (int y = 0; y < chunkSize && spawned < maxPropsPerChunk; y++)
        {
            for (int x = 0; x < chunkSize && spawned < maxPropsPerChunk; x++)
            {
                if (rng.NextDouble() > propChance) continue;

                int cellX = startX + x;
                int cellY = startY + y;

                Vector3 worldPos = tilemap.GetCellCenterWorld(new Vector3Int(cellX, cellY, 0));

                // Random prefab
                var prefab = propPrefabs[rng.Next(propPrefabs.Length)];
                if (!prefab) continue;

                // Small jitter so it doesn't look like perfect grid
                float jx = (float)(rng.NextDouble() - 0.5) * propJitter;
                float jy = (float)(rng.NextDouble() - 0.5) * propJitter;

                var go = Instantiate(prefab, worldPos + new Vector3(jx, jy, 0f), Quaternion.identity, propsParent);
                list.Add(go);
                spawned++;
            }
        }
    }

    void ClearPropsForChunk(Vector2Int chunkCoord)
    {
        if (_chunkProps.TryGetValue(chunkCoord, out var list))
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i]) Destroy(list[i]);
            }
            _chunkProps.Remove(chunkCoord);
        }
    }

    float SampleNoise(int x, int y)
    {
        float sx = (x + seed * 0.0137f) / noiseScale;
        float sy = (y + seed * 0.0271f) / noiseScale;
        return Mathf.PerlinNoise(sx, sy);
    }

    TileBase PickTile(float noise)
    {
        if (noise < thresholdA) return grassA;
        if (noise < thresholdB) return grassB;
        return grassC;
    }

    int FloorDiv(int a, int b)
    {
        int q = a / b;
        int r = a % b;
        if (r != 0 && ((r > 0) != (b > 0))) q--;
        return q;
    }
}
