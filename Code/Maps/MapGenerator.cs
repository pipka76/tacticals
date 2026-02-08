using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Godot;
using Godot.Collections;
using GodotPlugins.Game;
using tacticals.Code.Maps;
using tacticals.Code.Maps.Generators;
using tacticals.Code.Maps.Spawners;
using static MapBlock;

public class MapGenerator
{
    private readonly int _mapWidth;
    private readonly int _mapHeight;
    private int _structureID;
    private const int HEATRADIUS = 10;
    Random _r = new Random();
    
    public MapGenerator(int mapWidth, int mapHeight)
    {
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
    }

    public string ToJson(MapBlock[][] map)
    {
        return System.Text.Json.JsonSerializer.Serialize(map, new JsonSerializerOptions() { IncludeFields = true});
    }

    public MapBlock[][] GenerateMap(FlowFieldManager mgr)
    {
        MapBlock[][] mm = new MapBlock[_mapWidth][];
        for (int i = 0; i < _mapWidth; i++)
            mm[i] = new MapBlock[_mapHeight];

        var biomes = ForestHeatmapGenerator.GenerateBiomes(new Vector2I(_mapWidth * MapConstants.BIOMEHEATMAPSCALE, _mapHeight * MapConstants.BIOMEHEATMAPSCALE));
        //biomes.SavePng("biometest.png");
        mgr.InitializeMap(_mapWidth, _mapHeight, 1f / MapConstants.BIOMEHEATMAPSCALE, new Vector2(0, 0));

        InitUsingHeatmap(mm, biomes);
        //var river = GenerateRiver();
        //river.Draw(mm);
        GenerateBases(mm, mgr);

        // structures
        GenerateStructures(mm, mgr);
        
        //var forestMap = new Image();
        //forestMap.Load("res://Assets/UI/TreeMap.png");

        GenerateForest(mm, biomes);

        return mm;
    }

    private float[,] BuildHeightMapFromMeshes(Array<Node> surfaces)
    {
        // Height per map cell (i,j) in world units.
        // This is a fast, coarse pass: bins transformed mesh vertices into map cells and keeps the max Y.
        // If you need more accuracy (e.g., large triangles), follow up with a vertical raycast pass.
        var heights = new float[_mapWidth, _mapHeight];
        for (int i = 0; i < _mapWidth; i++)
            for (int j = 0; j < _mapHeight; j++)
                heights[i, j] = float.NegativeInfinity;

        foreach (var n in surfaces)
        {
            if (n is not MeshInstance3D mi)
                continue;
            
//            if (mi.Layers != 2)
//                continue;
            
            var mesh = mi.Mesh;
            if (mesh == null)
                continue;

            int surfaceCount = mesh.GetSurfaceCount();
            for (int s = 0; s < surfaceCount; s++)
            {
                var arrays = mesh.SurfaceGetArrays(s);
                if (arrays.Count == 0)
                    continue;

                // Vertex array
                var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                if (verts.Length == 0)
                    continue;

                // Transform vertices into world space
                var xform = mi.GlobalTransform;
                GD.Print($"inside={mi.IsInsideTree()} local={mi.Transform.Origin} global={mi.GlobalTransform.Origin}");
                for (int k = 0; k < verts.Length; k++)
                {
                    Vector3 w = xform * verts[k];

                    // Convert world XZ to map grid indices.
                    // Assumes map origin at (0,0) in world XZ and each block is MapConstants.BLOCK_SIZE wide.
                    int i = Mathf.FloorToInt(w.X / MapConstants.BLOCK_SIZE);
                    int j = Mathf.FloorToInt(w.Z / MapConstants.BLOCK_SIZE);

                    if (i < 0 || j < 0 || i >= _mapWidth || j >= _mapHeight)
                        continue;

                    if (w.Y > heights[i, j])
                        heights[i, j] = w.Y;
                }
            }
        }

        // Replace untouched cells with 0 height.
        for (int i = 0; i < _mapWidth; i++)
        {
            for (int j = 0; j < _mapHeight; j++)
            {
                if (float.IsNegativeInfinity(heights[i, j]))
                    heights[i, j] = 0f;
            }
        }

        return heights;
    }

