using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using tacticals.Code.Maps;
using static MapBlock;

public partial class Plains : Node3D, IGameMap
{
	private PackedScene _playerScene = GD.Load<PackedScene>("res://Scenes/Game/Player.tscn");
	private PackedScene _teamflag, _tank, _tower, _bunker, _treeB1, _treeB2, _treeC1, _treeC2, _treeC3, _treeC4, _treeC5, _treeC6;
	private Node _entities;
	private MapBlock[][] _map;
	PackedScene _grassP, _riverS, _riverT;

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
        _grassP = GD.Load<PackedScene>("res://Scenes/Terrains/GrassPlain.tscn");
        _riverS = GD.Load<PackedScene>("res://Scenes/Terrains/RiverStraight.tscn");
        _riverT = GD.Load<PackedScene>("res://Scenes/Terrains/RiverTurn.tscn");
	}

    public void SpawnEntity(Node3D entity)
	{
		var b = FindFirstBase();
        _entities.AddChild(entity);
        if (b != Vector2I.Zero)
        {
	        entity.GlobalPosition = _map[b.X][b.Y].GlobalPosition;
	        //entity.SetScale(new Vector3(10,10,10));
        }
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
		_map = mm.GenerateMap();

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

    public float GetTerrainHeight(Vector2 coords)
    {
	    if (coords.X < 0 || coords.X >= (_map.Length * MapConstants.BLOCK_SIZE))
		    return 0f;
	    if (coords.Y < 0 || coords.Y >= (_map[0].Length * MapConstants.BLOCK_SIZE))
		    return 0f;

	    var mx = (int)(coords.X / MapConstants.BLOCK_SIZE);
	    var my = (int)(coords.Y / MapConstants.BLOCK_SIZE);
	    var mapBlock = _map[mx][my];
	    
	    float height = 0f;
	    if (mapBlock.BiomeInfo != null)
	    {
		    var k = 1f / (float)MapConstants.BIOMEHEATMAPSCALE;
		    var kx = (int)(((coords.X / MapConstants.BLOCK_SIZE) - mx) / k);
		    var ky = (int)(((coords.Y / MapConstants.BLOCK_SIZE) - my) / k);
		    var g = mapBlock.BiomeInfo.FirstOrDefault(bi => bi.Type == BiomeDataType.GROUND && (int)bi.LocalCoord.X == kx && (int)bi.LocalCoord.Z == ky);
		    if (g != null)
		    {
			    return g.LocalCoord.Y;
		    }
	    }
	    
	    return 0;
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
						//var pb = PreparePlainBlock(map, i, j);
						//if(pb != null)
							//this.AddChild(pb);
						var pbList = PrepareDetailedPlainBlocks(map, i, j);
						foreach (var pb in pbList)
						{
							this.AddChild(pb);
						}
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
                        tf.Position = map[i][j].GlobalPosition;
                        if (tf != null)
                            this.AddChild(tf);
                        break;
					case MapBlockStructureType.TANK:
                        var t = (Node3D)_tank.Instantiate();
                        t.Position = map[i][j].GlobalPosition;
                        if (t != null)
                            this.AddChild(t);
                        break;
                    case MapBlockStructureType.TOWER:
                        var to = (Node3D)_tower.Instantiate();
                        to.Position = map[i][j].GlobalPosition;
                        if (to != null)
                            this.AddChild(to);
                        break;
                    case MapBlockStructureType.BUNKER:
                        var b = (Node3D)_bunker.Instantiate();
                        b.Position = map[i][j].GlobalPosition;
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
							default:
								continue;
						}
						
						tree.Position = map[i][j].GlobalPosition + bd.LocalCoord;
                        this.AddChild(tree);
					}
				}
			}
		}
	}

	private Node3D PreparePlainBlock(MapBlock[][] map, int i, int j)
	{
        var r = (Node3D)_grassP.Instantiate();
        r.Translate(map[i][j].GlobalPosition);
        return r;
    }

	private IList<Node3D> PrepareDetailedPlainBlocks(MapBlock[][] map, int i, int j)
	{
		const int s = MapConstants.BIOMEHEATMAPSCALE;
		var subBlockSize = (float)MapConstants.BLOCK_SIZE / s;
		IList<Node3D> result = new List<Node3D>();

		// Build a single, smooth terrain mesh for this map block from the height-field samples.
		// We create a (s+1)x(s+1) vertex grid by averaging the surrounding cell heights at each corner.
		if (map[i][j].BiomeInfo == null)
			return result;

		var grounds = map[i][j].BiomeInfo.Where(bi => bi.Type == BiomeDataType.GROUND).ToList();
		if (grounds.Count == 0)
			return result;

		// We need boundary vertices to match neighboring blocks.
		// Instead of using only this block's (s x s) samples, sample heights in *global cell space*
		// so both adjacent blocks compute the exact same height for shared corners.
		var hcellCache = new Dictionary<Vector2I, float[,]>();

		float[,] GetHCellForBlock(int bi, int bj)
		{
			var key = new Vector2I(bi, bj);
			if (hcellCache.TryGetValue(key, out var cached))
				return cached;

			var arr = new float[s, s];
			if (bi >= 0 && bi < map.Length && bj >= 0 && bj < map[0].Length && map[bi][bj].BiomeInfo != null)
			{
				foreach (var g in map[bi][bj].BiomeInfo.Where(bi2 => bi2.Type == BiomeDataType.GROUND))
				{
					var ix = Mathf.Clamp((int)g.LocalCoord.X, 0, s - 1);
					var iz = Mathf.Clamp((int)g.LocalCoord.Z, 0, s - 1);
					arr[ix, iz] = g.LocalCoord.Y;
				}
			}

			hcellCache[key] = arr;
			return arr;
		}

		float CellHeightGlobal(int globalCellX, int globalCellZ)
		{
			// Map cell indices -> block indices + local cell indices
			if (globalCellX < 0 || globalCellZ < 0)
				return 0f;

			int bi = globalCellX / s;
			int bj = globalCellZ / s;
			if (bi < 0 || bi >= map.Length || bj < 0 || bj >= map[0].Length)
				return 0f;

			int lx = globalCellX - (bi * s);
			int lz = globalCellZ - (bj * s);
			var hc = GetHCellForBlock(bi, bj);
			return hc[lx, lz];
		}

		float CornerHeight(int cx, int cz)
		{
			// Corner (cx,cz) is shared by up to 4 adjacent cells.
			// Sample those cells in global space so seams line up between blocks.
			int baseGX = i * s + cx;
			int baseGZ = j * s + cz;

			float sum = 0f;
			int count = 0;
			for (int dx = -1; dx <= 0; dx++)
			{
				for (int dz = -1; dz <= 0; dz++)
				{
					int cellGX = baseGX + dx;
					int cellGZ = baseGZ + dz;
					// Only include valid cells (within overall map)
					if (cellGX >= 0 && cellGZ >= 0)
					{
						int bi = cellGX / s;
						int bj = cellGZ / s;
						if (bi >= 0 && bi < map.Length && bj >= 0 && bj < map[0].Length)
						{
							sum += CellHeightGlobal(cellGX, cellGZ);
							count++;
						}
					}
				}
			}

			// Fallback shouldn't happen if your height is never 0 and data is complete,
			// but keep it safe.
			return count > 0 ? (sum / count) : 0f;
		}

		// Material
		var grass = new StandardMaterial3D { AlbedoColor = Colors.DarkGreen };

		// Build mesh
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		st.SetMaterial(grass);

		// Cache vertices for indexing
		var verts = new Vector3[s + 1, s + 1];
		var uvs = new Vector2[s + 1, s + 1];

		var originX = map[i][j].Coordinates.X * MapConstants.BLOCK_SIZE;
		var originZ = map[i][j].Coordinates.Y * MapConstants.BLOCK_SIZE;

		for (int x = 0; x <= s; x++)
		{
			for (int z = 0; z <= s; z++)
			{
				var h = CornerHeight(x, z);
				verts[x, z] = new Vector3(originX + x * subBlockSize, h, originZ + z * subBlockSize);
				uvs[x, z] = new Vector2((float)x / s, (float)z / s);
			}
		}

		// Triangulate grid: (s x s) quads -> 2 triangles each
		for (int x = 0; x < s; x++)
		{
			for (int z = 0; z < s; z++)
			{
				// Quad corners
				var v00 = verts[x, z];
				var v10 = verts[x + 1, z];
				var v01 = verts[x, z + 1];
				var v11 = verts[x + 1, z + 1];

				var uv00 = uvs[x, z];
				var uv10 = uvs[x + 1, z];
				var uv01 = uvs[x, z + 1];
				var uv11 = uvs[x + 1, z + 1];

				// Triangle 1: v00, v10, v01
				st.SetUV(uv00);
				st.AddVertex(v00);
				st.SetUV(uv10);
				st.AddVertex(v10);
				st.SetUV(uv01);
				st.AddVertex(v01);

				// Triangle 2: v10, v11, v01
				st.SetUV(uv10);
				st.AddVertex(v10);
				st.SetUV(uv11);
				st.AddVertex(v11);
				st.SetUV(uv01);
				st.AddVertex(v01);
			}
		}

		st.GenerateNormals();
		var mesh = st.Commit();

		var terrain = new MeshInstance3D();
		terrain.Mesh = mesh;

		result.Add(terrain);
		return result;
	}
	
	

	/*	private IList<Node3D> PrepareDetailedPlainBlocks(MapBlock[][] map, int i, int j)
	   {
	   const float subBlockSize = (float)MapConstants.BLOCK_SIZE / (float)MapConstants.BIOMEHEATMAPSCALE;
	   IList<Node3D> result = new List<Node3D>();
	   
	   var grass = new StandardMaterial3D() { AlbedoColor = Colors.DarkGreen };
	   
	   //var r = (Node3D)_grassP.Instantiate();
	   if (map[i][j].BiomeInfo != null)
	   {
	   var grounds = map[i][j].BiomeInfo.Where(bi => bi.Type == BiomeDataType.GROUND);
	   foreach (var g in grounds)
	   {
	   var box = new MeshInstance3D();
	   box.Mesh = new BoxMesh();
	   ((BoxMesh)box.Mesh).Size = new Vector3(subBlockSize, g.LocalCoord.Y, subBlockSize);
	   box.Position = new(map[i][j].Coordinates.X * MapConstants.BLOCK_SIZE + g.LocalCoord.X * subBlockSize + subBlockSize/2, g.LocalCoord.Y/2, map[i][j].Coordinates.Y * MapConstants.BLOCK_SIZE + g.LocalCoord.Z * subBlockSize + subBlockSize/2);
	   ((BoxMesh)box.Mesh).SetMaterial(grass);
	   result.Add(box);
	   }
	   }
	   
	   return result;
	   }
	 */
	
	private Node3D PrepareRiverBlock(MapBlock[][] map, int i, int j)
	{
		if (((i - 1) >= 0 && map[i - 1][j].BlockType == MapBlockType.RIVER) &&
		    ((i + 1) < map.Length && map[i + 1][j].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)_riverS.Instantiate();
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
		}

		if (((j - 1) >= 0 && map[i][j - 1].BlockType == MapBlockType.RIVER) && ((j + 1) < map.Length && map[i][j + 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)_riverS.Instantiate();
			r.RotateY((float)Math.PI / 2);
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
		}

		if (((i - 1) >= 0 && map[i - 1][j].BlockType == MapBlockType.RIVER) &&
		    ((j - 1) >= 0 && map[i][j - 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)_riverT.Instantiate();
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
		}

		if (((i - 1) >= 0 && map[i - 1][j].BlockType == MapBlockType.RIVER) && ((j + 1) < map.Length && map[i][j + 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)_riverT.Instantiate();
            r.RotateY((float)Math.PI / 2);
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
		}

		if (((i + 1) < map.Length && map[i + 1][j].BlockType == MapBlockType.RIVER) && ((j + 1) < map.Length && map[i][j + 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)_riverT.Instantiate();
            r.RotateY((float)Math.PI);
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
		}

		if (((i + 1) < map.Length && map[i + 1][j].BlockType == MapBlockType.RIVER) && ((j - 1) >= 0 && map[i][j - 1].BlockType == MapBlockType.RIVER))
		{
			var r = (Node3D)_riverT.Instantiate();
            r.RotateY((float)(3 * Math.PI / 2));

            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
		}
        if (((i - 1) >= 0 && map[i - 1][j].BlockType == MapBlockType.RIVER) ||
            ((i + 1) < map.Length && map[i + 1][j].BlockType == MapBlockType.RIVER))
        {
            var r = (Node3D)_riverS.Instantiate();
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
        }
        if (((j - 1) >= 0 && map[i][j - 1].BlockType == MapBlockType.RIVER) || ((j + 1) < map.Length && map[i][j + 1].BlockType == MapBlockType.RIVER))
        {
            var r = (Node3D)_riverS.Instantiate();
            r.RotateY((float)Math.PI / 2);
            r.Position = (new Vector3(i * MapConstants.BLOCK_SIZE, 0, j * MapConstants.BLOCK_SIZE));
            return r;
        }

        return null;
	}

	public void SpawnPlayer()
	{
		var player = (Player)_playerScene.Instantiate();
		GetNode<Node>("Players").AddChild(player);
		var b = FindFirstBase();
		if (b != Vector2I.Zero)
		{
			var basePos = _map[b.X][b.Y].GlobalPosition;
			player.GlobalPosition = new Vector3(basePos.X, 0, basePos.Z);
		}
	}
}
