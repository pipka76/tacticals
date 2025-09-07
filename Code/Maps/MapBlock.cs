using Godot;

public class MapBlock
{
    public MapBlockType BlockType;
    public MapBlockStructureType StructureType;
    public Vector2I Coordinates;
    public int LayerIndex;
    public int StructureID;
    public int StructureHeat;

    public bool StructurePlacable(MapBlockStructureType structureType)
    {
        // todo switch per structureType

        return StructureHeat < 1;
    }
}