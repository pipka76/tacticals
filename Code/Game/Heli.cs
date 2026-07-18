using System.Collections.Generic;
using Godot;

namespace tacticals.Code.Game;

public partial class Heli : MovableTeamEntity, IPassengers
{
    private const float MOVE_SPEED = 10f;
    private const float FLIGHT_LEVEL = 100f;
    private const float CLIMB_SPEED = 15f;
    private float _aboveGround;
    private AnimationPlayer _animPlayer;  
    private SoundHandle? _sfxSound;

    public Heli()
    {
        _maxPassengersCapacity = 4;
        _aboveGround = 0;
    }

    public override void _Ready()
    {
        _selectorObject = GetNode<Node3D>("SelectionRing");
        _synchronizer = GetNode<MultiplayerSynchronizer>("ServerSynchronizer");
        _animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        AddToGroup(EntityGroup.MACHINERY);
        AddToGroup(EntityGroup.GROUND_UNIT);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        HandleAnimation();
        
        if (RaycastToTerrain(out var gnd, out _))
            GlobalPosition = new Vector3(GlobalPosition.X, gnd.Y + _aboveGround, GlobalPosition.Z);

        if (IsInState(TeamEntityStates.ONTHEWAY))
        {
            UpdatePassengersPosition(GlobalPosition);
            HandleOnTheWay(delta);
            return;
        }

        if (IsInState(TeamEntityStates.LANDING))
        {
            UpdatePassengersPosition(GlobalPosition);
            HandleLanding(delta);
            return;
        }

        if (IsInState(TeamEntityStates.EXITING))
        {
            UpdatePassengersPosition(GlobalPosition);
            HandleExiting(delta);
            return;
        }

        if (IsInState(TeamEntityStates.TAKEOFF))
        {
            UpdatePassengersPosition(GlobalPosition);
            HandleTakeOff(delta);
            return;
        }

        if (IsInState(TeamEntityStates.HOVER))
        {
            HandleHover();
            return;
        }
        
        HandleIdle();
    }

    private void HandleIdle()
    {
        TransitionToNextState();
    }

    private void HandleOnTheWay(double delta)
    {
        var globalPositionFlat = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);
        if ((MoveToCoordinates - globalPositionFlat).Length() > 1f)
        {
            if (RaycastToTerrain(out var gnd, out _))
            {
                var direction = (MoveToCoordinates - globalPositionFlat).Normalized();
                
                // 1) Face the move direction (Godot forward is -Z, so this makes -Z point to dir)
                Basis face = Basis.LookingAt(direction, Vector3.Up);
                // 2) Additional rotation (lean / match plane). This must return a rotation (Basis or Quaternion)
                Basis extra = new Basis(
                    RotateMatchPlane(
                        Vector3.Forward,
                        Vector3.Up.Rotated(Vector3.Left, 0.35f),
                        1f
                    )
                );
                // 3) Combine them
                Basis finalBasis = face * extra; // local-space “extra” tilt
                
                //_rayCast.GlobalPosition = GlobalPosition;
                //_rayCast.TargetPosition = GlobalPosition + direction * 30f;
                //if(!_rayCast.IsColliding())
                
                var moveXZ = GlobalPosition + direction * (float)delta * MOVE_SPEED;
                moveXZ.Y = gnd.Y + _aboveGround;
                GlobalPosition = moveXZ;
                GlobalTransform = new Transform3D(finalBasis, GlobalTransform.Origin);
            }

            if (!_sfxSound.HasValue)
                _sfxSound = Main.Current.Audio.Play3D("heli_flight", GlobalPosition, true);

            return;
        }

