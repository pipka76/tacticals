using Godot;
using System;
using tacticals.Code.Maps;

public partial class Plains : Node3D, IGameMap
{
	private PackedScene _playerScene = GD.Load<PackedScene>("res://Scenes/Game/Player.tscn");
	private Node _entities;
	private MapBlock[][] _map;

	public override void _Ready()
	{
		_entities = GetNode<Node>("Entities");
	}

    public void SpawnEntity(Node3D entity)
	{
		var b = FindFirstBase();
        _entities.AddChild(entity);
        if (b != Vector2I.Zero)
            entity.GlobalPosition = new Vector3(b.X * MapConstants.BLOCK_SIZE, 0, b.Y * MapConstants.BLOCK_SIZE);
    }

    private Vector2I FindFirstBase()
	{
		for (int i = 0; i < _map.Length; i++)
		{
			for (int j = 0; j < _map[i].Length; j++)
			{
				if (_map[i][j].StructureType == MapBlockStructureType.BASE)
					return new Vector2I(i, j);
			}
		}

		return Vector2I.Zero;
	}

	public void GenerateLevel()
	{
		var mm = new MapGenerator(100, 100);
		_map = mm.GenerateMinimap();

		GenerateSceneObjects(_map);
		
		var minimap = GetNode<Minimap>("Minimap");
		if (minimap != null)
			minimap.Generate(_map);
	}

	public void ToggleMinimap()
	{
        var minimap = GetNode<Minimap>("Minimap");
		if (minimap != null)
			minimap.Visible = !minimap.Visible;
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
                        PackedScene teamflag = GD.Load<PackedScene>("res://Scenes/Structures/TeamFlag.tscn");

                        var tf = (Node3D)teamflag.Instantiate();
                        tf.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, (float)0.5, j * MapConstants.BLOCK_SIZE));
                        if (tf != null)
                            this.AddChild(tf);
                        break;
					case MapBlockStructureType.TANK:
                        PackedScene tank = GD.Load<PackedScene>("res://Scenes/Structures/Tank.tscn");

                        var t = (Node3D)tank.Instantiate();
                        t.Position = (new Vector3(i * MapConstants.BLOCK_SIZE,(float)0.5, j * MapConstants.BLOCK_SIZE));
                        if (t != null)
                            this.AddChild(t);
                        break;
                    case MapBlockStructureType.TOWER:
                        PackedScene tower = GD.Load<PackedScene>("res://Scenes/Structures/Tower.tscn");

                        var to = (Node3D)tower.Instantiate();
                        to.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, (float)0.5, j * MapConstants.BLOCK_SIZE));
                        if (to != null)
                            this.AddChild(to);
                        break;
                }
			}
		}
	}

	private Node3D PreparePlainBlock(MapBlock[][] map, int i, int j)
	{
        PackedScene grassP = GD.Load<PackedScene>("res://Scenes/Terrains/GrassPlain.tscn");

        var r = (Node3D)grassP.Instantiate();
        r.Translate(new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
        return r;
    }

	private Node3D PrepareRiverBlock(MapBlock[][] map, int i, int j)
	{
		PackedScene riverS = GD.Load<PackedScene>("res://Scenes/Terrains/RiverStraight.tscn");
		PackedScene riverT = GD.Load<PackedScene>("res://Scenes/Terrains/RiverTurn.tscn");

		if (((i - 1) >= 0 && map[i - 1][j].BlockType == MapBlockType.RIVER) &&
		    ((i + 1) < map.Length && map[i + 1][j].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)riverS.Instantiate();
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
		}

		if (((j - 1) >= 0 && map[i][j - 1].BlockType == MapBlockType.RIVER) && ((j + 1) < map.Length && map[i][j + 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)riverS.Instantiate();
			r.RotateY((float)Math.PI / 2);
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
		}

		if (((i - 1) >= 0 && map[i - 1][j].BlockType == MapBlockType.RIVER) &&
		    ((j - 1) >= 0 && map[i][j - 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)riverT.Instantiate();
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
		}

		if (((i - 1) >= 0 && map[i - 1][j].BlockType == MapBlockType.RIVER) && ((j + 1) < map.Length && map[i][j + 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)riverT.Instantiate();
            r.RotateY((float)Math.PI / 2);
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
		}

		if (((i + 1) < map.Length && map[i + 1][j].BlockType == MapBlockType.RIVER) && ((j + 1) < map.Length && map[i][j + 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)riverT.Instantiate();
            r.RotateY((float)Math.PI);
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
		}

		if (((i + 1) < map.Length && map[i + 1][j].BlockType == MapBlockType.RIVER) && ((j - 1) >= 0 && map[i][j - 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)riverT.Instantiate();
            r.RotateY((float)(3 * Math.PI / 2));

            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
		}
        if (((i - 1) >= 0 && map[i - 1][j].BlockType == MapBlockType.RIVER) ||
            ((i + 1) < map.Length && map[i + 1][j].BlockType == MapBlockType.RIVER))
        {
            var r = (Node3D)riverS.Instantiate();
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
        }
        if (((j - 1) >= 0 && map[i][j - 1].BlockType == MapBlockType.RIVER) || ((j + 1) < map.Length && map[i][j + 1].BlockType == MapBlockType.RIVER))
        {
            var r = (Node3D)riverS.Instantiate();
            r.RotateY((float)Math.PI / 2);
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
        }

        return null;
	}
}
