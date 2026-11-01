using System.Collections.Generic;
using Godot;

namespace tacticals.Code.Game;

/// <summary>
/// A field gun worked by a crew of <see cref="CREW_SIZE"/> soldiers. It boards like a vehicle, but
/// the crew stays in view: each man stands at his own CrewSpot marker and goes along with the gun.
/// Whoever gets on first claims it for his team; it goes back to neutral once everyone is off.
///
/// It only moves with the full crew on it - short-handed it cannot be pushed, so move orders are
/// ignored. Firing is not in yet.
///
/// Enemies go for the crew, never the gun: like the turret's, its colliders - the model's -col
/// meshes - stay on layer 1, which spotting does not look at, so only the soldiers working it get
/// it noticed. Bullets and sight lines still stop on it, so it is cover for them.
/// Layer 1 is also the terrain layer every unit raycasts to find the ground, so the gun lists its
/// colliders for its own ground ray to look through - otherwise it would stand on itself.
/// The box on the gun's own body is only there to make it easy to click: it sits on the
/// click-only Picking layer, which bullets and sight lines never look at.
/// </summary>
public partial class Artillery : MovableTeamEntity, IPassengers
{
    public const int CREW_SIZE = 3;

    private const float MOVE_SPEED = 3f;
    /// <summary>How far past the crew spots the crew steps off when getting out.</summary>
    private const float EXIT_STEP = 3f;

    private readonly List<Node3D> _crewSpots = new();

    public Artillery()
    {
        _maxPassengersCapacity = CREW_SIZE;
    }

    /// <summary>Reachable from anywhere around the trails, not only right at the axle.</summary>
    float IPassengers.BoardReach => 6f;

    /// <summary>The crew works the gun out in the open.</summary>
    bool IPassengers.HidesPassengers => false;

    public bool IsFullyCrewed => _passengers.Count >= CREW_SIZE;

    public override void _Ready()
    {
        _selectorObject = GetNode<Node3D>("SelectionRing");
        _synchronizer = GetNodeOrNull<MultiplayerSynchronizer>("ServerSynchronizer");

        for (int i = 1; i <= CREW_SIZE; i++)
        {
            var spot = GetNodeOrNull<Node3D>($"CrewSpot{i}");
            if (spot != null)
                _crewSpots.Add(spot);
        }

        foreach (var node in GetNode("Model").FindChildren("*", nameof(CollisionObject3D), true, false))
        {
            var body = (CollisionObject3D)node;
            body.CollisionMask = 0;
            _ownTerrainColliders.Add(body.GetRid());
        }

        AddToGroup(EntityGroup.MACHINERY);
        AddToGroup(EntityGroup.GROUND_UNIT);
    }

    public override void _Process(double delta)
    {
        DropFallen();

        if (IsInState(TeamEntityStates.ONTHEWAY))
        {
            HandleOnTheWay(delta);
            StandCrewAtSpots();
            return;
        }

        if (IsInState(TeamEntityStates.EXITING))
        {
            HandleExiting();
            return;
        }

        HandleIdle();
        StandCrewAtSpots();
    }

    /// <summary>Only soldiers, and only into a free spot. The team check is the base one: empty takes anyone.</summary>
    bool IPassengers.BoardPassenger(TeamEntity entity)
    {
        if (entity is not Soldier || entity.IsInState(TeamEntityStates.TERMINATED))
            return false;
        if (!BoardPassenger(entity))
            return false;

        StandCrewAtSpots();
        return true;
    }

    /// <summary>Short-handed the gun stays where it is.</summary>
    public override void MoveAlong(IReadOnlyList<Vector2> waypoints)
    {
        if (!IsFullyCrewed)
            return;

        base.MoveAlong(waypoints);
    }

    public override void PortTo(Vector2 coords)
    {
        base.PortTo(coords);
        StandCrewAtSpots();
    }

    /// <summary>
    /// Puts each crewman at his spot, facing where the barrel points. Every frame, so the crew
    /// keeps its places while the gun moves and tilts over the ground.
    /// </summary>
    private void StandCrewAtSpots()
    {
        // Same convention as the units: yaw 0 faces -Z, and so does the barrel.
        var facing = GlobalBasis.Orthonormalized() * Vector3.Forward;
        var yaw = Mathf.Atan2(-facing.X, -facing.Z);

        for (int i = 0; i < _passengers.Count; i++)
        {
            var p = _passengers[i];
            p.GlobalPosition = i < _crewSpots.Count ? _crewSpots[i].GlobalPosition : GlobalPosition;
            p.GlobalRotation = new Vector3(0f, yaw, 0f);
        }
    }

    /// <summary>A crewman killed at the gun frees his spot. With nobody left it is up for grabs again.</summary>
    private void DropFallen()
    {
        int dropped = _passengers.RemoveAll(p => !GodotObject.IsInstanceValid(p) || p.IsInState(TeamEntityStates.TERMINATED));
        if (dropped > 0 && _passengers.Count == 0)
            _teamMembership = TeamMembership.NEUTRAL;
    }

    /// <summary>The crew steps off away from the gun, towards where they were sent.</summary>
    private void HandleExiting()
    {
        var exitDir = new Vector3(_moveToCoords.X, GlobalPosition.Y, _moveToCoords.Y) - GlobalPosition;
        exitDir.Y = 0f;
        exitDir = exitDir.LengthSquared() < 0.0001f ? Vector3.Zero : exitDir.Normalized();

        foreach (var p in ExitPassengers())
        {
            p.GlobalPosition += exitDir * EXIT_STEP;
            p.SetVisible(true);
        }

        TransitionToNextState();
    }

    private void TransitionToNextState()
    {
        var nextState = GetNextState();
        if (nextState.Item1 == TeamEntityStates.ONTHEWAY || nextState.Item1 == TeamEntityStates.EXITING)
            _moveToCoords = (Vector2)nextState.Item2;
        SetNewState(nextState.Item1);
    }

    private void HandleOnTheWay(double delta)
    {
        // Losing a man on the way stops the gun where it is.
        if (!IsFullyCrewed)
        {
            ClearPendingStates();
            SetNewState(TeamEntityStates.IDLE);
            return;
        }

        var globalPositionFlat = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);
        if ((MoveToCoordinates - globalPositionFlat).Length() > 1f)
        {
            var step = StepTowards(MoveToCoordinates, delta, MOVE_SPEED, out var steer, out var n);
            if (step == StepResult.Degenerate)
                return;

            if (step == StepResult.Moved)
            {
                // Lay the carriage over the slope it is standing on.
                GlobalTransform = new Transform3D(new Basis(RotateMatchPlane(steer, n, (float)(1.0 - Mathf.Exp(-12f * delta)))), GlobalTransform.Origin);
            }

            return;
        }

        TransitionToNextState();
    }

    private void HandleIdle()
    {
        if (RaycastToTerrain(out var gnd, out var n))
        {
            GlobalTransform = new Transform3D(new Basis(RotateMatchPlane(-GlobalTransform.Basis.Z.Normalized(), n, 1f)), GlobalTransform.Origin);
            GlobalPosition = new Vector3(GlobalPosition.X, gnd.Y, GlobalPosition.Z);
        }

        TransitionToNextState();
    }

    protected override void HitTaken(int pDamage, TeamEntity pShooter, Vector3 hitPos)
    {
        if (pDamage < 50)
            Main3d.Current.Audio.Play3D("metal_ricochet", GlobalPosition);
        else
            Main3d.Current.Audio.Play3D("metal_hit_heavy", GlobalPosition);
    }
}
