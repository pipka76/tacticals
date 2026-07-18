using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot.NativeInterop;
using tacticals.Code.Game;

public partial class Player : Node3D
{
	// Player synchronized input.
	private PlayerInput _inputs;
	private Camera3D _godCamera;
	private float _selectCooldown;
	private float _moveToCooldown;
	private float _patrolCmdCooldown;
	/// <summary>Waypoints collected while "move_army" is held, committed on release.</summary>
	private readonly List<Vector3> _moveWaypoints = new List<Vector3>();
    private Vector3 _homeBaseCoords;
	private TeamMembership _myTeam;
	
	#region MouseCursors
	private Texture2D _mouse_cursor_board;
	private Texture2D _mouse_cursor_moveto;
	private Texture2D _mouse_cursor_select;
	#endregion
	
	private const float CAM_MOVE_SPEED = 50f;
	private const float VIEW_DISTANCE = 1000f;
	private const float CLICK_COOLDOWN = 0.2f;
	private const float FORMATION_SPACING = 2f;   // distance between soldiers (XZ)
	private const float FORMATION_JITTER = 0.35f;   // random jitter factor (0..1)
	
	// Set by the authority, synchronized on spawn.
	[Export]
	public int PlayerId
	{
		get => _playerId;
		set
		{
			_playerId = value;
			// Give authority over the player input to the appropriate peer.
			GetNode("PlayerInput").SetMultiplayerAuthority(value);
		}
	}

	[Export]
	public Vector3 HomeBaseCoordinates
	{
		get => _homeBaseCoords;
		set
		{
			_homeBaseCoords = value;
		}
	}

	private int _playerId = 1;	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_myTeam = TeamMembership.BLUE;
		_inputs = GetNode<PlayerInput>("PlayerInput");
		_godCamera = GetNode<Camera3D>("GodCamera");
		_mouse_cursor_board = GD.Load<Texture2D>("res://Assets/UI/mouse_cursor_board.png");
		_mouse_cursor_moveto = GD.Load<Texture2D>("res://Assets/UI/mouse_cursor_moveto.png");
		_mouse_cursor_select = GD.Load<Texture2D>("res://Assets/UI/mouse_cursor_select.png");
		DeployArmy();
		
