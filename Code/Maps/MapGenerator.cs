using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Godot;
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
    private const int BIOMEHEATMAPSCALE = 2; // 5:1
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

    public MapBlock[][] GenerateMinimap()
    {
        MapBlock[][] mm = new MapBlock[_mapWidth][];
        for (int i = 0; i < _mapWidth; i++)
            mm[i] = new MapBlock[_mapHeight];

        InitMinimap(mm);
        var river = GenerateRiver();
        river.Draw(mm);
        GenerateBases(mm);

        // structures
        GenerateStructures(mm);
        
        //var forestMap = new Image();
        //forestMap.Load("res://Assets/UI/TreeMap.png");

        var biomes = ForestHeatmapGenerator.GenerateBiomes(new Vector2I(_mapWidth*BIOMEHEATMAPSCALE, _mapHeight*BIOMEHEATMAPSCALE));
        biomes.SavePng("biometest.png");
        GenerateForest(mm, biomes);

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

                float mapX = (float)x / BIOMEHEATMAPSCALE;
                float mapY = (float)y / BIOMEHEATMAPSCALE;

                if (mm[(int)mapX][(int)mapY].BiomeInfo == null)
                    mm[(int)mapX][(int)mapY].BiomeInfo = new List<MapBlock.BiomeData>();

                if (ColorInRange(color, treeMid, 0.4f))
                {
                    var bd = new MapBlock.BiomeData();
                    bd.LocalCoord = new Vector3(MapConstants.BLOCK_SIZE*(mapX % 1) - MapConstants.BLOCK_SIZE/2, 0, MapConstants.BLOCK_SIZE*(mapY % 1) - MapConstants.BLOCK_SIZE/2);

                    if (ColorInRange(color, treeMin, 0.7f))
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
        return (Math.Abs(color.R - Mid.R) + Math.Abs(color.G - Mid.G) + Math.Abs(color.B - Mid.B)) <= colorRange;
    }

    private void GenerateStructures(MapBlock[][] map)
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
                if(spw.SpawnAt(map, i, j, MapBlockStructureType.TOWER, 1f))
                    AddHeat(map, i, j, HEATRADIUS);
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

    private void GenerateBases(MapBlock[][] mm)
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
        Random r = new Random();

        var n = r.Next(0, 100);
        return n < 50;
    }

    private void InitMinimap(MapBlock[][] mm)
    {
        _structureID = 0;
        for (int i = 0; i < _mapWidth; i++)
        {
            for (int j = 0; j < _mapHeight; j++)
            {
                mm[i][j] = new MapBlock()
                {
                    BlockType = MapBlockType.PLAIN,
                    StructureType = MapBlockStructureType.NONE,
                    LayerIndex = 0,
                    Coordinates = new Vector2I(i, j)
                };
            }
        }
    }
}