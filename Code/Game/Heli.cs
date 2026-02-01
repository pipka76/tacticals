using Godot;

namespace tacticals.Code.Game;

public partial class Heli : MovableTeamEntity
{
    private const float MOVE_SPEED = 10f;
    private const float FLIGHT_LEVEL = 100f;
    private float _aboveGround;
    private AnimationPlayer _animPlayer;

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
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        var globalPositionFlat = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);
        if ((IsInState(TeamEntityStates.HOVER) || IsInState(TeamEntityStates.ONTHEWAY)) && (MoveToCoordinates - globalPositionFlat).Length() > 0.1f)
        {
            if (RaycastToTerrain(out var gnd, out _))
            {
                var direction = (MoveToCoordinates - globalPositionFlat).Normalized();
                //_rayCast.GlobalPosition = GlobalPosition;
                //_rayCast.TargetPosition = GlobalPosition + direction * 30f;
                //if(!_rayCast.IsColliding())
                var moveXZ = GlobalPosition + direction * (float)delta * MOVE_SPEED;
                moveXZ.Y = gnd.Y + _aboveGround;
                GlobalPosition = moveXZ;
            }
        }
        else
        {
            if (IsInState(TeamEntityStates.ONTHEWAY) && _aboveGround == 0)
                SetNewState(TeamEntityStates.TAKEOFF);

            if (RaycastToTerrain(out var gnd, out _))
                GlobalPosition = new Vector3(GlobalPosition.X, gnd.Y + _aboveGround, GlobalPosition.Z);
        }
		
        HandleAnimation();
    }

    public override void MoveTo(Vector2 coords)
    {
        _moveToCoords = coords;

        if (_aboveGround != FLIGHT_LEVEL)
        {
            SetNewState(TeamEntityStates.TAKEOFF);
            return;
        }

        SetNewState(TeamEntityStates.ONTHEWAY);
    }

    private void HandleAnimation()
    {
        if (IsInState(TeamEntityStates.ONTHEWAY))
        {
            //    if (_animPlayer.CurrentAnimation != "Moving")
            //      _animPlayer.Play("Moving");
        }

        if (IsInState(TeamEntityStates.TAKEOFF) && _aboveGround < FLIGHT_LEVEL)
        {
            _aboveGround += (float)GetProcessDeltaTime();
            if (_aboveGround >= FLIGHT_LEVEL)
            {
                _aboveGround = FLIGHT_LEVEL;
                SetNewState(TeamEntityStates.HOVER);
            }
        }

        if (IsInState(TeamEntityStates.EXITING))
            SetNewState(TeamEntityStates.LANDING);
        
        if (IsInState(TeamEntityStates.LANDING) && _aboveGround > 0)
        {
            _aboveGround -= (float)GetProcessDeltaTime();
            if (_aboveGround <= 0)
            {
                _aboveGround = 0;
                SetNewState(TeamEntityStates.IDLE);
            }
        }

        if (IsInState(TeamEntityStates.IDLE))
        {
            //if (_animPlayer.CurrentAnimation != "Idle")
            //  _animPlayer.Play("Idle");
            if (RaycastToTerrain(out var gnd, out var n))
            {
                GlobalTransform =
                    new Transform3D(
                        new Basis(RotateMatchTerrain(Vector3.Forward, n, 1f)), GlobalTransform.Origin);
                GlobalPosition = new Vector3(GlobalPosition.X, gnd.Y, GlobalPosition.Z);
            }
        }
    }
}