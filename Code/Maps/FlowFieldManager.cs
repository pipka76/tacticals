using Godot;
using System;
using System.Collections.Generic;
using tacticals.Code.Maps;

/// <summary>
/// Immutable key for caching a flow field. Extend this with whatever makes fields differ in your game
/// (agent radius, team, etc.).
/// </summary>
public readonly struct FlowFieldKey : IEquatable<FlowFieldKey>
{
    public readonly Vector2I GoalCell;
    public readonly MovementDomain Domain;
    public readonly bool UseDiagonals;

    public FlowFieldKey(Vector2I goalCell, MovementDomain domain, bool useDiagonals)
    {
        GoalCell = goalCell;
        Domain = domain;
        UseDiagonals = useDiagonals;
    }

    public bool Equals(FlowFieldKey other)
        => GoalCell == other.GoalCell && Domain == other.Domain && UseDiagonals == other.UseDiagonals;

    public override bool Equals(object obj) => obj is FlowFieldKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int h = 17;
            h = (h * 31) + GoalCell.GetHashCode();
            h = (h * 31) + (int)Domain;
            h = (h * 31) + (UseDiagonals ? 1 : 0);
            return h;
        }
    }
}

/// <summary>
/// One flow field (integration + flow vectors) for a specific goal and movement domain.
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

    /// <summary>The movement domain this field routes for. Cells not passable to it are treated as walls.</summary>
    public MovementDomain Domain { get; }

    /// <summary>Version of the map this field was built against.</summary>
    public int BuiltOnMapVersion { get; private set; } = -1;

    /// <summary>Per-cell bitmask of the domains allowed to enter. Owned by FlowFieldManager.</summary>
    private readonly byte[] _passable;

    /// <summary>
    /// Per-cell step cost, currently uniform 1.0 everywhere.
    /// Do NOT wire terrain into this: difficult terrain must slow units down, not reroute them,
    /// so that the player drives route choice. Terrain lives in FlowFieldManager._moveFactor.
    /// </summary>
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

    public FlowFieldData(int width, int height, float cellSize, Vector2 origin, bool useDiagonals, MovementDomain domain, byte[] passableRef, float[] baseCostRef)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("Grid must be > 0.");
        if (domain == MovementDomain.None) throw new ArgumentException("Domain must name at least one movement type.", nameof(domain));
        Width = width;
        Height = height;
        CellSize = cellSize;
        Origin = origin;
        UseDiagonals = useDiagonals;
        Domain = domain;

        _passable = passableRef ?? throw new ArgumentNullException(nameof(passableRef));
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

    /// <summary>True if this field's domain cannot enter the cell. Out of bounds counts as blocked.</summary>
    public bool IsBlocked(Vector2I cell)
    {
        if (!InBounds(cell)) return true;
        return IsBlockedAt(Idx(cell));
    }

    private bool IsBlockedAt(int idx) => (_passable[idx] & (byte)Domain) == 0;

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
                if (IsBlockedAt(ni)) continue;

                if (DiagonalBlocked(current, off)) continue;

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

            if (IsBlockedAt(i) || _integration[i] >= INF * 0.5f)
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

                // Must match ComputeIntegration, otherwise the field points units diagonally
                // through a corner gap the integration pass declared impassable.
                if (DiagonalBlocked(cell, off)) continue;

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

    /// <summary>
    /// True when a diagonal step from <paramref name="from"/> by <paramref name="off"/> would cut
    /// the corner between two blocked cells. Shared by the integration and flow passes so they
    /// cannot disagree about which diagonals exist.
    /// No bounds guard needed: the flanking cells are (from+off).X paired with from.Y and vice
    /// versa, and the caller has already bounds-checked from+off, so both are always in range.
    /// </summary>
    private bool DiagonalBlocked(Vector2I from, Vector2I off)
    {
        if (!UseDiagonals || off.X == 0 || off.Y == 0)
            return false;

        return IsBlockedAt(Idx(new Vector2I(from.X + off.X, from.Y)))
            || IsBlockedAt(Idx(new Vector2I(from.X, from.Y + off.Y)));
    }

    private bool InBounds(Vector2I c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;
    private int Idx(Vector2I c) => c.X + c.Y * Width;
    private Vector2I ToCell(int idx) => new Vector2I(idx % Width, idx / Width);
}

