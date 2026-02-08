using System.Collections.Generic;
using System.Linq;
using Godot;
using tacticals.Code.Game;

public partial class MovableTeamEntity : TeamEntity
{
    private const float NEIGHBOR_RADIUS = 2f;      // meters-ish; tune
    protected const float SEPARATION_WEIGHT = 2.0f;     // tune
    protected const float ARRIVE_SLOW_RADIUS = 2.0f;    // start slowing down near slot
    protected const float MIN_SPEED_FACTOR = 0.25f;     // never fully stop while moving
    protected Vector2 _moveToCoords;

    public Vector3 MoveToCoordinates
    {
        get
        {
            return new Vector3(_moveToCoords.X, 0, _moveToCoords.Y);
        }
    }

    protected Vector3 ComputeSeparation(Vector3 myPosFlat, string group)
    {
        // With <= 20 soldiers, a simple O(n) scan is fine.
        Vector3 sep = Vector3.Zero;
        foreach (var node in GetTree().GetNodesInGroup(group))
        {
            if (node is not MovableTeamEntity other || other == this)
                continue;
            // Optional: only separate from units on the move
            if (!other.IsInState(TeamEntityStates.ONTHEWAY))
                continue;

            var oPos = other.GlobalPosition;
            var oFlat = new Vector3(oPos.X, 0, oPos.Z);
            var delta = myPosFlat - oFlat;
            float d2 = delta.LengthSquared();
            if (d2 < 0.000001f)
                continue;
            if (d2 > NEIGHBOR_RADIUS * NEIGHBOR_RADIUS)
                continue;

            // Inverse-square push: stronger when very close, weaker farther away
            sep += delta / d2;
        }

        sep.Y = 0;
        return sep;
    }

    public virtual void PortTo(Vector2 coords)
    {
        if (_teamMembership != TeamMembership.OWN && _teamMembership != TeamMembership.NONE)
            return;
            
        if (RaycastToTerrain(out var gnd, out _))
            GlobalPosition = new Vector3(coords.X, gnd.Y, coords.Y);
    }

    public virtual void MoveTo(Vector2 coords)
    {
        if (_teamMembership != TeamMembership.OWN && _teamMembership != TeamMembership.NONE)
            return;

        SetNewState(TeamEntityStates.ONTHEWAY);
        _moveToCoords = coords;
        RotateTowards(new Vector3(coords.X, 0, coords.Y));
    }
}