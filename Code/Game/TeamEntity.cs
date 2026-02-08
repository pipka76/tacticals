using System;
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
    protected List<TeamEntity> _passengers;
    protected int _maxPassengersCapacity;
    private Queue<Tuple<TeamEntityStates, object>> _entityStateQueue;
    protected TeamMembership _teamMembership;
    protected int _damageTaken;
    
    protected TeamEntity()
    {
        _passengers = new List<TeamEntity>();
        _entityStateQueue = new Queue<Tuple<TeamEntityStates, object>>(5);
        _maxPassengersCapacity = 0;
        _damageTaken = 0;
        _teamMembership = TeamMembership.NONE;
    }

    public void SetMembership(TeamMembership team)
    {
        _teamMembership = team;
        if (team != TeamMembership.OWN && team != TeamMembership.NONE)
            AddToGroup(EntityGroup.ENEMY);
    }
    
    public void EnqueueState(TeamEntityStates state, object arg = null, bool cleanFirst = false)
    {
        if (cleanFirst)
            _entityStateQueue.Clear();
        _entityStateQueue.Enqueue(new Tuple<TeamEntityStates, object>(state, arg));
    }

    protected Tuple<TeamEntityStates, object> GetNextState()
    {
        if (_entityStateQueue.Count == 0)
            return new Tuple<TeamEntityStates, object>(TeamEntityStates.IDLE, null);
            
        return _entityStateQueue.Dequeue();
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

    public bool BoardPassenger(TeamEntity entity)
    {
        if (_maxPassengersCapacity <= _passengers.Count)
            return false;

        if (_teamMembership != TeamMembership.OWN && _teamMembership != TeamMembership.NONE)
            return false;
        if (_teamMembership == TeamMembership.NONE)
            _teamMembership = TeamMembership.OWN;
        
        _passengers.Add(entity);
        return true;
    }

    public IReadOnlyList<TeamEntity> ExitPassengers()
    {
        var result = new List<TeamEntity>();
        if (_passengers.Count == 0)
            return result;

        if (_teamMembership != TeamMembership.OWN && _teamMembership != TeamMembership.NONE)
            return result;

        result.AddRange(_passengers.ToArray());

        _passengers.Clear();
        _teamMembership = TeamMembership.NONE;

        return result;
    }

    public void UpdatePassengersPosition(Vector3 pos)
    {
        foreach (var p in _passengers)
        {
            p.GlobalPosition = pos;
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
    
    protected Quaternion RotateMatchPlane(Vector3 direction, Vector3 n, float weight)
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

        var from = GlobalPosition + Vector3.Up * VIEWDISTANCE/2;
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

    public void TakeHit(int pDamage, TeamEntity pShooter, Vector3 hitPos)
    {
        _damageTaken += pDamage;
    }

    public bool IsMemberOf(TeamMembership memberOf)
    {
        return _teamMembership == memberOf;
    }
}