using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class InfiniteTilemapChunks : MonoBehaviour
{
    [Header("Props")]
    public GameObject[] propPrefabs;
    [Range(0f, 0.2f)] public float propChance = 0.03f; // 3% per tile
    public int maxPropsPerChunk = 25;
    public Transform propsParent; // optional (empty GameObject)

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

    private Vector2Int _currentPlayerChunk;
    private readonly HashSet<Vector2Int> _loadedChunks = new();
    private readonly List<Vector3Int> _positionsBuffer = new(32 * 32);
    private readonly List<TileBase> _tilesBuffer = new(32 * 32);
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
        // Convert world -> cell -> chunk
        Vector3Int cell = tilemap.WorldToCell(worldPos);
        int cx = FloorDiv(cell.x, chunkSize);
        int cy = FloorDiv(cell.y, chunkSize);
        return new Vector2Int(cx, cy);
    }

    void UpdateLoadedChunks(bool force)
    {
        // Determine which chunks should be loaded
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
                _loadedChunks.Add(c);
            }
        }

        // Unload far (clear tiles)
        // Collect first to avoid modifying set while iterating
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

        // We generate in cell coordinates
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

    float SampleNoise(int x, int y)
    {
        // Seed offsets so different worlds look different but stay consistent
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

    // Correct floor division for negatives (so chunks work in all directions)
    int FloorDiv(int a, int b)
    {
        int q = a / b;
        int r = a % b;
        if (r != 0 && ((r > 0) != (b > 0))) q--;
        return q;
    }
}