    public MapBlock[][] MapExistingSurface(FlowFieldManager mgr, Array<Node> surfaces)
    {
        MapBlock[][] mm = new MapBlock[_mapWidth][];
        for (int i = 0; i < _mapWidth; i++)
            mm[i] = new MapBlock[_mapHeight];

        // Build a coarse height map from mesh vertices.
        // NOTE: This assumes your surface meshes are positioned in the same world XZ space as your map grid.
        var heights = BuildHeightMapFromMeshes(surfaces);

        // Flow field map init (same idea as GenerateMap)
        mgr.InitializeMap(_mapWidth, _mapHeight, 1f, new Vector2(0, 0));

        // Fill map blocks.
        for (int i = 0; i < _mapWidth; i++)
        {
            for (int j = 0; j < _mapHeight; j++)
            {
                mm[i][j] = new MapBlock()
                {
                    BlockType = MapBlockType.PLAIN,
                    StructureType = MapBlockStructureType.NONE,
                    LayerIndex = 0,
                    Coordinates = new Vector2I(i, j),
                    BiomeInfo = new List<BiomeData>()
                };

                // Store ground height as a single BiomeData point centered in the block.
                // If you rely on BIOMEHEATMAPSCALE micro-vertices per block, expand this similarly.
                mm[i][j].BiomeInfo.Add(new BiomeData()
                {
                    Type = BiomeDataType.GROUND,
                    // LocalCoord is interpreted elsewhere as local block coords.
                    // Put height in Y; X/Z at block center.
                    LocalCoord = new Vector3(MapConstants.BLOCK_SIZE * 0.5f, heights[i, j], MapConstants.BLOCK_SIZE * 0.5f)
                });
            }
        }

        return mm;
    }

