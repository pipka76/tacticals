using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Immutable key for caching a flow field. Extend this with whatever makes fields differ in your game
/// (agent radius, team, movement type, etc.).
/// </summary>
public readonly struct FlowFieldKey : IEquatable<FlowFieldKey>
{
    public readonly Vector2I GoalCell;
    public readonly int AgentProfileId;
    public readonly bool UseDiagonals;

    public FlowFieldKey(Vector2I goalCell, int agentProfileId, bool useDiagonals)
    {
        GoalCell = goalCell;
        AgentProfileId = agentProfileId;
        UseDiagonals = useDiagonals;
    }

    public bool Equals(FlowFieldKey other)
        => GoalCell == other.GoalCell && AgentProfileId == other.AgentProfileId && UseDiagonals == other.UseDiagonals;

    public override bool Equals(object obj) => obj is FlowFieldKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int h = 17;
            h = (h * 31) + GoalCell.GetHashCode();
            h = (h * 31) + AgentProfileId;
            h = (h * 31) + (UseDiagonals ? 1 : 0);
            return h;
        }
    }
}

/// <summary>
/// One flow field (integration + flow vectors) for a specific goal (and profile).
/// It references map data owned by FlowFieldManager.
/// </summary>
public sealed class FlowFieldData
{
    public const float INF = 1_000_000f;

    public int Width { get; }
    public int Height { get; }
    public float CellSize { get; }
    public Vector2 Origin { get; }

    public Vector2I GoalCell { get; private set; } = new Vector2I(-1, -1);
    public bool UseDiagonals { get; }

    /// <summary>Version of the map this field was built against.</summary>
    public int BuiltOnMapVersion { get; private set; } = -1;

    private readonly bool[] _blocked;
    private readonly float[] _baseCost;

    private readonly float[] _integration;
    private readonly Vector2[] _flow;

    private static readonly Vector2I[] Neighbors4 =
    {
        new Vector2I( 1, 0),
        new Vector2I(-1, 0),
        new Vector2I( 0, 1),
        new Vector2I( 0,-1),
    };

    private static readonly Vector2I[] Neighbors8 =
    {
        new Vector2I( 1, 0),
        new Vector2I(-1, 0),
        new Vector2I( 0, 1),
        new Vector2I( 0,-1),
        new Vector2I( 1, 1),
        new Vector2I( 1,-1),
        new Vector2I(-1, 1),
        new Vector2I(-1,-1),
    };

    public FlowFieldData(int width, int height, float cellSize, Vector2 origin, bool useDiagonals, bool[] blockedRef, float[] baseCostRef)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("Grid must be > 0.");
        Width = width;
        Height = height;
        CellSize = cellSize;
        Origin = origin;
        UseDiagonals = useDiagonals;

        _blocked = blockedRef ?? throw new ArgumentNullException(nameof(blockedRef));
        _baseCost = baseCostRef ?? throw new ArgumentNullException(nameof(baseCostRef));

        int n = width * height;
        _integration = new float[n];
        _flow = new Vector2[n];

