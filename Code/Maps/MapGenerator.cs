using System;
using System.Text.Json;
using Godot;

public class MapGenerator
{
    private readonly int _mapWidth;
    private readonly int _mapHeight;
    private int _structureID;
    
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
        
        return mm;
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