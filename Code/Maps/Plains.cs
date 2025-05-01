using Godot;
using System;

public partial class Plains : Node3D, IGameMap
{
	private PackedScene _playerScene = GD.Load<PackedScene>("res://Scenes/Game/Player.tscn");
	private Node _entities;
	
	public override void _Ready()
	{
		_entities = GetNode<Node>("Entities");
	}
	
	public void SpawnEntity(Node3D entity)
	{
		_entities.AddChild(entity);
	}

	public void GenerateLevel()
	{
		var mm = new MapGenerator(100, 100);
		var map = mm.GenerateMinimap();

		var minimap = GetNode<Minimap>("Minimap");
		if (minimap != null)
			minimap.Generate(map);
	}

	public void ImportLevelData(string data)
	{
		var dData = System.Text.Json.JsonSerializer.Deserialize<MapBlock[][]>(data);
		var minimap = GetNode<Minimap>("Minimap");
		if (minimap != null)
			minimap.Generate(dData);
	}
}