/// <summary>
/// Owns the map grid (passability, step cost, terrain speed) and caches FlowFieldData per
/// (goal, movement domain, diagonal mode).
/// Owned by the map (see IGameMap implementations) - plain C# object, deliberately not a Node:
/// it uses no scene-tree facilities, and as an unparented Node it would leak on every level load.
/// </summary>
public sealed class FlowFieldManager
{
    // --- Map config ---
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float CellSize { get; private set; } = 16f;
    public Vector2 Origin { get; private set; } = Vector2.Zero;

    // Map state, in two layers.
    //
    // The base layer is the terrain as generated: authored once by MapGenerator and never
    // mutated afterwards. The effective layer is what everything actually reads, and is the
    // base recombined with every active TerrainEdit covering the cell.
    //
    // Two layers rather than one because edits have to be REMOVABLE. Player structures get
    // destroyed, cancelled and refunded, and a single mutable grid cannot express that: with
    // only Block()/SetMoveFactor() there is no way to undo a tower's footprint without also
    // wiping the forest or trench it happened to overlap.
    private byte[] _basePassable;
    private float[] _baseMoveFactor;

    // One byte per cell holding a MovementDomain bitmask of who may enter it.
    private byte[] _passable;
    private float[] _baseCost;

    /// <summary>
    /// Per-cell ground movement-speed multiplier, 1.0 = unimpeded.
    /// Deliberately NOT read by the Dijkstra integration pass and deliberately not handed to
    /// FlowFieldData: terrain slows units down, it never changes which route they take.
    /// </summary>
    private float[] _moveFactor;

    /// <summary>Increments whenever map passability/cost changes.</summary>
    public int MapVersion { get; private set; } = 0;

    // Cache
    private readonly Dictionary<FlowFieldKey, FlowFieldData> _cache = new();

    /// <summary>One removable contribution laid over the terrain - a tower, bunker or trench.</summary>
    private sealed class EditRecord
    {
        public Vector2I Min;               // inclusive cell bounds
        public Vector2I Max;
        public MovementDomain Blocks;      // domains this edit denies (None for a pure slowdown)
        public float MoveFactor;           // 1f for a pure obstacle

        public bool Covers(Vector2I c) => c.X >= Min.X && c.X <= Max.X && c.Y >= Min.Y && c.Y <= Max.Y;

        public bool Overlaps(EditRecord o) =>
            Min.X <= o.Max.X && Max.X >= o.Min.X && Min.Y <= o.Max.Y && Max.Y >= o.Min.Y;
    }

    private readonly Dictionary<int, EditRecord> _edits = new();
    private int _nextEditId = 1;

    public bool IsInitialized => _passable != null && _baseCost != null;

    public void InitializeMap(int width, int height, float cellSize, Vector2 origin)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("Grid must be > 0.");

        Width = width;
        Height = height;
        CellSize = cellSize;
        Origin = origin;

        int n = width * height;
        _basePassable = new byte[n];
        _baseMoveFactor = new float[n];
        _passable = new byte[n];
        _baseCost = new float[n];
        _moveFactor = new float[n];

        for (int i = 0; i < n; i++)
        {
            _basePassable[i] = (byte)MovementDomain.All;
            _baseMoveFactor[i] = 1f;
            _passable[i] = (byte)MovementDomain.All;
            _baseCost[i] = 1f;
            _moveFactor[i] = 1f;
        }

