using System;
using Godot;

namespace tacticals.Code.Game;

public partial class Tank : MovableTeamEntity, IPassengers
{
    private const float MOVE_SPEED = 5f;
    private AnimationPlayer _animPlayer;
    private SoundHandle? _sfxSound;

    public Tank()
    {
        _sfxSound = null;
        _maxPassengersCapacity = 3;
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

        if (IsInState(TeamEntityStates.ONTHEWAY))
        {
            UpdatePassengersPosition(GlobalPosition);
            HandleOnTheWay(delta);
            return;
        }

        if (IsInState(TeamEntityStates.EXITING))
        {
            UpdatePassengersPosition(GlobalPosition);
            HandleExiting(delta);
            return;
        }

        HandleIdle();
    }

    private void HandleExiting(double delta)
    {
        var passengers = ExitPassengers();
        var exitDir = (new Vector3(_moveToCoords.X, GlobalPosition.Y, _moveToCoords.Y) - GlobalPosition).Normalized();
        foreach (var p in passengers)
        {
            p.GlobalPosition = p.GlobalPosition + exitDir * 5f;
            p.SetVisible(true);
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

    private void HandleOnTheWay(double delta)
    {
        var globalPositionFlat = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);
        if ((MoveToCoordinates - globalPositionFlat).Length() > 1f)
        {
            if (RaycastToTerrain(out var gnd, out var n))
            {
                n = n.Normalized();
                
                // Desired direction to our assigned slot/target (MoveToCoordinates)
                var toTarget = (MoveToCoordinates - globalPositionFlat);
                float dist = toTarget.Length();
                var desired = dist > 0.0001f ? (toTarget / dist) : Vector3.Zero;

                // Local steering to keep a loose crowd spacing
                var separation = ComputeSeparation(globalPositionFlat, EntityGroup.GROUND_UNIT);
                var steer = desired + separation * SEPARATION_WEIGHT;
                steer.Y = 0;
                if (steer.LengthSquared() < 0.000001f)
                    return;
                steer = steer.Normalized();

                // Arrival: slow down near target to avoid jitter/pile-ups
                float speedFactor = 1.0f;
                if (dist < ARRIVE_SLOW_RADIUS)
                {
                    speedFactor = Mathf.Clamp(dist / ARRIVE_SLOW_RADIUS, MIN_SPEED_FACTOR, 1.0f);
                }

                var move = GlobalPosition + steer * (float)delta * (MOVE_SPEED * speedFactor);
                move.Y = gnd.Y;
                GlobalPosition = move;
                GlobalTransform = new Transform3D(new Basis(RotateMatchPlane(steer, n,(float)(1.0 - Math.Exp(-12f * GetProcessDeltaTime())))), GlobalTransform.Origin);
            }

            if (!_sfxSound.HasValue)
                _sfxSound = Main.Current.Audio.Play3D("tank_driving", GlobalPosition, true);

            return;
        }

        TransitionToNextState();
    }

    private void HandleIdle()
    {
        TransitionToNextState();
    }
    
    protected override void HitTaken(int pDamage, TeamEntity pShooter, Vector3 hitPos)
    {
        if (pDamage < 50)
            Main.Current.Audio.Play3D("metal_ricochet", GlobalPosition);
        else
            Main.Current.Audio.Play3D("metal_hit_heavy", GlobalPosition);
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
            if (_sfxSound.HasValue)
            {
                Main.Current.Audio.Stop(_sfxSound.Value);
                _sfxSound = null;
            }

            //if (_animPlayer.CurrentAnimation != "Idle")
              //  _animPlayer.Play("Idle");
              if (RaycastToTerrain(out var gnd, out var n))
              {
                  GlobalTransform =
                      new Transform3D(
                          new Basis(RotateMatchPlane(-GlobalTransform.Basis.Z.Normalized(), n, 1f)), GlobalTransform.Origin);
                  GlobalPosition = new Vector3(GlobalPosition.X, gnd.Y, GlobalPosition.Z);
              }

        }
    }

}