        TransitionToNextState();
    }

    private void HandleHover()
    {
        if (!_sfxSound.HasValue)
            _sfxSound = Main.Current.Audio.Play3D("heli_hover", GlobalPosition, true);

        TransitionToNextState(true);
    }

    private void HandleTakeOff(double delta)
    {
        if (_aboveGround < FLIGHT_LEVEL)
        {
            if (!_sfxSound.HasValue)
                _sfxSound = Main.Current.Audio.Play3D("heli_take_off", GlobalPosition);
            
            _aboveGround += (float)delta * CLIMB_SPEED;
        }
        
        if (_aboveGround >= FLIGHT_LEVEL)
        {
            RemoveFromGroup(EntityGroup.GROUND_UNIT);
            AddToGroup(EntityGroup.AIR_UNIT);

            _aboveGround = FLIGHT_LEVEL;
            TransitionToNextState();
        }
    }

    private void HandleLanding(double delta)
    {
        if (_aboveGround > 0)
        {
            if (!_sfxSound.HasValue)
                _sfxSound = Main.Current.Audio.Play3D("heli_hover", GlobalPosition, true);
            
            _aboveGround -= (float)delta * CLIMB_SPEED;
            if (_aboveGround <= 0)
            {
                _aboveGround = 0;
            }
        }

        if (_aboveGround == 0)
        {
            RemoveFromGroup(EntityGroup.AIR_UNIT);
            AddToGroup(EntityGroup.GROUND_UNIT);
            
            if (RaycastToTerrain(out var gnd, out var n))
            {
                GlobalTransform =
                    new Transform3D(
                        new Basis(RotateMatchPlane(-GlobalTransform.Basis.Z.Normalized(), n, 1f)), GlobalTransform.Origin);
                GlobalPosition = new Vector3(GlobalPosition.X, gnd.Y, GlobalPosition.Z);
            }
         
            if (_sfxSound.HasValue)
            {
                Main.Current.Audio.Stop(_sfxSound.Value);
            }
            Main.Current.Audio.Play3D("heli_off", GlobalPosition);
            
            TransitionToNextState();
        }
    }
    
    private void HandleExiting(double delta)
    {
        if (_aboveGround > 0)
        {
            EnqueueState(TeamEntityStates.ONTHEWAY, _moveToCoords);
            EnqueueState(TeamEntityStates.LANDING);
            EnqueueState(TeamEntityStates.EXITING, _moveToCoords);
        }

        if (_aboveGround == 0)
        {
            var passengers = ExitPassengers();
            var exitDir = (new Vector3(_moveToCoords.X, GlobalPosition.Y, _moveToCoords.Y) - GlobalPosition).Normalized();
            foreach (var p in passengers)
            {
                p.GlobalPosition = p.GlobalPosition + exitDir * 5f;
                p.SetVisible(true);
            }
        }

        TransitionToNextState();
    }

    private void TransitionToNextState(bool ignoreIdle = false, bool stopSound = true)
    {
        var nextState = GetNextState();
        if (ignoreIdle && (nextState.Item1 == TeamEntityStates.IDLE))
            return;
        if (nextState.Item1 == TeamEntityStates.ONTHEWAY || nextState.Item1 == TeamEntityStates.EXITING)
            _moveToCoords = (Vector2)nextState.Item2;
        SetNewState(nextState.Item1);

        if (stopSound && _sfxSound.HasValue)
        {
            Main.Current.Audio.Stop(_sfxSound.Value);
            _sfxSound = null;
        }
    }

    public override void MoveAlong(IReadOnlyList<Vector2> waypoints)
    {
        if (waypoints == null || waypoints.Count == 0)
            return;

        // this is command from player directly, so forget everything and listen!
        ClearPendingStates();
        _moveToCoords = waypoints[0];

        // Grounded, the first leg has to wait behind the climb, so it gets queued with the rest.
        // Already at altitude, the first leg starts immediately and only the rest are queued.
        bool grounded = _aboveGround != FLIGHT_LEVEL;

        for (int i = grounded ? 0 : 1; i < waypoints.Count; i++)
            EnqueueState(TeamEntityStates.ONTHEWAY, waypoints[i]);

        // Settle into a hover once the last leg is done, rather than dropping straight to IDLE.
        EnqueueState(TeamEntityStates.HOVER, null);

        SetNewState(grounded ? TeamEntityStates.TAKEOFF : TeamEntityStates.ONTHEWAY);
    }

    private void HandleAnimation()
    {
        if (IsInState(TeamEntityStates.ONTHEWAY))
        {
        }

        if (IsInState(TeamEntityStates.TAKEOFF))
        {
        }

        if (IsInState(TeamEntityStates.IDLE))
        {
        }
    }
}