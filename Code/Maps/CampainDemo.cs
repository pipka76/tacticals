using Godot;
using System;

public partial class CampainDemo :  Node3D, IGameMap
{
    private PackedScene _playerScene = GD.Load<PackedScene>("res://Scenes/Game/Player.tscn");
    //private PackedScene _teamflag, _tank, _tower, _bunker, _treeB1, _treeB2, _treeC1, _treeC2, _treeC3, _treeC4, _treeC5, _treeC6;
    private Node _entities;
    private MapBlock[][] _map;
    private PackedScene _grassP, _riverS, _riverT;
    private FlowFieldManager _mgr = new FlowFieldManager();
	
    public override void _Ready()
    {
        _entities = GetNode<Node>("Entities");
    }

    public void GenerateLevel()
    {
//        var mm = new MapGenerator(100, 100);
//        _map = mm.GenerateMap(_mgr);

        LoadResources();
		
        var minimap = GetNode<Minimap>("Minimap");
        if (minimap != null && _map != null)
            minimap.Generate(_map);
    }

    public void ToggleMinimap()
    {
        var minimap = GetNode<Minimap>("Minimap");
        if (minimap != null)
            minimap.Visible = !minimap.Visible;
    }
    
    private void LoadResources()
    {
    }

    public Vector2 GetMyBasePosition()
    {
        return Vector2.Zero;
    }

    public void SpawnPlayer()
    {
    }

    public void SpawnEntity(Node3D entity)
    {
    }

    public void ImportLevelData(string data)
    {
    }


    public float GetTerrainHeight(Vector2 coords)
    {
        return 0;
    }
}
