namespace tacticals.Code.Game;

public class EntityGroup
{
    public const string GROUND_UNIT = "ground-units";
    public const string AIR_UNIT = "air-units";
    public const string SOLDIERS = "soldiers";
    public const string MACHINERY = "machinery";
    public const string ENEMY = "enemies";
    public const string BUILD_SITES = "build-sites";
    /// <summary>Structures soldiers can get into. They live outside the map's entity list, so selection reaches them through this.</summary>
    public const string GARRISONS = "garrisons";
}