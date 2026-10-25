using System.Collections.Generic;
using Godot;
using tacticals.Code.Game;

public interface IGameMap
{
    /// <summary>
    /// The level's pathfinding grid: cell passability per MovementDomain, plus the per-cell
    /// terrain speed factor. Populated during GenerateLevel.
    /// </summary>
    FlowFieldManager PathField { get; }

    IEnumerable<TeamEntity> GetEntities(TeamMembership? memberOf = null);
    /// <summary>Spawns the local player, owned by this peer (1 when offline).</summary>
    Player SpawnPlayer(Vector2 globalFlatPosition);
    public void SpawnEntity(Node3D entity, Vector2 globalFlatPosition);
    void GenerateLevel();
    /// <summary>Like GenerateLevel, but builds the level from a map generated elsewhere (the game server).</summary>
    void LoadLevel(MapBlock[][] map);
    void ToggleMinimap();
    float GetTerrainHeight(Vector2 coords);
}