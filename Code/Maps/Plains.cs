using Godot;
using System;
using tacticals.Code.Maps;
using static MapBlock;

public partial class Plains : Node3D, IGameMap
{
	private PackedScene _playerScene = GD.Load<PackedScene>("res://Scenes/Game/Player.tscn");
	private PackedScene _teamflag, _tank, _tower, _bunker, _treeB1, _treeB2, _treeC1, _treeC2, _treeC3, _treeC4, _treeC5, _treeC6;
	private Node _entities;
	private MapBlock[][] _map;

	public override void _Ready()
	{
		_entities = GetNode<Node>("Entities");
	}

    private void LoadResources()
    {
        _teamflag = GD.Load<PackedScene>("res://Scenes/Structures/TeamFlag.tscn");
        _tank = GD.Load<PackedScene>("res://Scenes/Structures/Tank.tscn");
        _tower = GD.Load<PackedScene>("res://Scenes/Structures/Tower.tscn");
        _bunker = GD.Load<PackedScene>("res://Scenes/Structures/Bunker.tscn");
        _treeB1 = GD.Load<PackedScene>("res://Scenes/Biome/TreeB1.tscn");
        _treeB2 = GD.Load<PackedScene>("res://Scenes/Biome/TreeB2.tscn");
        _treeC1 = GD.Load<PackedScene>("res://Scenes/Biome/TreeC1.tscn");
        _treeC2 = GD.Load<PackedScene>("res://Scenes/Biome/TreeC2.tscn");
        _treeC3 = GD.Load<PackedScene>("res://Scenes/Biome/TreeC3.tscn");
        _treeC4 = GD.Load<PackedScene>("res://Scenes/Biome/TreeC4.tscn");
        _treeC5 = GD.Load<PackedScene>("res://Scenes/Biome/TreeC5.tscn");
        _treeC6 = GD.Load<PackedScene>("res://Scenes/Biome/TreeC6.tscn");
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

        LoadResources();
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
                        var tf = (Node3D)_teamflag.Instantiate();
                        tf.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, (float)0.5, j * MapConstants.BLOCK_SIZE));
                        if (tf != null)
                            this.AddChild(tf);
                        break;
					case MapBlockStructureType.TANK:
                        var t = (Node3D)_tank.Instantiate();
                        t.Position = (new Vector3(i * MapConstants.BLOCK_SIZE,(float)0.5, j * MapConstants.BLOCK_SIZE));
                        if (t != null)
                            this.AddChild(t);
                        break;
                    case MapBlockStructureType.TOWER:
                        var to = (Node3D)_tower.Instantiate();
                        to.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, (float)0.5, j * MapConstants.BLOCK_SIZE));
                        if (to != null)
                            this.AddChild(to);
                        break;
                    case MapBlockStructureType.BUNKER:
                        var b = (Node3D)_bunker.Instantiate();
                        b.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, (float)0.5, j * MapConstants.BLOCK_SIZE));
                        if (b != null)
                            this.AddChild(b);
                        break;
                }
				
				// generate biomes
				if (map[i][j].BiomeInfo != null)
				{
					foreach (var bd in map[i][j].BiomeInfo)
					{
						Node3D tree = null;
						switch (bd.Type)
						{
							case MapBlock.BiomeDataType.TREEB1:
								tree = (Node3D)_treeB1.Instantiate();
								break;
							case MapBlock.BiomeDataType.TREEB2:
								tree = (Node3D)_treeB1.Instantiate();
								break;
							case MapBlock.BiomeDataType.TREEC1:
								tree = (Node3D)_treeC1.Instantiate();
								break;
							case MapBlock.BiomeDataType.TREEC2:
								tree = (Node3D)_treeC2.Instantiate();
								break;
							case MapBlock.BiomeDataType.TREEC3:
								tree = (Node3D)_treeC3.Instantiate();
								break;
							case MapBlock.BiomeDataType.TREEC4:
								tree = (Node3D)_treeC4.Instantiate();
								break;
							case MapBlock.BiomeDataType.TREEC5:
								tree = (Node3D)_treeC5.Instantiate();
								break;
							case MapBlock.BiomeDataType.TREEC6:
								tree = (Node3D)_treeC6.Instantiate();
								break;
						}
						
						tree.GlobalPosition = map[i][j].GlobalPosition + bd.LocalCoord;
						this.AddChild(tree);
					}
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
