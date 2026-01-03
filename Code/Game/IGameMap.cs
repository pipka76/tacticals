using Godot;

public interface IGameMap
{
    void SpawnPlayer();
    void SpawnEntity(Node3D entity);
    void GenerateLevel();
    void ImportLevelData(string data);
    void ToggleMinimap();
    float GetTerrainHeight(Vector2 coords);
}