        Clear();
    }

    public void Clear()
    {
        for (int i = 0; i < _integration.Length; i++)
        {
            _integration[i] = INF;
            _flow[i] = Vector2.Zero;
        }

        GoalCell = new Vector2I(-1, -1);
        BuiltOnMapVersion = -1;
    }

    public bool Build(Vector2I goalCell, int mapVersion)
    {
        if (!InBounds(goalCell)) return false;
        if (IsBlocked(goalCell)) return false;

        GoalCell = goalCell;

        ComputeIntegration(goalCell);
        ComputeFlow();

        BuiltOnMapVersion = mapVersion;
        return true;
    }

    // --- Sampling ---
    public Vector2 SampleFlow(Vector2I cell)
    {
        if (!InBounds(cell)) return Vector2.Zero;
        return _flow[Idx(cell)];
    }

    public Vector2 SampleFlowWorld(Vector2 worldPos)
    {
        var cell = WorldToCell(worldPos);
        return SampleFlow(cell);
    }

    public float GetIntegration(Vector2I cell)
    {
        if (!InBounds(cell)) return INF;
        return _integration[Idx(cell)];
    }

    public bool IsBlocked(Vector2I cell)
    {
        if (!InBounds(cell)) return true;
        return _blocked[Idx(cell)];
    }

    public Vector2I WorldToCell(Vector2 worldPos)
    {
        Vector2 local = (worldPos - Origin) / CellSize;
        return new Vector2I(Mathf.FloorToInt(local.X), Mathf.FloorToInt(local.Y));
    }

    public Vector2 CellToWorldCenter(Vector2I cell)
    {
        return Origin + new Vector2((cell.X + 0.5f) * CellSize, (cell.Y + 0.5f) * CellSize);
    }

    // --- Internals ---
    private void ComputeIntegration(Vector2I goalCell)
    {
        for (int i = 0; i < _integration.Length; i++)
            _integration[i] = INF;

        int goalIndex = Idx(goalCell);
        _integration[goalIndex] = 0f;

        var pq = new PriorityQueue<int, float>();
        pq.Enqueue(goalIndex, 0f);

        Vector2I[] neighbors = UseDiagonals ? Neighbors8 : Neighbors4;

        while (pq.Count > 0)
        {
            int currentIndex = pq.Dequeue();
            float currentCost = _integration[currentIndex];

            Vector2I current = ToCell(currentIndex);

            foreach (var off in neighbors)
            {
                Vector2I nxt = current + off;
                if (!InBounds(nxt)) continue;

                int ni = Idx(nxt);
                if (_blocked[ni]) continue;

                // Prevent corner cutting on diagonals
                if (UseDiagonals && off.X != 0 && off.Y != 0)
                {
                    Vector2I c1 = new Vector2I(current.X + off.X, current.Y);
                    Vector2I c2 = new Vector2I(current.X, current.Y + off.Y);
                    if (InBounds(c1) && InBounds(c2))
                    {
                        if (_blocked[Idx(c1)] || _blocked[Idx(c2)])
                            continue;
                    }
                }

                float stepLen = (off.X != 0 && off.Y != 0) ? 1.41421356f : 1f;
                float stepCost = _baseCost[ni] * stepLen;

                float candidate = currentCost + stepCost;
                if (candidate < _integration[ni])
                {
                    _integration[ni] = candidate;
                    pq.Enqueue(ni, candidate);
                }
            }
        }
    }

    private void ComputeFlow()
    {
        Vector2I[] neighbors = UseDiagonals ? Neighbors8 : Neighbors4;

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            var cell = new Vector2I(x, y);
            int i = Idx(cell);

            if (_blocked[i] || _integration[i] >= INF * 0.5f)
            {
                _flow[i] = Vector2.Zero;
                continue;
            }

            float best = _integration[i];
            Vector2I bestN = cell;

            foreach (var off in neighbors)
            {
                Vector2I n = cell + off;
                if (!InBounds(n)) continue;

                int ni = Idx(n);
                float val = _integration[ni];
                if (val < best)
                {
                    best = val;
                    bestN = n;
                }
            }

            if (bestN == cell)
            {
                _flow[i] = Vector2.Zero;
            }
            else
            {
                Vector2 dir = new Vector2(bestN.X - cell.X, bestN.Y - cell.Y).Normalized();
                _flow[i] = dir;
            }
        }
    }

    private bool InBounds(Vector2I c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;
    private int Idx(Vector2I c) => c.X + c.Y * Width;
    private Vector2I ToCell(int idx) => new Vector2I(idx % Width, idx / Width);
}

/// <summary>
/// Owns the map grid (blocked + base costs) and caches FlowFieldData per (goal, agent profile, diagonal mode).
/// Make this an Autoload (singleton) if you want global access.
/// </summary>
public partial class FlowFieldManager : Node
{
    // --- Map config ---
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float CellSize { get; private set; } = 16f;
    public Vector2 Origin { get; private set; } = Vector2.Zero;

