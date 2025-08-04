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

		GenerateSceneObjects(map);
		
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

	private void GenerateSceneObjects(MapBlock[][] map)
	{
		for (int i = 0; i < map.Length; i++)
		{
			for (int j = 0; j < map[0].Length; j++)
			{
				switch (map[i][j].BlockType)
				{
					case MapBlockType.PLAIN:
						var pb = PreparePlainBlock(map, i, j);
						if(pb != null)
							this.AddChild(pb);
						break;
					case MapBlockType.RIVER:
						var rb = PrepareRiverBlock(map, i, j);
						if(rb != null)
							this.AddChild(rb);
						break;
					default:
						break;
				}

				switch (map[i][j].StructureType)
				{
					case MapBlockStructureType.BASE:

						break;
				}
			}
		}
	}

	private Node3D PreparePlainBlock(MapBlock[][] map, int i, int j)
	{
		// TODO METY
	}

	private Node3D PrepareRiverBlock(MapBlock[][] map, int i, int j)
	{
		PackedScene riverS = GD.Load<PackedScene>("res://Scenes/Terrains/RiverStraight.tscn");
		PackedScene riverT = GD.Load<PackedScene>("res://Scenes/Terrains/RiverTurn.tscn");

		if (((i - 1) >= 0 && map[i - 1][j].BlockType == MapBlockType.RIVER) &&
		    ((i + 1) < map.Length && map[i + 1][j].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)riverS.Instantiate();
			r.Translate(new Vector3(j * 5, 0, i * 5));
			return r;
		}

		if (((j - 1) >= 0 && map[i][j - 1].BlockType == MapBlockType.RIVER) && ((j + 1) < map.Length && map[i][j + 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)riverS.Instantiate();
			r.RotateY((float)Math.PI / 2);
			r.Translate(new Vector3(j * 5, 0, i * 5));
			return r;
		}

		if (((i - 1) >= 0 && map[i - 1][j].BlockType == MapBlockType.RIVER) &&
		    ((j - 1) >= 0 && map[i][j - 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)riverT.Instantiate();
			r.Translate(new Vector3(j * 5, 0, i * 5));
			return r;
		}

		if (((i - 1) >= 0 && map[i - 1][j].BlockType == MapBlockType.RIVER) && ((j + 1) < map.Length && map[i][j + 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)riverT.Instantiate();
			r.RotateY((float)(3 * Math.PI / 2));
			r.Translate(new Vector3(j * 5, 0, i * 5));
			return r;
		}

		if (((i + 1) < map.Length && map[i + 1][j].BlockType == MapBlockType.RIVER) && ((j + 1) < map.Length && map[i][j + 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)riverT.Instantiate();
			r.RotateY((float)Math.PI);
			r.Translate(new Vector3(j * 5, 0, i * 5));
			return r;
		}

		if (((i + 1) < map.Length && map[i + 1][j].BlockType == MapBlockType.RIVER) && ((j - 1) >= 0 && map[i][j - 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)riverT.Instantiate();
			r.RotateY((float)Math.PI / 2);
			r.Translate(new Vector3(j * 5, 0, i * 5));
			return r;
		}

		return null;
	}
}
