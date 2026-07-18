using System;

/// <summary>
/// Which kinds of entity may occupy a pathfinding cell.
/// Used as a bitmask: each cell stores the set of domains allowed to enter it, so one grid
/// serves every movement type instead of needing a separate blocked map per unit class.
/// </summary>
[Flags]
public enum MovementDomain : byte
{
    None = 0,

    /// <summary>Walks/drives on terrain - Soldier, Tank. Stopped by trees, buildings, AIR_ONLY blocks.</summary>
    Ground = 1 << 0,

    /// <summary>Flies over terrain - Heli. Stopped only by RESTRICTED blocks.</summary>
    Air = 1 << 1,

    All = Ground | Air
}
