using System.Collections.Generic;
using System.Linq;
using Godot;
using tacticals.Code.Maps;

public class MapBlock
{
    public enum BiomeDataType
    {
        TREEC1,
        TREEC2,
        TREEC3,
        TREEC4,
        TREEC5,
        TREEC6,
        TREEB1,
        TREEB2,
        GROUND
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

    public Vector3 GlobalPosition
    {
        get
        {
            float height = 0f;
            if (BiomeInfo != null)
            {
                float sum = 0f;
                int cnt = 0;
                
                var grounds = BiomeInfo.Where(bi => bi.Type == BiomeDataType.GROUND);
                foreach (var g in grounds)
                {
                    cnt++;
                    sum += g.LocalCoord.Y;
                }

                if (cnt != 0)
                    height = sum / cnt;
            }

            return new(Coordinates.X * MapConstants.BLOCK_SIZE, height, Coordinates.Y * MapConstants.BLOCK_SIZE);
        }
    }
}