    // Map state
    private bool[] _blocked;
    private float[] _baseCost;

    /// <summary>Increments whenever map passability/cost changes.</summary>
    public int MapVersion { get; private set; } = 0;

    // Cache
    private readonly Dictionary<FlowFieldKey, FlowFieldData> _cache = new();

    public bool IsInitialized => _blocked != null && _baseCost != null;

    public void InitializeMap(int width, int height, float cellSize, Vector2 origin)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("Grid must be > 0.");

        Width = width;
        Height = height;
        CellSize = cellSize;
        Origin = origin;

        int n = width * height;
        _blocked = new bool[n];
        _baseCost = new float[n];

        for (int i = 0; i < n; i++)
            _baseCost[i] = 1f;

        _cache.Clear();
        MapVersion++;
    }

    public void ClearMap()
    {
        if (!IsInitialized) return;

        Array.Clear(_blocked, 0, _blocked.Length);
        for (int i = 0; i < _baseCost.Length; i++) _baseCost[i] = 1f;

        _cache.Clear();
        MapVersion++;
    }

    // --- Map editing API ---
    public void SetBlocked(Vector2I cell, bool blocked)
    {
        if (!IsInitialized) return;
        if (!InBounds(cell)) return;

        int idx = Idx(cell);
        if (_blocked[idx] == blocked) return;

        _blocked[idx] = blocked;
        MapVersion++;
    }
    
    public void SetBaseCost(Vector2I cell, float cost)
    {
        if (!IsInitialized) return;
        if (!InBounds(cell)) return;

        int idx = Idx(cell);
        float clamped = Mathf.Max(1f, cost);
        if (Mathf.IsEqualApprox(_baseCost[idx], clamped)) return;

        _baseCost[idx] = clamped;
        MapVersion++;
    }

    public bool IsBlocked(Vector2I cell)
    {
        if (!IsInitialized) return true;
        if (!InBounds(cell)) return true;
        return _blocked[Idx(cell)];
    }

    // --- Field retrieval / caching ---
    /// <summary>
    /// Get a cached flow field for (goalCell, agentProfileId, useDiagonals).
    /// If missing or stale (map version changed), it will be (re)built.
    /// Returns null if goal is invalid/blocked.
    /// </summary>
    public FlowFieldData GetField(Vector2I goalCell, int agentProfileId = 0, bool useDiagonals = true)
    {
        if (!IsInitialized) throw new InvalidOperationException("FlowFieldManager.InitializeMap must be called before GetField().");
        if (!InBounds(goalCell)) return null;
        if (IsBlocked(goalCell)) return null;

        var key = new FlowFieldKey(goalCell, agentProfileId, useDiagonals);

        if (!_cache.TryGetValue(key, out var field))
        {
            field = new FlowFieldData(Width, Height, CellSize, Origin, useDiagonals, _blocked, _baseCost);
            _cache[key] = field;
        }

        if (field.BuiltOnMapVersion != MapVersion || field.GoalCell != goalCell)
        {
            // Rebuild against latest map
            bool ok = field.Build(goalCell, MapVersion);
            if (!ok) return null;
        }

        return field;
    }

    /// <summary>
    /// Remove all cached fields. Useful if you regenerate the entire world.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Helper if you want to force rebuild on next GetField without changing the map arrays.
    /// </summary>
    public void InvalidateAllFields()
    {
        MapVersion++;
    }

    // --- Helpers ---
    public Vector2I WorldToCell(Vector2 worldPos)
    {
        Vector2 local = (worldPos - Origin) / CellSize;
        return new Vector2I(Mathf.FloorToInt(local.X), Mathf.FloorToInt(local.Y));
    }

    public Vector2 CellToWorldCenter(Vector2I cell)
    {
        return Origin + new Vector2((cell.X + 0.5f) * CellSize, (cell.Y + 0.5f) * CellSize);
    }

    private bool InBounds(Vector2I c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;
    private int Idx(Vector2I c) => c.X + c.Y * Width;
}