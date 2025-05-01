using Godot;

public interface IGameMap
{
    void SpawnEntity(Node3D entity);
    void GenerateLevel();
    void ImportLevelData(string data);
}