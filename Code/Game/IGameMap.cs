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
    void SpawnPlayer(Vector2 globalFlatPosition);
    public void SpawnEntity(Node3D entity, Vector2 globalFlatPosition);
    void GenerateLevel();
    void ImportLevelData(string data);
    void ToggleMinimap();
    float GetTerrainHeight(Vector2 coords);
}