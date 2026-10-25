using Godot;

namespace tacticals.Code.Game;

/// <summary>
/// The ground marker that replaces the mouse cursor shape as command-mode feedback. It sits on
/// the exact point the pending command will use, so the player can see where an order lands
/// before committing it, and is coloured per mode so which mode is active is readable at a glance.
///
/// The marker is three parts, all of which exist because the god camera often looks along the
/// ground rather than down at it: a filled disc reads the spot from above, an outline sharpens
/// its edge, and a vertical post stays visible when the flat parts foreshorten away at a grazing
/// angle. The whole thing scales with camera distance so a far-off waypoint does not shrink to
/// nothing.
/// </summary>
public partial class CommandCursor : Node3D
{
    private const float BASE_RADIUS = 1.5f;
    private const float REF_DISTANCE = 60f;     // distance at which the marker is drawn at base size
    private const float MAX_SCALE = 3f;
    private const float POST_HEIGHT_FACTOR = 2.5f;
    private const float FILL_ALPHA = 0.22f;
    private const float OUTLINE_ALPHA = 0.9f;

    private static readonly Color MOVE_COLOR = new Color(0.30f, 0.90f, 0.40f);
    private static readonly Color PATROL_COLOR = new Color(0.70f, 0.45f, 1.00f);
    private static readonly Color BOARD_COLOR = new Color(0.35f, 0.70f, 1.00f);
    private static readonly Color EXIT_COLOR = new Color(1.00f, 0.70f, 0.25f);
    private static readonly Color BLOCKED_COLOR = new Color(1.00f, 0.25f, 0.20f);
    private static readonly Color SLOW_COLOR = new Color(1.00f, 0.80f, 0.30f);

    private GroundOverlay _overlay;

    public override void _Ready()
    {
        _overlay = new GroundOverlay { Name = "GroundOverlay" };
        AddChild(_overlay);
    }

    /// <summary>
    /// Redraws the marker for this frame. Pass the same terrain hit the command handlers use, so
    /// what the player aims at and what the click resolves to cannot drift apart.
    /// </summary>
    public void Render(CommandMode mode, bool hasHover, Vector3 hitPos, Vector3 hitNormal, float cameraDistance)
    {
        if (_overlay == null)
            return;

        _overlay.Begin();

        if (hasHover && TryGetModeColor(mode, out var color))
        {
            float radius = BASE_RADIUS * Mathf.Clamp(cameraDistance / REF_DISTANCE, 1f, MAX_SCALE);
            color = TintByTerrain(color, new Vector2(hitPos.X, hitPos.Z));

            _overlay.DiscAt(hitPos, hitNormal, radius, new Color(color, FILL_ALPHA));
            _overlay.CircleOutlineAt(hitPos, hitNormal, radius, new Color(color, OUTLINE_ALPHA));
            _overlay.PostAt(hitPos, hitNormal, radius * POST_HEIGHT_FACTOR, new Color(color, OUTLINE_ALPHA));

            switch (mode)
            {
                // A second, wider ring: patrol is the one mode that keeps accepting clicks, so it
                // wants to look different from move at a glance.
                case CommandMode.Patrol:
                    _overlay.CircleOutlineAt(hitPos, hitNormal, radius * 1.6f, new Color(color, OUTLINE_ALPHA * 0.6f));
                    break;

                // Exit drops passengers on a spot rather than travelling to it - mark the spot.
                case CommandMode.Exit:
                    _overlay.CrossAt(hitPos, hitNormal, radius * 0.7f, new Color(color, OUTLINE_ALPHA));
                    break;
            }
        }

        _overlay.End();
    }

    private static bool TryGetModeColor(CommandMode mode, out Color color)
    {
        switch (mode)
        {
            case CommandMode.Move: color = MOVE_COLOR; return true;
            case CommandMode.Patrol: color = PATROL_COLOR; return true;
            case CommandMode.Board: color = BOARD_COLOR; return true;
            case CommandMode.Exit: color = EXIT_COLOR; return true;
            // Plain selection is a click on a unit, not on the ground - a ground marker would only
            // be noise.
            default: color = default; return false;
        }
    }

    /// <summary>
    /// Warns about the destination itself: red where the grid is closed to ground units, amber
    /// where terrain will slow them down. Both are invisible to the player otherwise.
    /// </summary>
    private static Color TintByTerrain(Color color, Vector2 worldXZ)
    {
        var pathField = Main3d.Current?.Map?.PathField;
        if (pathField == null || !pathField.IsInitialized)
            return color;

        if (!pathField.IsPassable(pathField.WorldToCell(worldXZ), MovementDomain.Ground))
            return BLOCKED_COLOR;

        float moveFactor = pathField.GetMoveFactorWorld(worldXZ);
        if (moveFactor < 1f)
            return color.Lerp(SLOW_COLOR, Mathf.Clamp(1f - moveFactor, 0f, 1f));

        return color;
    }
}
