using Godot;
using System;

namespace tacticals.Code.Game;

public partial class Soldier : MovableTeamEntity
{
	private const float SOLDIER_FOV = 60f;
    private const float MOVE_SPEED = 2.5f;
	private RayCast3D _rayCast;
	private AnimationPlayer _animPlayer;
	private SoundHandle? _sfxSound;
	private const float AWARE_RADIUS = 50f;
	private const float AWARE_CHECK_INTERVAL = 0.2f;
	private const float LOOKOUT_INTERVAL = 15f;
	private const float LOOKOUT_DURATION = 8f;
	private double _awareT = 0.0;
	private TeamEntity _enemyTarget;
	private const float ATTACK_INTERVAL = 3.0f;
	private double _attackT = 0.0;
	private double _eyeAngle = 0.0;
	private double _lookoutT = 0.0;
	private double _lookoutD = 0.0;

	public Soldier()
	{
		_maxPassengersCapacity = 0;
	}
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_rayCast = new RayCast3D();
		_rayCast.CollisionMask = 0b1111;
		_rayCast.Enabled = true;
		
		_selectorObject = GetNode<Node3D>("SelectionRing");
		_synchronizer = GetNode<MultiplayerSynchronizer>("ServerSynchronizer");
		_animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		//_synchronizer.SetVisibilityFor();
		AddChild(_rayCast);
		AddToGroup(EntityGroup.GROUND_UNIT);
		AddToGroup(EntityGroup.SOLDIERS);
	}

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
        HandleAnimation();
		if (IsInState(TeamEntityStates.TERMINATED))
			return;
        
		ReportDebug();

        if (_damageTaken > 0)
		{
			SetNewState(TeamEntityStates.TERMINATED);
			return;
		}

		if (IsInState(TeamEntityStates.ONTHEWAY))
		{
			ResetLookout();
			HandleOnTheWay(delta);
			return;
		}

		if (IsInState(TeamEntityStates.PATROL)) 
		{
			HandlePatrol(delta);
			return;
		}

		if (IsInState(TeamEntityStates.ATTACK))
		{
            ResetLookout();
            HandleAttack(delta);
			return;
		}

		if (IsInState(TeamEntityStates.BOARDED))
		{
            ResetLookout();
            Mute();
			return;
		}

		HandleIdle(delta);
	}

    private void HandlePatrol(double delta)
	{
		var moveTo = GetPatrolCheckpoint();

		repeat:
		if (moveTo == null)
			return;

		GameDebug.Current.RegisterPatrolPath(_patrolCheckpoints.ToArray());

        var globalPositionFlat = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);
		if ((moveTo.Value - globalPositionFlat).Length() > 1f)
		{
			// NOTE: the Degenerate early-out skips HandleLookout/CheckForEnemies below. That is
			// pre-existing behaviour, preserved here deliberately - changing it is a separate call.
			var step = StepTowards(moveTo.Value, delta, MOVE_SPEED, out _, out _);
			if (step == StepResult.Degenerate)
				return;

			if (step == StepResult.Moved)
				RotateTowards(moveTo.Value);

			//Mute();
		}
		else
		{
            moveTo = GetPatrolCheckpoint(true);
			goto repeat;
        }
		HandleLookout(delta, 8, 5);
		if (CheckForEnemies(delta))
		{
			SetNewState(TeamEntityStates.ATTACK);
			EnqueueState(TeamEntityStates.PATROL);
		}
    }

	private void HandleLookout(double delta, double duration = LOOKOUT_DURATION, double interval = LOOKOUT_INTERVAL)
	{
        _lookoutT -= delta;
        if (_lookoutT <= 0.0)
        {
            _lookoutD -= delta;

            double t = 1.0 - (_lookoutD / duration); // 0.0 → 1.0 over the duration
            _eyeAngle = (Math.PI / 2) * Math.Sin(2 * Math.PI * t);

            if (_lookoutD <= 0.0)
            {
                _lookoutD = duration;
                _lookoutT = interval;
                _eyeAngle = 0.0;
            }
        }
    }

    private void HandleAttack(double delta)
	{
		// Fire in ~5s intervals as long as target remains in range + visible.
		_attackT -= delta;
		if (_attackT > 0.0)
			return;

		_attackT = ATTACK_INTERVAL;

		// Target can disappear (freed) or be unset.
		if (_enemyTarget == null || !GodotObject.IsInstanceValid(_enemyTarget) || _enemyTarget.IsInState(TeamEntityStates.TERMINATED) || _enemyTarget.IsMemberOf(TeamMembership.NEUTRAL))
		{
			_enemyTarget = null;
			TransitionToNextState();
			return;
		}

		RotateTowards(new Vector3(_enemyTarget.GlobalPosition.X, 0, _enemyTarget.GlobalPosition.Z));

		// Range check
		if ((_enemyTarget.GlobalPosition - GlobalPosition).Length() > AWARE_RADIUS)
		{
			_enemyTarget = null;
			TransitionToNextState();
			return;
		}

		var space = GetWorld3D().DirectSpaceState;
		var myEye = GlobalPosition + Vector3.Up * 1.4f;
		var enemyAim = _enemyTarget.GlobalPosition + Vector3.Up * 1.2f;

		// Visibility/LoS check
		if (HasLineOfSight(space, myEye, _enemyTarget, enemyAim))
		{
			FireOnTarget(enemyAim);
			return;
		}

		// Not visible -> stop attacking and let state machine decide what's next.
		_enemyTarget = null;
		TransitionToNextState();
	}

	private void FireOnTarget(Vector3 target)
	{
		var inaccX = Random.Shared.Next(-(int)target.X, (int)target.X);
		var inaccY = Random.Shared.Next(-(int)target.Y, (int)target.Y);
		var inaccZ = Random.Shared.Next(-(int)target.Z, (int)target.Z);
		
		Main.Current.Audio.Play3D("handgun_shot", GlobalPosition);
		var weapPos = GlobalPosition + Vector3.Up * 1.3f;
		var fireDirection = (target - weapPos).Normalized() * 300f;
		Main.Current.Projectiles.Spawn(new Projectile()
		{
			Shooter = this,
			Pos = weapPos,
			Vel = new Vector3(fireDirection.X + inaccX/50f, fireDirection.Y + inaccY/50f, fireDirection.Z + inaccZ/50f),
			Mask = 0b111,
			ShooterRid = GetRid(),
			Life = 1,
			Damage = 10
		});
	}

	private bool CheckForEnemies(double delta)
	{
        _awareT -= delta;
        if (_awareT <= 0.0)
        {
            _awareT = AWARE_CHECK_INTERVAL;

            var enemy = FindVisibleEnemy(AWARE_RADIUS);
            if (enemy != null)
            {
                _enemyTarget = enemy as TeamEntity;
                _attackT = 1.5f;
                return true;
            }
        }

		return false;
    }

	private void HandleIdle(double delta)
	{
		if(CheckForEnemies(delta))
            EnqueueState(TeamEntityStates.ATTACK);
        HandleLookout(delta);
        TransitionToNextState();
	}

	private void ResetLookout()
	{
		_lookoutT = 0.0;
		_eyeAngle = 0.0;
		_lookoutD = LOOKOUT_DURATION;
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

            if (!IsInFieldOfView(myEye, enemyPos, SOLDIER_FOV)) // total cone angle
                continue;

            if (!HasLineOfSight(space, myEye, entity, enemyPos))
				continue;

			best = entity;
			bestD2 = d2;
		}

		return best;
	}

    private bool IsInFieldOfView(Vector3 myEye, Vector3 enemyPos, float AngleDeg)
    {
        // Reconstruct forward the same way RotateTowards sets it
        float yaw = (float)_eyeAngle + GlobalRotation.Y;
        var forward = new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw));

        // Flatten enemy direction to XZ plane
        var toEnemy = new Vector3(enemyPos.X - myEye.X, 0, enemyPos.Z - myEye.Z).Normalized();

        float dot = forward.Dot(toEnemy);
        return dot >= Mathf.Cos(Mathf.DegToRad(AngleDeg / 2));
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

	private void HandleOnTheWay(double delta)
	{
		var globalPositionFlat = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);
		if ((MoveToCoordinates - globalPositionFlat).Length() > 1f)
		{
			if (StepTowards(MoveToCoordinates, delta, MOVE_SPEED, out _, out _) == StepResult.Degenerate)
				return;

			Mute();

			return;
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

		if (stopSound) Mute();
	}

	private void Mute()
	{
		if (_sfxSound.HasValue)
		{
			Main.Current.Audio.Stop(_sfxSound.Value);
			_sfxSound = null;
		}
	}

	protected override void HitTaken(int pDamage, TeamEntity pShooter, Vector3 hitPos)
	{
		Mute();
		Main.Current.Audio.Play3D("soldier_died", GlobalPosition);
	}

	private void ReportDebug()
	{
        float yaw = (float)_eyeAngle + GlobalRotation.Y;
        GameDebug.Current?.RegisterFov(GlobalPosition + Vector3.Up * 1.4f, SOLDIER_FOV, new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw)), AWARE_RADIUS);
    }

    private void HandleAnimation()
	{
		if (IsInState(TeamEntityStates.ONTHEWAY) || IsInState(TeamEntityStates.PATROL))
		{
			if (_animPlayer.CurrentAnimation != "Walking")
				_animPlayer.Play("Walking");
		}

		if (IsInState(TeamEntityStates.TERMINATED))
		{
			Visible = false;
		}

		if (IsInState(TeamEntityStates.IDLE))
		{
			if (_animPlayer.CurrentAnimation != "Idle")
				_animPlayer.Play("Idle");
			if (RaycastToTerrain(out var gnd, out _))
				GlobalPosition = new Vector3(GlobalPosition.X, gnd.Y, GlobalPosition.Z);
		}
	}
}
