namespace tacticals.Code.Game;

/// <summary>
/// Which command the player is currently aiming. Derived from the held modifier key alone - the
/// left mouse button is the confirm, not the mode - so the ground marker can show where a command
/// will land before it is issued.
/// </summary>
public enum CommandMode
{
    None,
    Select,
    Move,
    Patrol,
    Board,
    Exit
}
