using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using tacticals.Code.Game;
using tacticals.Code.Maps;

public partial class CampainDemo :  Node3D, IGameMap
{
    private PackedScene _playerScene = GD.Load<PackedScene>("res://Scenes/Game/Player.tscn");
    //private PackedScene _teamflag, _tank, _tower, _bunker, _treeB1, _treeB2, _treeC1, _treeC2, _treeC3, _treeC4, _treeC5, _treeC6;
    private Node _entities;
    private MapBlock[][] _map;
    private PackedScene _grassP, _riverS, _riverT;
    private FlowFieldManager _mgr = new FlowFieldManager();

    public FlowFieldManager PathField => _mgr;

    public override void _Ready()
    {
        _entities = GetNode<Node>("Entities");
    }

    public void GenerateLevel()
    {
        var mm = new MapGenerator(100, 100);
        var gm = GetNode<Node3D>("TestingFlatGround");
        var meshes = gm.FindChildren("*", "MeshInstance3D", recursive: true, owned: false);
        _map = mm.MapExistingSurface(_mgr, meshes);

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

    public IEnumerable<TeamEntity> GetEntities(TeamMembership? memberOf = null)
    {
        if (!memberOf.HasValue)
            return _entities.GetChildren().Cast<TeamEntity>().AsEnumerable();

        return _entities.GetChildren().Cast<TeamEntity>().Where(te => te.IsMemberOf(memberOf.Value));
    }
    
    public void SpawnPlayer(Vector2 globalFlatPosition)
    {
        var player = (Player)_playerScene.Instantiate();
        GetNode<Node>("Players").AddChild(player);
        player.GlobalPosition = new Vector3(globalFlatPosition.X, 0, globalFlatPosition.Y);
    }

    public void SpawnEntity(Node3D entity, Vector2 globalFlatPosition)
    {
        var b = new Vector2I((int)(globalFlatPosition.X / MapConstants.BLOCK_SIZE),(int)(globalFlatPosition.Y/MapConstants.BLOCK_SIZE));
        _entities.AddChild(entity);
        entity.GlobalPosition = new Vector3(globalFlatPosition.X, _map[b.X][b.Y].GlobalPosition.Y, globalFlatPosition.Y);
    }

    public void ImportLevelData(string data)
    {
    }


    public float GetTerrainHeight(Vector2 coords)
    {
        return 0;
    }

}
