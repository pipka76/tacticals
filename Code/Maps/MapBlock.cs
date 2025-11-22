using System.Collections.Generic;
using Godot;

public class MapBlock
{
    public enum BiomeDataType
    {
        TREEC1,
        TREEC2,
        TREEC3,
        TREEC4,
        TREEB1,
        TREEB2,
        TREEB3,
        TREEB4,
    }

    public class BiomeData
    {
        public BiomeDataType Type { get; set; }
        public Vector3 LocalCoord { get; set; }
    }
    
    public MapBlockType BlockType;
    public MapBlockStructureType StructureType;
    public Vector2I Coordinates;
    public int LayerIndex;
    public int StructureID;
    public int StructureHeat;
    public IList<BiomeData> BiomeInfo;

    public bool StructurePlacable(MapBlockStructureType structureType)
    {
        // todo switch per structureType

        return StructureHeat < 1;
    }
}