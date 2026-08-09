namespace tacticals.Code.Game;

/// <summary>
/// What a crate unpacks into once it is built. SMALL_TOWER is the only content so far - the enum
/// exists so the crate carries its payload with it instead of every call site assuming a tower.
/// </summary>
public enum CrateContent
{
    SMALL_TOWER
}
