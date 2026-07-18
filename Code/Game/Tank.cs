using System;
using Godot;

namespace tacticals.Code.Game;

public partial class Tank : MovableTeamEntity, IPassengers
{
    private const float MOVE_SPEED = 5f;
    private AnimationPlayer _animPlayer;
    private SoundHandle? _sfxSound;
    private const float AWARE_RADIUS = 50f;
    private const float AWARE_CHECK_INTERVAL = 0.2f;
    private double _awareT = 0.0;
    private TeamEntity _enemyTarget;
    private double _attackT = 0.0;
    private const float ATTACK_INTERVAL = 15.0f;
	private const float TURRET_SPEED = 40.0f; // deg per second
    
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
        
        if (IsInState(TeamEntityStates.ATTACK))
        {
	        HandleAttack(delta);
	        return;
        }

        if (IsInState(TeamEntityStates.EXITING))
        {
            UpdatePassengersPosition(GlobalPosition);
            HandleExiting(delta);
            return;
        }

        HandleIdle(delta);
    }

    private void HandleAttack(double delta)
    {
	    // Target can disappear (freed) or be unset.
	    if (_enemyTarget == null || !GodotObject.IsInstanceValid(_enemyTarget) || _enemyTarget.IsInState(TeamEntityStates.TERMINATED) || _enemyTarget.IsMemberOf(TeamMembership.NEUTRAL))
	    {
		    _enemyTarget = null;
		    TransitionToNextState();
		    return;
	    }

	    
    }

    private Node3D? FindVisibleEnemy(float radius)
	{
		var space = GetWorld3D().DirectSpaceState;

		// Broad phase: overlap sphere
		var sphere = new SphereShape3D { Radius = radius };

		var shapeQuery = new PhysicsShapeQueryParameters3D
		{
			Shape = sphere,
			Transform = new Transform3D(Basis.Identity, GlobalPosition),
			// Set to whatever layers your "units" exist on
			CollisionMask = 0b0110, 
			CollideWithAreas = true,
			CollideWithBodies = true
		};

		// Increase if needed; 32 is fine for your scale
		var hits = space.IntersectShape(shapeQuery, 32);

		Node3D? best = null;
		float bestD2 = float.PositiveInfinity;

		var myEye = GlobalPosition + Vector3.Up * 1.4f; // “eye” height

		foreach (var h in hits)
		{
			if (!h.TryGetValue("collider", out var cv))
				continue;

			if (cv.AsGodotObject() is not Node hitNode)
				continue;

			// Walk up to your root entity type
			TeamEntity? entity = null;
			for (Node n = hitNode; n != null; n = n.GetParent())
			{
				if (n is TeamEntity te) { entity = te; break; }
			}
			if (entity == null)
				continue;

			if (entity == this) 
				continue;
			
			if (entity.IsInState(TeamEntityStates.TERMINATED))
				continue;

			// Filter enemies
			if (entity.IsMemberOf(_teamMembership) || entity.IsMemberOf(TeamMembership.NEUTRAL))
				continue;

			// Approx aim point: enemy center-ish
			var enemyPos = entity.GlobalPosition + Vector3.Up * 1.2f;
			var d2 = myEye.DistanceSquaredTo(enemyPos);
			if (d2 >= bestD2)
				continue;

			if (!HasLineOfSight(space, myEye, entity, enemyPos))
				continue;

			best = entity;
			bestD2 = d2;
		}

		return best;
	}
	
	private bool HasLineOfSight(PhysicsDirectSpaceState3D space, Vector3 from, Node3D enemyRoot, Vector3 enemyAim)
	{
		var ray = new PhysicsRayQueryParameters3D
		{
			From = from,
			To = enemyAim,
			CollideWithAreas = true,
			CollideWithBodies = true,
			CollisionMask = 0b1111 // include obstacles + units; tune
		};

		// Don’t let self-block
		ray.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var res = space.IntersectRay(ray);
		if (!res.TryGetValue("collider", out var cv))
			return false;

		if (cv.AsGodotObject() is not Node hitNode)
			return false;

		// If the ray hits anything belonging to the enemy entity, we consider it visible
		for (Node n = hitNode; n != null; n = n.GetParent())
			if (n == enemyRoot)
				return true;

		return false;
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

    private void HandleIdle(double delta)
    {
	    _awareT -= delta;
	    if (_awareT <= 0.0)
	    {
		    _awareT = AWARE_CHECK_INTERVAL;

		    var enemy = FindVisibleEnemy(AWARE_RADIUS);
		    if (enemy != null)
		    {
			    _enemyTarget = enemy as TeamEntity;
			    SetNewState(TeamEntityStates.ATTACK);
			    return;
		    }
	    }
		
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