using Godot;
using tacticals.Code.Game;

public partial class TeamEntity : CharacterBody3D
{
    private TeamObjectType objectType;
    protected Node3D _selectorObject;
    protected TeamEntityStates _enityState;
    protected MultiplayerSynchronizer _synchronizer;

    public bool IsSelected {
        set
        {
            if(_selectorObject == null)
                return;
            
            _selectorObject.Visible = value;
        }
        get
        {
            if(_selectorObject == null)
                return false;
            
            return _selectorObject.Visible;
        }
    }

    protected void RotateTowards(Vector3 location)
    {
        // Get current position, ignore Y difference
        Vector3 direction = location - GlobalPosition;
        direction.Y = 0; // Ignore Y axis

        if (direction.LengthSquared() == 0)
            return; // Avoid errors when target is directly above or below

        direction = direction.Normalized();

        // Calculate rotation angle on Y-axis
        float targetAngle = Mathf.Atan2(-direction.X, -direction.Z);

        Rotation = new Vector3(0, targetAngle, 0);
    }
    
    protected bool IsInState(TeamEntityStates state)
    {
        return (_enityState == state);
    }

    protected void SetNewState(TeamEntityStates newState)
    {
        _enityState = newState;
    }

    public void Command(TeamEntityCommands cmd, bool valid, TeamEntityCommandParameters parameters = null)
    {
        switch (cmd)
        {
            case TeamEntityCommands.MOVETO:
                if (this is not MovableTeamEntity)
                    return;
                if (parameters != null && parameters.Coords != null)
                (this as MovableTeamEntity).MoveTo(parameters.Coords.Value);
                break;
        }
    }

    public virtual TeamObjectType GetTeamObjectType()
    {
        return objectType;
    }
    
    protected bool RaycastToTerrain(out Vector3 hitPos, out Vector3 hitNormal)
    {
        const float VIEWDISTANCE = 50f;

        var from = GlobalPosition + Vector3.Up * VIEWDISTANCE;
        var to   = GlobalPosition - Vector3.Up * VIEWDISTANCE;

        var space = GetWorld3D().DirectSpaceState;

        var query = new PhysicsRayQueryParameters3D
        {
            From = from,
            To = to,
            CollisionMask = 1,
            // Optional but recommended if you're raycasting from the entity itself
            Exclude = new Godot.Collections.Array<Rid> { GetRid() }
        };

        var result = space.IntersectRay(query);

        if (result.Count == 0)
        {
            hitPos = default;
            hitNormal = default;
            return false;
        }

        hitPos = (Vector3)result["position"];
        hitNormal = (Vector3)result["normal"];
        return true;
    }
}