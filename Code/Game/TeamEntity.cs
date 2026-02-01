using System.Collections.Generic;
using System.Linq;
using Godot;
using tacticals.Code.Game;

public partial class TeamEntity : CharacterBody3D
{
    private TeamObjectType objectType;
    protected Node3D _selectorObject;
    protected TeamEntityStates _enityState;
    protected MultiplayerSynchronizer _synchronizer;
    protected Godot.Collections.Dictionary<TeamObjectType, int> _passengers;
    protected int _maxPassengersCapacity;
    
    protected TeamEntity()
    {
        _passengers = new Godot.Collections.Dictionary<TeamObjectType, int>();
        _maxPassengersCapacity = 0;
    }
    
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

    public void BoardPassenger(TeamEntity entity)
    {
        if (_maxPassengersCapacity >= _passengers.Sum(p => p.Value))
            return;
        
        var actualCount = _passengers[entity.objectType];
        _passengers[entity.objectType] = actualCount + 1;
    }

    public IReadOnlyDictionary<TeamObjectType, int> ExitPassengers(int count = 0)
    {
        var result = new Godot.Collections.Dictionary<TeamObjectType, int>();

        if (_passengers.Sum(p => p.Value) == 0)
            return result;

        if (count == 0) // exit all
            return _passengers;

        int c = 0;
        foreach (var p in _passengers)
        {
            result.Add(p.Key, p.Value);
            c++;
            _passengers[p.Key] -= 1;
            if (c == count)
                break;
        }

        return result;
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
    
    public bool IsInState(TeamEntityStates state)
    {
        return (_enityState == state);
    }

    public void SetNewState(TeamEntityStates newState)
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
    
    protected Quaternion RotateMatchTerrain(Vector3 direction, Vector3 n, float weight)
    {
        var forwardOnPlane = direction - n * direction.Dot(n);
        forwardOnPlane = forwardOnPlane.Normalized();

        // Build an orthonormal basis: X = right, Y = up(normal), Z = -forward
        var right = forwardOnPlane.Cross(n).Normalized();
        var desiredBasis = new Basis(right, n, -forwardOnPlane).Orthonormalized();

        // Smooth rotation (optional). For instant snap, just assign the basis.
        var current = GlobalTransform;
        var target = new Transform3D(desiredBasis, current.Origin);

        // Slerp via quaternions for smoothness:
        var qCurrent = current.Basis.GetRotationQuaternion();
        var qTarget = desiredBasis.GetRotationQuaternion();
        //var t = (float)(1.0 - Math.Exp(-12f * GetProcessDeltaTime()));

        if (weight == 1f)
            return qTarget;
        
        return qCurrent.Slerp(qTarget, weight);
    }
    
    protected bool RaycastToTerrain(out Vector3 hitPos, out Vector3 hitNormal)
    {
        const float VIEWDISTANCE = 200f;

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