using System;
using Godot;

namespace tacticals.Code.Game;

public partial class Tank : MovableTeamEntity
{
    private const float MOVE_SPEED = 5f;
    private AnimationPlayer _animPlayer;

    public Tank()
    {
        _maxPassengersCapacity = 3;
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
        if (IsInState(TeamEntityStates.ONTHEWAY) && (MoveToCoordinates - globalPositionFlat).Length() > 0.1f)
        {
            if (RaycastToTerrain(out var gnd, out var n))
            {
                n = n.Normalized();
                
                var direction = (MoveToCoordinates - globalPositionFlat).Normalized();
                //_rayCast.GlobalPosition = GlobalPosition;
                //_rayCast.TargetPosition = GlobalPosition + direction * 30f;
                //if(!_rayCast.IsColliding())
                var moveXZ = GlobalPosition + direction * (float)delta * MOVE_SPEED;
                moveXZ.Y = gnd.Y;
                GlobalPosition = moveXZ;
                
                GlobalTransform = new Transform3D(new Basis(RotateMatchTerrain(direction, n,(float)(1.0 - Math.Exp(-12f * GetProcessDeltaTime())))), GlobalTransform.Origin);
            }
        }
        else
        {
            if (IsInState(TeamEntityStates.ONTHEWAY))
                SetNewState(TeamEntityStates.IDLE);
        }
		
        HandleAnimation();
    }
    
    private void HandleAnimation()
    {
        if (IsInState(TeamEntityStates.ONTHEWAY))
        {
        //    if (_animPlayer.CurrentAnimation != "Moving")
          //      _animPlayer.Play("Moving");
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