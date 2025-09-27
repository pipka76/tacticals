using System;
using System.Text.Json;
using Godot;
using tacticals.Code.Maps.Spawners;

public class MapGenerator
{
    private readonly int _mapWidth;
    private readonly int _mapHeight;
    private int _structureID;
    private const int HEATRADIUS = 10;
    
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

        return mm;
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