        _edits.Clear();
        _cache.Clear();
        MapVersion++;
    }

    public void ClearMap()
    {
        if (!IsInitialized) return;

        for (int i = 0; i < _passable.Length; i++)
        {
            _basePassable[i] = (byte)MovementDomain.All;
            _baseMoveFactor[i] = 1f;
            _passable[i] = (byte)MovementDomain.All;
            _baseCost[i] = 1f;
            _moveFactor[i] = 1f;
        }

        _edits.Clear();
        _cache.Clear();
        MapVersion++;
    }

    // --- Terrain authoring (base layer) ---
    // These write the terrain the level was generated with. Use them from MapGenerator, not for
    // anything the player can later remove - see AddObstacle/AddSlowdown for that.

    /// <summary>
    /// Sets exactly which domains may enter the cell, discarding any previous terrain state.
    /// Use Block() instead when layering one terrain feature on top of others.
    /// </summary>
    public void SetPassable(Vector2I cell, MovementDomain domains)
    {
        if (!IsInitialized) return;
        if (!InBounds(cell)) return;

        int idx = Idx(cell);
        if (_basePassable[idx] == (byte)domains) return;

        _basePassable[idx] = (byte)domains;
        if (RecomputeCell(idx, cell))
            MapVersion++;
    }

    /// <summary>
    /// Removes the given domains from the cell's passable set, leaving the others intact.
    /// Block(cell, MovementDomain.Ground) on a tree still lets a Heli fly over it.
    /// </summary>
    public void Block(Vector2I cell, MovementDomain domains)
    {
        if (!IsInitialized) return;
        if (!InBounds(cell)) return;

        int idx = Idx(cell);
        byte updated = (byte)(_basePassable[idx] & ~(byte)domains);
        if (_basePassable[idx] == updated) return;

        _basePassable[idx] = updated;
        if (RecomputeCell(idx, cell))
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

    /// <summary>
    /// Slows ground movement through the cell. Takes the strongest (lowest) factor seen, so
    /// overlapping features compound rather than the last writer winning.
    /// Does NOT bump MapVersion - this is not pathfinding state, and invalidating every cached
    /// field over a speed tweak would be pure waste.
    /// </summary>
    public void SetMoveFactor(Vector2I cell, float factor)
    {
        if (!IsInitialized) return;
        if (!InBounds(cell)) return;

        int idx = Idx(cell);
        float clamped = Mathf.Clamp(factor, MapConstants.MOVE_FACTOR_MIN, 1f);
        if (clamped >= _baseMoveFactor[idx]) return;

        _baseMoveFactor[idx] = clamped;
        RecomputeCell(idx, cell);
    }

    /// <summary>Ground movement-speed multiplier for the cell. 1.0 when uninitialized or out of bounds.</summary>
    public float GetMoveFactor(Vector2I cell)
    {
        if (!IsInitialized) return 1f;
        if (!InBounds(cell)) return 1f;
        return _moveFactor[Idx(cell)];
    }

    /// <summary>Ground movement-speed multiplier at a world XZ position.</summary>
    public float GetMoveFactorWorld(Vector2 worldXZ) => GetMoveFactor(WorldToCell(worldXZ));

    /// <summary>True if the given domain may enter the cell. Out of bounds is never passable.</summary>
    public bool IsPassable(Vector2I cell, MovementDomain domain)
    {
        if (!IsInitialized) return false;
        if (!InBounds(cell)) return false;
        return (_passable[Idx(cell)] & (byte)domain) != 0;
    }

    /// <summary>The full set of domains allowed to enter the cell.</summary>
    public MovementDomain GetPassable(Vector2I cell)
    {
        if (!IsInitialized) return MovementDomain.None;
        if (!InBounds(cell)) return MovementDomain.None;
        return (MovementDomain)_passable[Idx(cell)];
    }

    // --- Removable edits (player-built structures) ---

    /// <summary>
    /// A tower, bunker or trench laid over the terrain. Keep it and pass it to RemoveEdit when
    /// the structure is destroyed or the placement is cancelled.
    /// </summary>
    public readonly struct TerrainEdit
    {
        internal readonly int Id;
        internal TerrainEdit(int id) { Id = id; }
        public bool IsValid => Id != 0;
    }

    /// <summary>
    /// Denies <paramref name="blocks"/> across a world-space footprint - a tower or bunker.
    /// </summary>
    public TerrainEdit AddObstacle(Rect2 worldArea, MovementDomain blocks)
        => AddEdit(worldArea, blocks, 1f);

    /// <summary>
    /// Slows ground movement across a world-space footprint without blocking it - a trench.
    /// Composes with terrain and other edits by taking the strongest (lowest) factor.
    /// </summary>
    public TerrainEdit AddSlowdown(Rect2 worldArea, float factor)
        => AddEdit(worldArea, MovementDomain.None, factor);

    private TerrainEdit AddEdit(Rect2 worldArea, MovementDomain blocks, float factor)
    {
        if (!IsInitialized) return default;
        if (!ToCellBounds(worldArea, out var min, out var max)) return default;

        var record = new EditRecord
        {
            Min = min,
            Max = max,
            Blocks = blocks,
            MoveFactor = Mathf.Clamp(factor, MapConstants.MOVE_FACTOR_MIN, 1f)
        };

        int id = _nextEditId++;
        _edits[id] = record;

        // Applying an edit only ever subtracts, so the cells can be folded in directly
        // rather than rebuilt from the base layer.
        bool passabilityChanged = false;
        for (int y = min.Y; y <= max.Y; y++)
        {
            for (int x = min.X; x <= max.X; x++)
            {
                int idx = Idx(new Vector2I(x, y));

                byte updated = (byte)(_passable[idx] & ~(byte)record.Blocks);
                if (updated != _passable[idx])
                {
                    _passable[idx] = updated;
                    passabilityChanged = true;
                }

                if (record.MoveFactor < _moveFactor[idx])
                    _moveFactor[idx] = record.MoveFactor;
            }
        }

        // Speed alone never invalidates a field - it does not affect route choice.
        if (passabilityChanged)
            MapVersion++;

        return new TerrainEdit(id);
    }

    /// <summary>
    /// Lifts an edit and restores its footprint from the terrain plus whatever other edits still
    /// overlap it, so demolishing a tower cannot erase the forest or trench underneath it.
    /// </summary>
    public void RemoveEdit(TerrainEdit handle)
    {
        if (!IsInitialized) return;
        if (!_edits.TryGetValue(handle.Id, out var removed)) return;

        _edits.Remove(handle.Id);

        // Only edits overlapping the vacated footprint can contribute to those cells.
        var overlapping = new List<EditRecord>();
        foreach (var e in _edits.Values)
        {
            if (e.Overlaps(removed))
                overlapping.Add(e);
        }

        bool passabilityChanged = false;
        for (int y = removed.Min.Y; y <= removed.Max.Y; y++)
        {
            for (int x = removed.Min.X; x <= removed.Max.X; x++)
            {
                var cell = new Vector2I(x, y);
                if (RecomputeCell(Idx(cell), cell, overlapping))
                    passabilityChanged = true;
            }
        }

        if (passabilityChanged)
            MapVersion++;
    }

    /// <summary>Number of edits currently laid over the terrain.</summary>
    public int ActiveEditCount => _edits.Count;

    /// <summary>
    /// Rebuilds one cell's effective value from the base terrain plus the edits covering it.
    /// Returns true when passability changed, which is what invalidates cached fields.
    /// </summary>
    private bool RecomputeCell(int idx, Vector2I cell, List<EditRecord> candidates = null)
    {
        byte passable = _basePassable[idx];
        float factor = _baseMoveFactor[idx];

        if (candidates != null)
        {
            foreach (var e in candidates)
            {
                if (!e.Covers(cell)) continue;
                passable = (byte)(passable & ~(byte)e.Blocks);
                if (e.MoveFactor < factor) factor = e.MoveFactor;
            }
        }
        else
        {
            foreach (var e in _edits.Values)
            {
                if (!e.Covers(cell)) continue;
                passable = (byte)(passable & ~(byte)e.Blocks);
                if (e.MoveFactor < factor) factor = e.MoveFactor;
            }
        }

        bool passabilityChanged = _passable[idx] != passable;
        _passable[idx] = passable;
        _moveFactor[idx] = factor;

        return passabilityChanged;
    }

    /// <summary>Clamps a world-space rect onto the grid. False when it lies entirely outside.</summary>
    private bool ToCellBounds(Rect2 worldArea, out Vector2I min, out Vector2I max)
    {
        var a = WorldToCell(worldArea.Position);
        var b = WorldToCell(worldArea.End);

        min = new Vector2I(Mathf.Max(Mathf.Min(a.X, b.X), 0), Mathf.Max(Mathf.Min(a.Y, b.Y), 0));
        max = new Vector2I(Mathf.Min(Mathf.Max(a.X, b.X), Width - 1), Mathf.Min(Mathf.Max(a.Y, b.Y), Height - 1));

        return min.X <= max.X && min.Y <= max.Y;
    }

    // --- Field retrieval / caching ---
    /// <summary>
    /// Get a cached flow field for (goalCell, domain, useDiagonals).
    /// If missing or stale (map version changed), it will be (re)built.
    /// Returns null if the goal is out of bounds or not passable to that domain.
    /// </summary>
    public FlowFieldData GetField(Vector2I goalCell, MovementDomain domain, bool useDiagonals = true)
    {
        if (!IsInitialized) throw new InvalidOperationException("FlowFieldManager.InitializeMap must be called before GetField().");
        if (domain == MovementDomain.None) throw new ArgumentException("Domain must name at least one movement type.", nameof(domain));
        if (!InBounds(goalCell)) return null;
        if (!IsPassable(goalCell, domain)) return null;

        var key = new FlowFieldKey(goalCell, domain, useDiagonals);

        if (!_cache.TryGetValue(key, out var field))
        {
            field = new FlowFieldData(Width, Height, CellSize, Origin, useDiagonals, domain, _passable, _baseCost);
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