    private void GenerateForest(MapBlock[][] mm, Image forestMap)
    {
        const float threshold = 10f;
        int width = forestMap.GetWidth();
        int height = forestMap.GetHeight();
        Color treeMin = new Color("002030ff");
        Color treeMax = new Color("f8ff00ff");
        Color treeMid = new Color("60ff00ff");

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color color = forestMap.GetPixel(x, y);

                float mapX = (float)x / MapConstants.BIOMEHEATMAPSCALE;
                float mapY = (float)y / MapConstants.BIOMEHEATMAPSCALE;

                if (mm[(int)mapX][(int)mapY].BlockType != MapBlockType.PLAIN)
                    continue;
                if (mm[(int)mapX][(int)mapY].StructureType != MapBlockStructureType.NONE)
                    continue;
                
                if (mm[(int)mapX][(int)mapY].BiomeInfo == null)
                    mm[(int)mapX][(int)mapY].BiomeInfo = new List<MapBlock.BiomeData>();

                if (ColorInRange(color, treeMid, 0.4f))
                {
                    float treeShiftX = (float)Random.Shared.Next(-250, 250) / (1000 * MapConstants.BIOMEHEATMAPSCALE);
                    float treeShiftY = (float)Random.Shared.Next(-250, 250) / (1000 * MapConstants.BIOMEHEATMAPSCALE);
                    var bd = new MapBlock.BiomeData();
                    bd.LocalCoord = new Vector3(MapConstants.BLOCK_SIZE*(mapX % 1) - MapConstants.BLOCK_SIZE/2 + treeShiftX, 0, MapConstants.BLOCK_SIZE*(mapY % 1) - MapConstants.BLOCK_SIZE/2 + treeShiftY);

                    if (ColorInRange(color, treeMin, 0f))
                    {
                        string treeType = "broadleaved";
                        bd.Type = Enum.Parse<BiomeDataType>(ChooseTree(treeType));
                    }
                    else
                    {
                        string treeType = "conifer";
                        bd.Type = Enum.Parse<BiomeDataType>(ChooseTree(treeType));
                    }

                    mm[(int)mapX][(int)mapY].BiomeInfo.Add(bd);
                }
            }
        }
    }
    private string ChooseTree(string treeType)
    {
        int r = _r.Next(0, 120);
        switch (treeType)
        {
            case "conifer":
                if (r <= 20)
                    return "TREEC1";
                else if (r > 20 && r <= 40)
                    return "TREEC2";
                else if (r > 40 && r <= 60)
                    return "TREEC3";
                else if (r > 60 && r <= 80)
                    return "TREEC4";
                else if (r > 80 && r <= 100)
                    return "TREEC5";
                else
                    return "TREEC6";
            case "broadleaved":
                if (r <= 60)
                    return "TREEB1";
                else
                    return "TREEB2";
            default: return "TREEC1";
        }
    }
    private bool ColorInRange(Color color, Color Mid, float colorRange)
    {
        return (Math.Abs(color.R - Mid.R) + Math.Abs(color.G - Mid.G) + Math.Abs(color.B - Mid.B)) < colorRange;
    }

    private void GenerateStructures(MapBlock[][] map, FlowFieldManager mgr)
    {
        var rnd = new Random(); 
        var spw = new Spawner();

        spw.RegisterLimit(MapBlockStructureType.TANK, 5);
        spw.RegisterLimit(MapBlockStructureType.TOWER, 10);
        spw.RegisterLimit(MapBlockStructureType.BUNKER, 10);

        while (true)
        {
            int i = rnd.Next(0, map.Length);
            int j = rnd.Next(0, map[0].Length);

            if(map[i][j].StructurePlacable(MapBlockStructureType.TANK))
            {
                if(spw.SpawnAt(map, i, j, MapBlockStructureType.TANK, 1f))
                    AddHeat(map, i, j, HEATRADIUS);
            }


            if (map[i][j].StructurePlacable(MapBlockStructureType.TOWER))
            {
                if (spw.SpawnAt(map, i, j, MapBlockStructureType.TOWER, 1f))
                {
                    AddHeat(map, i, j, HEATRADIUS);
                    mgr.SetBlocked(new Vector2I(i * MapConstants.BLOCK_SIZE, j), true);
                }
            }

            if (map[i][j].StructurePlacable(MapBlockStructureType.BUNKER))
            {
                if (spw.SpawnAt(map, i, j, MapBlockStructureType.BUNKER, 1f))
                    AddHeat(map, i, j, HEATRADIUS);
            }

            if (spw.IsLimitReached())
                break;
        }
    }

    private void SetWholeBlockBlocked(FlowFieldManager mgr, int i, int j)
    {
        for (int k = 0; k < MapConstants.BIOMEHEATMAPSCALE; k++)
        {
            for (int l = 0; l < MapConstants.BIOMEHEATMAPSCALE; l++)
            {
                mgr.SetBlocked(new Vector2I(i * (MapConstants.BLOCK_SIZE * MapConstants.BIOMEHEATMAPSCALE), j * MapConstants.BLOCK_SIZE), true);
            }
        }
    }

    private void AddHeat(MapBlock[][] map,int i, int j, int radius)
    {
        int iMax, jMax, iMin, jMin;
        iMax = i + radius;
        if (iMax >= map.Length) iMax = map.Length - 1;
        jMax = j + radius;
        if (jMax >= map[0].Length) jMax = map[0].Length - 1;
        iMin = i - radius;
        if (iMin < 0) iMin = 0;
        jMin = j - radius;
        if (jMin < 0) jMin = 0;

        for ( int x = iMin; x <= iMax; x++)
        {
            for ( int y = jMin; y <= jMax; y++) 
            {
                int a = x - i, b = y - j;

                if (a * a + b * b <= radius*radius)
                {
                    map[x][y].StructureHeat++;
                }
            }
        }
    }

    private River GenerateRiver()
    {
        River river = new River();
        Random rand = new Random();

        if (FlipCoin())
        {
            // river is starting on left side
            river.Start = new Vector2I(0, (int)(((float)rand.Next(300, 700) / 1000f) * _mapHeight));
            river.End = new Vector2I(_mapWidth - 1, (int)(((float)rand.Next(300, 700) / 1000f) * _mapHeight));
        }
        else
        {
            // river is starting on top side
            river.Start = new Vector2I((int)(((float)rand.Next(300, 700) / 1000f) * _mapWidth), 0);
            river.End = new Vector2I((int)(((float)rand.Next(300, 700) / 1000f) * _mapWidth), _mapHeight -1);
        }

        return river;
    }

    private void GenerateBases(MapBlock[][] mm, FlowFieldManager mgr)
    {
        mm[20][20].StructureType = MapBlockStructureType.BASE;
        mm[20][20].StructureID = _structureID++;
        mm[mm.Length - 20][20].StructureType = MapBlockStructureType.BASE;
        mm[mm.Length - 20][20].StructureID = _structureID++;
        mm[20][mm[0].Length - 20].StructureType = MapBlockStructureType.BASE;
        mm[20][mm[0].Length - 20].StructureID = _structureID++;
        mm[mm.Length - 20][mm[0].Length - 20].StructureType = MapBlockStructureType.BASE;
        mm[mm.Length - 20][mm[0].Length - 20].StructureID = _structureID++;
    }

    private float GetRandomProb()
    {
        Random r = new Random();

        return (float)r.NextDouble();
    }

    private bool FlipCoin()
    {
        var n = Random.Shared.Next(0, 100);
        return n < 50;
    }

    private void InitUsingHeatmap(MapBlock[][] mm, Image heatmap, float amplification = 300f)
    {
        //int width = heatmap.GetWidth();
        //int height = heatmap.GetHeight();
        
        _structureID = 0;
        for (int i = 0; i < _mapWidth; i++)
        {
            for (int j = 0; j < _mapHeight; j++)
            {
                var bd = new List<BiomeData>();
                for (int k = 0; k < MapConstants.BIOMEHEATMAPSCALE; k++)
                {
                    for (int l = 0; l < MapConstants.BIOMEHEATMAPSCALE; l++)
                    {
                        Color px = heatmap.GetPixel( i * MapConstants.BIOMEHEATMAPSCALE + k, j * MapConstants.BIOMEHEATMAPSCALE + l);
                        bd.Add(new BiomeData()
                        {
                            Type = BiomeDataType.GROUND,
                            LocalCoord = new Vector3(k, (px.A == 0 ? 0.001f : px.A) * amplification, l)
                        });
                    }
                }

                mm[i][j] = new MapBlock()
                {
                    BlockType = MapBlockType.PLAIN,
                    StructureType = MapBlockStructureType.NONE,
                    LayerIndex = 0,
                    Coordinates = new Vector2I(i, j),
                    BiomeInfo = bd  
                };
            }
        }
    }
}