		Input.SetCustomMouseCursor(_mouse_cursor_select, Input.CursorShape.Arrow, new Vector2(0, 8));
		Input.SetCustomMouseCursor(_mouse_cursor_moveto, Input.CursorShape.Cross, new Vector2(8, 10));
		Input.SetCustomMouseCursor(_mouse_cursor_board, Input.CursorShape.PointingHand, new Vector2(8, 100));
		Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
	}

	private void DeployArmy()
	{
		// just test
		var map = GetParent().GetParent() as IGameMap;
		if (map != null)
		{
			var army = new List<TeamEntity>();
			var soldier = GD.Load<PackedScene>("res://Scenes/Game/Soldier.tscn");
			for (int i = 0; i < 3; i++)
			{
				var s = (TeamEntity)soldier.Instantiate();
				s.SetMembership(_myTeam);
				map.SpawnEntity(s, new Vector2(200, 200));
				army.Add(s);
			}

			// testing the enemy soldier
			for (int i = 0; i < 4; i++)
			{
				var enemySoldier = (TeamEntity)soldier.Instantiate();
				enemySoldier.SetMembership(TeamMembership.RED, true);
				var mesh = enemySoldier.GetNode<MeshInstance3D>("GeneralSkeleton/SoldierMesh");
				// Get the material used in that surface (index 2 in your case)
				var mat = mesh.GetActiveMaterial(2) as StandardMaterial3D;
				if (mat != null)
				{
					// Duplicate so it’s not shared
					var unique = (StandardMaterial3D)mat.Duplicate();
					unique.AlbedoColor = Colors.Red;

					// Assign the unique copy to this instance
					mesh.SetSurfaceOverrideMaterial(2, unique);
				}
				map.SpawnEntity(enemySoldier, new Vector2(133 - i*18, 76 + i*30));
				if (i == 2 || i == 1)
				{
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(135, 0, 75));
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(80, 0, 75));
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(76.3f, 0, 115));
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(76.3f, 0, 167));
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(120, 0, 167));
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(120, 0, 125));
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(135, 0, 125));
                }
                else
				{
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(120, 0, 167));
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(120, 0, 125));
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(135, 0, 125));
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(135, 0, 75));
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(80, 0, 75));
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(76.3f, 0, 115));
                    ((Soldier)enemySoldier).AddPatrolCheckpoint(new Vector3(76.3f, 0, 167));
                }
                ((Soldier)enemySoldier).OnBeginPatrol();
                ((Soldier)enemySoldier).SetNewState(TeamEntityStates.PATROL);
            }

            // var tank = GD.Load<PackedScene>("res://Scenes/Game/Tank.tscn");
            // var t = (TeamEntity)tank.Instantiate();
            // map.SpawnEntity(t);
            // _myArmy.Add(t); 
            //
            // var heli = GD.Load<PackedScene>("res://Scenes/Game/Heli.tscn");
            // var h = (TeamEntity)heli.Instantiate();
            // map.SpawnEntity(h);
            // _myArmy.Add(h); 

            var dest = GetMyBasePosition();
			// Build ring-scatter slots around the destination
			var slots = BuildRingScatterSlots(dest, 12, FORMATION_SPACING);

			// Sort units by their current angle around the group's centroid,
			// and slots by their angle around the destination. This reduces crossing.
			var selected = army.Where(e => e is MovableTeamEntity).Cast<MovableTeamEntity>().ToList();
			Vector2 srcCentroid = ComputeCentroidXZ(selected);
			selected.Sort((a, b) =>
			{
				float aa = AngleOf(new Vector2(a.GlobalPosition.X, a.GlobalPosition.Z) - srcCentroid);
				float ab = AngleOf(new Vector2(b.GlobalPosition.X, b.GlobalPosition.Z) - srcCentroid);
				return aa.CompareTo(ab);
			});

			slots.Sort((p, q) =>
			{
				float ap = AngleOf(p - dest);
				float aq = AngleOf(q - dest);
				return ap.CompareTo(aq);
			});
			
			for (int i = 0; i < selected.Count; i++)
			{
				selected[i].PortTo(slots[i]);
			}

			/*
			var red = GD.Load<Material>("res://Assets/Game/red-team.tres");

			int i = 0;
			var aUnit = GameUtils.GenerateGrid(Godot.Vector2.Zero, 3,3, 1f);
			foreach (var au in aUnit)
			{
				var s = (Node3D)soldier.Instantiate();
				var mesh = s.GetNode<Node3D>("SoldierLevel1");
				var body = (MeshInstance3D)mesh.GetChild(1);
				body.MaterialOverride = red;
				s.Name = $"{Multiplayer.GetUniqueId()}-{i++}";
				map.SpawnEntity(s);
				((MovableTeamEntity)s).MoveTo(new Vector2(au.X, au.Y));
				_myArmy.Add((TeamEntity)s);
			}*/
		}
	}

	private Vector2 GetMyBasePosition()
	{
		// TODO
		// for testing only
		return new Vector2(20, 20);
	}

	private Node MouseRaycastToEntity(uint collisionMask)
	{
		var mousePos = GetViewport().GetMousePosition();
		var from = _godCamera.ProjectRayOrigin(mousePos);
		var to = from + _godCamera.ProjectRayNormal(mousePos) * VIEW_DISTANCE;
		var space = GetWorld3D().DirectSpaceState;
		var rayQuery = new PhysicsRayQueryParameters3D();
		rayQuery.From = from;
		rayQuery.To = to;
		rayQuery.CollisionMask = collisionMask;
		
		var result = space.IntersectRay(rayQuery);
		if (result.TryGetValue("collider", out Variant collider))
		{
			if (collider.Obj != null)
			{
				return collider.Obj as Node;
				//return (collider.Obj as Node)?.GetParent();
			}
		}

		return null;
	}

	private TeamEntity MouseRaycastToTeamEntity()
	{
		var mousePos = GetViewport().GetMousePosition();
		var from = _godCamera.ProjectRayOrigin(mousePos);
		var to = from + _godCamera.ProjectRayNormal(mousePos) * VIEW_DISTANCE;
		var space = GetWorld3D().DirectSpaceState;
		var rayQuery = new PhysicsRayQueryParameters3D();
		rayQuery.From = from;
		rayQuery.To = to;
		rayQuery.CollisionMask = 0b011;
		
		var result = space.IntersectRay(rayQuery);
		if (result.TryGetValue("collider", out var v))
		{
			if (v.AsGodotObject() is Node hit)
			{
				var team = hit.GetParentOrNull<TeamEntity>();
				if (team != null) return team;

				// If collider might be deeper than one level:
				for (Node n = hit; n != null; n = n.GetParent())
					if (n is TeamEntity t) return t;
			}
		}
		
		return null;
	}

	private Vector3 MouseRaycastToTerrain()
	{
		var mousePos = GetViewport().GetMousePosition();
		var from = _godCamera.ProjectRayOrigin(mousePos);
		var to = from + _godCamera.ProjectRayNormal(mousePos) * VIEW_DISTANCE;
		var space = GetWorld3D().DirectSpaceState;
		var rayQuery = new PhysicsRayQueryParameters3D();
		rayQuery.From = from;
		rayQuery.To = to;
		rayQuery.CollisionMask = 1;
		
		var result = space.IntersectRay(rayQuery);
		if (result.TryGetValue("position", out Variant posVar))
		{
			return (Vector3)posVar.Obj;
		}

		return Vector3.Inf;
	}
	
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
	
		var map = GetParent().GetParent() as IGameMap;
		if (map != null)
		{
			var camBaseLevel = map.GetTerrainHeight(new Vector2(this.GlobalPosition.X, this.GlobalPosition.Z)); // get height of the place where player's camera is looking at
			PlayerInput.Current.SetGroundLevel(camBaseLevel);
		}

		_godCamera.Fov = _inputs.CameraFov;
		_godCamera.Rotation = new Vector3(_inputs.CameraDegX, _inputs.CameraYaw, 0);
		_godCamera.Position = new Vector3(_godCamera.Position.X, _inputs.CameraY, _godCamera.Position.Z);
		
		if (_inputs.IsSelecting)
		{
			if (_inputs.IsMoveArmy)
			{
				Input.SetDefaultCursorShape(Input.CursorShape.Cross);
				HandleMoveToCommand();
				goto specialModeExit;
			}

			if (_inputs.IsBoarding)
			{
				Input.SetDefaultCursorShape(Input.CursorShape.Cross);
				HandleBoardCommand();
				goto specialModeExit;
			}

			if (_inputs.IsExiting)
			{
				Input.SetDefaultCursorShape(Input.CursorShape.Cross);
				HandleExitCommand();
				goto specialModeExit;
			}

			if (_inputs.IsPatrolArmy)
			{
                Input.SetDefaultCursorShape(Input.CursorShape.Cross);
                HandlePatrolCommand();
                goto specialModeExit;
            }

            HandleSelectCommand();
		}

    specialModeExit:

		if (_inputs.IsPatrolArmyReleased)
        {
            HandlePatrolMode();
        }

        if (_inputs.IsMoveArmyReleased)
        {
            HandleMoveMode();
        }

        if (_inputs.IsMoveArmy)
		{
			Input.SetDefaultCursorShape(Input.CursorShape.Cross);

			// Show the route being laid down while the button is held (debug overlay only).
			if (_moveWaypoints.Count > 0)
				GameDebug.Current?.RegisterPath(_moveWaypoints.ToArray(), false);
		}

		if (_inputs.MapToggle)
		{
			if (map != null)
			{
				map.ToggleMinimap();
				_inputs.MapToggle = false;
            }
        }
		
		this.Position += new Vector3(_inputs.PlayerMove.X, 0,  _inputs.PlayerMove.Y) * (float)delta * CAM_MOVE_SPEED;
	}

    private void HandlePatrolMode()
    {
        // Collect selected movable entities
        var map = GetParent().GetParent() as IGameMap;
        var selected = new List<MovableTeamEntity>();
        if (map != null)
        {
            foreach (var entity in map.GetEntities(_myTeam))
            {
                if (!entity.IsSelected)
                    continue;
                if (entity is MovableTeamEntity m)
                    selected.Add(m);
            }
        }

        if (selected.Count == 0)
            return;

        for (int i = 0; i < selected.Count; i++)
        {
            selected[i].OnBeginPatrol();
        }
    }

    private void HandlePatrolCommand()
    {
        if ((Time.GetTicksMsec() / 1000f - _patrolCmdCooldown) <= CLICK_COOLDOWN)
            return;

        var whereTo3 = MouseRaycastToTerrain();
        if (whereTo3 == Vector3.Inf)
            return;

        // Collect selected movable entities
        var map = GetParent().GetParent() as IGameMap;
        var selected = new List<MovableTeamEntity>();
        if (map != null)
        {
            foreach (var entity in map.GetEntities(_myTeam))
            {
                if (!entity.IsSelected)
                    continue;
                if (entity is MovableTeamEntity m)
                    selected.Add(m);
            }
        }

        if (selected.Count == 0)
            return;

        _patrolCmdCooldown = Time.GetTicksMsec() / 1000f;

        for (int i = 0; i < selected.Count; i++)
        {
            selected[i].AddPatrolCheckpoint(whereTo3);
        }
    }

    private void HandleSelectCommand()
	{
		if ((Time.GetTicksMsec() / 1000f - _selectCooldown) > CLICK_COOLDOWN)
		{
			var nodeHit = MouseRaycastToEntity(0b1110);
			if (nodeHit != null)
			{
				var entity = nodeHit as TeamEntity;
				if (entity != null)
				{
					entity.IsSelected = !entity.IsSelected;
					_selectCooldown = Time.GetTicksMsec() / 1000f;
				}
			}
		}
	}

	private static float AngleOf(Vector2 v)
	{
		return Mathf.Atan2(v.Y, v.X);
	}

	private List<Vector2> BuildRingScatterSlots(Vector2 center, int count, float spacing)
	{
		// Concentric rings with slight randomness, good for "loose crowd".
		var slots = new List<Vector2>(count);
		var rng = new RandomNumberGenerator();
		rng.Randomize();

		// Always put one near the center (slightly jittered)
		if (count <= 0) return slots;
		slots.Add(center + new Vector2(
			rng.RandfRange(-spacing, spacing) * 0.15f,
			rng.RandfRange(-spacing, spacing) * 0.15f
		));

		int placed = 1;
		int ring = 1;
		while (placed < count)
		{
			float radius = ring * spacing;
			// Number of slots on this ring based on circumference / spacing
			int ringSlots = Mathf.Max(6, Mathf.FloorToInt((2.0f * Mathf.Pi * radius) / spacing));

			for (int i = 0; i < ringSlots && placed < count; i++)
			{
				float baseAngle = (i / (float)ringSlots) * Mathf.Tau;
				float angleJitter = rng.RandfRange(-1f, 1f) * (FORMATION_JITTER * 0.35f);
				float rJitter = rng.RandfRange(-1f, 1f) * (FORMATION_JITTER * spacing * 0.25f);

				float a = baseAngle + angleJitter;
				float r = radius + rJitter;
				var offset = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
				slots.Add(center + offset);
				placed++;
			}

			ring++;
		}

		return slots;
	}

	private static Vector2 ComputeCentroidXZ(List<MovableTeamEntity> units)
	{
		if (units.Count == 0) return Vector2.Zero;
		Vector2 sum = Vector2.Zero;
		foreach (var u in units)
			sum += new Vector2(u.GlobalPosition.X, u.GlobalPosition.Z);
		return sum / units.Count;
	}

	/// <summary>
	/// Records one waypoint per click while "move_army" is held. Nothing is issued to the units
	/// until the button is released - see HandleMoveMode.
	/// </summary>
	private void HandleMoveToCommand()
	{
		if ((Time.GetTicksMsec() / 1000f - _moveToCooldown) <= CLICK_COOLDOWN)
			return;

		var whereTo3 = MouseRaycastToTerrain();
		if (whereTo3 == Vector3.Inf)
			return;

		_moveToCooldown = Time.GetTicksMsec() / 1000f;

		_moveWaypoints.Add(whereTo3);
	}

	/// <summary>
	/// Fired when "move_army" is released: sends the selected units through the collected
	/// waypoints in order, once. Intermediate waypoints are shared by the whole group; only the
	/// final one is spread into formation slots, so the group travels together and forms up on arrival.
	/// </summary>
	private void HandleMoveMode()
	{
		if (_moveWaypoints.Count == 0)
			return;

		// Collect selected movable entities
		var map = GetParent().GetParent() as IGameMap;
		var selected = new List<MovableTeamEntity>();
		if (map != null)
		{
			foreach (var entity in map.GetEntities(_myTeam))
			{
				if (!entity.IsSelected)
					continue;
				if (entity is MovableTeamEntity m)
					selected.Add(m);
			}
		}

		if (selected.Count == 0)
		{
			// Nothing to command - drop the waypoints rather than leaking them into the next order.
			_moveWaypoints.Clear();
			return;
		}

		var last = _moveWaypoints[_moveWaypoints.Count - 1];
		Vector2 dest = new Vector2(last.X, last.Z);

		// Build ring-scatter slots around the destination
		var slots = BuildRingScatterSlots(dest, selected.Count, FORMATION_SPACING);

		// Sort units by their current angle around the group's centroid,
		// and slots by their angle around the destination. This reduces crossing.
		Vector2 srcCentroid = ComputeCentroidXZ(selected);
		selected.Sort((a, b) =>
		{
			float aa = AngleOf(new Vector2(a.GlobalPosition.X, a.GlobalPosition.Z) - srcCentroid);
			float ab = AngleOf(new Vector2(b.GlobalPosition.X, b.GlobalPosition.Z) - srcCentroid);
			return aa.CompareTo(ab);
		});

		slots.Sort((p, q) =>
		{
			float ap = AngleOf(p - dest);
			float aq = AngleOf(q - dest);
			return ap.CompareTo(aq);
		});

		// Legs shared by everyone: every waypoint except the last.
		var sharedLegs = new List<Vector2>(_moveWaypoints.Count);
		for (int i = 0; i < _moveWaypoints.Count - 1; i++)
			sharedLegs.Add(new Vector2(_moveWaypoints[i].X, _moveWaypoints[i].Z));

		for (int i = 0; i < selected.Count; i++)
		{
			var route = new List<Vector2>(sharedLegs) { slots[i] };
			selected[i].MoveAlong(route);
		}

		_moveWaypoints.Clear();
	}

	private void DeselectAllEntities()
	{
		var map = GetParent().GetParent() as IGameMap;
		if (map != null)
		{
			foreach (var p in map.GetEntities())
				p.IsSelected = false;
		}
	}

	private void HandleBoardCommand()
	{
		if ((Time.GetTicksMsec() / 1000f - _moveToCooldown) > CLICK_COOLDOWN)
		{
			var interactWith = MouseRaycastToTeamEntity();
			if (interactWith == null)
				return;
			if (interactWith is IPassengers iPass)
			{
				_moveToCooldown = Time.GetTicksMsec() / 1000f;
				var map = GetParent().GetParent() as IGameMap;
				foreach (var p in map.GetEntities(_myTeam))
				{
					if (!p.IsSelected)
						continue;
					
					if ((p.GlobalPosition - interactWith.GlobalPosition).Length() > 5f)
						continue;
					
					if(iPass.BoardPassenger(p))
						p.SetVisible(false);
				}

				DeselectAllEntities();
			}
		}
	}
	private void HandleExitCommand()
	{
		if ((Time.GetTicksMsec() / 1000f - _moveToCooldown) > CLICK_COOLDOWN)
		{
			var whereTo = MouseRaycastToTerrain();
			if (whereTo != Vector3.Inf)
			{
				_moveToCooldown = Time.GetTicksMsec() / 1000f;
				var map = GetParent().GetParent() as IGameMap;
				foreach (var entity in map.GetEntities(_myTeam))
				{
					if (!entity.IsSelected)
						continue;

					if (entity is TeamEntity and IPassengers iPass)
					{
						entity.EnqueueState(TeamEntityStates.EXITING, new Vector2(whereTo.X, whereTo.Z));
					}
				}
			}
		}
	}
}
