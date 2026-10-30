namespace tacticals.Code.Game;

/// <summary>
/// What a crate unpacks into once it is built. The crate carries its payload with it, so no call
/// site has to assume what is inside.
/// </summary>
public enum CrateContent
{
    SMALL_TOWER,
    BROWNING_TURRET
}
