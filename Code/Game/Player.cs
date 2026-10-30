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
	private CommandCursor _commandCursor;
	/// <summary>This frame's ground pick, shared by every command handler and the marker.</summary>
	private bool _hasHover;
	private Vector3 _hoverPos;
	private Vector3 _hoverNormal = Vector3.Up;
	/// <summary>
	/// The structure being placed, between clicking a crate in build mode and confirming where it
	/// stands. While set, the mouse belongs to the placement and no other command is dispatched.
	/// </summary>
	private BuildSite _placement;
	private Crate _placementCrate;
	private Soldier _placementCarrier;
	private readonly List<Soldier> _placementBuilders = new List<Soldier>();
	private Vector3 _placementPos;
	private Vector3 _placementNormal = Vector3.Up;
	private float _placementYaw;
	private bool _placementPlaceable;
	/// <summary>The click that picked the crate has been let go, so the next press is a fresh one.</summary>
	private bool _placementArmed;
	/// <summary>Position is pinned by a press; dragging from here aims the structure.</summary>
	private bool _placementAnchored;
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
	private const float CRATE_REACH = 5f;          // same reach as boarding
	private const float PLACEMENT_MIN_DRAG = 0.5f;  // shorter drags keep the previous heading
	/// <summary>
	/// Layer 5, "Picking": click-only colliders, for structures whose real colliders are too thin
	/// to hit with the mouse. Only the click rays here look at it - bullets and sight lines don't.
	/// </summary>
	private const uint PICK_LAYER = 0b10000;
	
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

		_godCamera.SetCurrent(true);
		// The camera sits up to 300 units out; the default 0.05 near plane spends its depth
		// precision on the first hand-span in front of the lens and leaves ground overlays
		// shimmering against the terrain.
		_godCamera.Near = 1f;

		// Local player only - the marker is aiming feedback for whoever is holding the mouse.
		// TopLevel matters: _Process pans this node to move the camera, and the marker works in
		// world space.
		if (GetNode("PlayerInput").GetMultiplayerAuthority() == Multiplayer.GetUniqueId())
		{
			_commandCursor = new CommandCursor { Name = "CommandCursor", TopLevel = true };
			AddChild(_commandCursor);
		}

		//Input.SetCustomMouseCursor(_mouse_cursor_select, Input.CursorShape.Arrow, new Vector2(0, 8));
		//Input.SetCustomMouseCursor(_mouse_cursor_moveto, Input.CursorShape.Cross, new Vector2(8, 10));
		//Input.SetCustomMouseCursor(_mouse_cursor_board, Input.CursorShape.PointingHand, new Vector2(8, 100));
		//Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
	}

	private void DeployArmy()
	{
		// just test
		var map = GetParent().GetParent() as IGameMap;
		if (map != null)
		{
			// Rect2 takes a SIZE, not an opposite corner: 7x5 box centred on (25, 50).
			map.PathField.AddObstacle(new Rect2(new Vector2(25-3f, 50-2f), new Vector2(6f, 4f)), MovementDomain.Ground);
			
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

	/// <param name="skipBoarded">
	/// Look past units sitting inside something: they are hidden but keep their collider, so a click
	/// on a tower or vehicle would otherwise pick the soldier in it instead.
	/// </param>
	private Node MouseRaycastToEntity(uint collisionMask, bool skipBoarded = false)
	{
		var mousePos = GetViewport().GetMousePosition();
		var from = _godCamera.ProjectRayOrigin(mousePos);
		var to = from + _godCamera.ProjectRayNormal(mousePos) * VIEW_DISTANCE;
		var space = GetWorld3D().DirectSpaceState;
		var rayQuery = new PhysicsRayQueryParameters3D();
		rayQuery.From = from;
		rayQuery.To = to;
		rayQuery.CollisionMask = collisionMask;
		var skipped = new Godot.Collections.Array<Rid>();

		// Bounded: each pass excludes one more boarded unit.
		for (int i = 0; i < 8; i++)
		{
			rayQuery.Exclude = skipped;
			var result = space.IntersectRay(rayQuery);
			if (!result.TryGetValue("collider", out Variant collider) || collider.Obj == null)
				return null;

			if (skipBoarded && collider.Obj is TeamEntity te && te.IsInState(TeamEntityStates.BOARDED))
			{
				skipped.Add(te.GetRid());
				continue;
			}

			return collider.Obj as Node;
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
		rayQuery.CollisionMask = 0b011 | PICK_LAYER;
		
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

	/// <summary>
	/// Where the mouse is pointing on the ground, and the surface normal there. Called once per
	/// frame into _hoverPos/_hoverNormal - every command handler and the ground marker read those
	/// fields rather than raycasting for themselves, so what the player aims at and what a click
	/// resolves to can never be a frame apart.
	/// </summary>
	private bool MouseRaycastToTerrain(out Vector3 hitPos, out Vector3 hitNormal)
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
		if (result.TryGetValue("position", out var posVar))
		{
			hitPos = posVar.AsVector3();
			hitNormal = result.TryGetValue("normal", out var normalVar) ? normalVar.AsVector3() : Vector3.Up;
			return true;
		}

		hitPos = Vector3.Inf;
		hitNormal = Vector3.Up;
		return false;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		var map = GetParent().GetParent() as IGameMap;
		if (map != null)
		{
			var camBaseLevel = map.GetTerrainHeight(new Vector2(this.GlobalPosition.X, this.GlobalPosition.Z)); // get height of the place where player's camera is looking at
			PlayerInput.Current.SetGroundLevel(camBaseLevel);

			// Ground truth of what is actually closed off; no-ops unless the debug overlay is on.
			GameDebug.Current?.RegisterBlockedCells(map.PathField, MovementDomain.Ground);
		}

		_godCamera.Fov = _inputs.CameraFov;
		_godCamera.Rotation = new Vector3(_inputs.CameraDegX, _inputs.CameraYaw, 0);
		_godCamera.Position = new Vector3(_godCamera.Position.X, _inputs.CameraY, _godCamera.Position.Z);

		// One ground pick for the whole frame, taken after the camera has been moved into this
		// frame's pose so the marker does not trail the view while panning or zooming.
		_hasHover = MouseRaycastToTerrain(out _hoverPos, out _hoverNormal);

		// Read every frame so a stray right click is not left over for the next placement.
		bool cancel = _inputs.IsCancelPressed;

		// The mode says which command is being aimed; the click confirms it.
		if (_placement != null)
		{
			HandlePlacement(cancel);
		}
		else if (_inputs.IsSelecting)
		{
			switch (_inputs.Mode)
			{
				case CommandMode.Move: HandleMoveToCommand(); break;
				case CommandMode.Board: HandleBoardCommand(); break;
				case CommandMode.Exit: HandleExitCommand(); break;
				case CommandMode.Patrol: HandlePatrolCommand(); break;
				case CommandMode.Build: HandleBuildCommand(); break;
				default: HandleSelectCommand(); break;
			}
		}

		if (_inputs.IsPatrolArmyReleased)
        {
            HandlePatrolMode();
        }

        if (_inputs.IsMoveArmyReleased)
        {
            HandleMoveMode();
        }

        // Show the route being laid down while the button is held (debug overlay only).
        if (_inputs.IsMoveArmy && _moveWaypoints.Count > 0)
	        GameDebug.Current?.RegisterPath(_moveWaypoints.ToArray(), false);

		if (_inputs.MapToggle)
		{
			if (map != null)
			{
				map.ToggleMinimap();
				_inputs.MapToggle = false;
            }
        }
		
		this.Position += new Vector3(_inputs.PlayerMove.X, 0,  _inputs.PlayerMove.Y) * (float)delta * CAM_MOVE_SPEED;

		// Last, so a route committed on this frame is already cleared when the marker is drawn.
		if (_placement != null)
			_commandCursor?.RenderPlacement(_placementPos, _placementNormal, _placementYaw, _placement.Radius, _placementPlaceable);
		else
			_commandCursor?.Render(_inputs.Mode, _hasHover, _hoverPos, _hoverNormal,
				_hasHover ? _godCamera.GlobalPosition.DistanceTo(_hoverPos) : 0f);
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

        if (!_hasHover)
            return;

        var whereTo3 = _hoverPos;

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
			var nodeHit = MouseRaycastToEntity(0b1110, skipBoarded: true);
			var entity = nodeHit as TeamEntity ?? MouseRaycastToGarrison();
			if (entity != null)
			{
				entity.IsSelected = !entity.IsSelected;
				_selectCooldown = Time.GetTicksMsec() / 1000f;
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

		if (!_hasHover)
			return;

		_moveToCooldown = Time.GetTicksMsec() / 1000f;

		_moveWaypoints.Add(_hoverPos);
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

		foreach (var g in GetGarrisons())
			g.IsSelected = false;
	}

	/// <summary>
	/// A tower has no body on the unit layers - one there would block the sight lines into its
	/// windows - so a click on it is found through its model's colliders, or its pick collider.
	/// </summary>
	private TeamEntity MouseRaycastToGarrison()
	{
		for (Node n = MouseRaycastToEntity(0b1 | PICK_LAYER); n != null; n = n.GetParent())
		{
			if (n is TeamEntity entity)
				return entity.IsInGroup(EntityGroup.GARRISONS) ? entity : null;
		}

		return null;
	}

	/// <summary>Structures soldiers can get into - outside the map's entity list, so asked for separately.</summary>
	private IEnumerable<TeamEntity> GetGarrisons()
	{
		return GetTree().GetNodesInGroup(EntityGroup.GARRISONS).OfType<TeamEntity>();
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
					
					if ((p.GlobalPosition - interactWith.GlobalPosition).Length() > iPass.BoardReach)
						continue;
					
					if(iPass.BoardPassenger(p) && iPass.HidesPassengers)
						p.SetVisible(false);
				}

				// Boarding hides the soldiers inside the vehicle, so their selection goes with them.
				// Hooking up a crate leaves them on foot, likely to be ordered on straight away.
				if (interactWith is not Crate)
					DeselectAllEntities();
			}
		}
	}
	private void HandleExitCommand()
	{
		if ((Time.GetTicksMsec() / 1000f - _moveToCooldown) > CLICK_COOLDOWN)
		{
			if (_hasHover)
			{
				var whereTo = _hoverPos;
				_moveToCooldown = Time.GetTicksMsec() / 1000f;
				var map = GetParent().GetParent() as IGameMap;
				foreach (var entity in map.GetEntities(_myTeam).Concat(GetGarrisons()))
				{
					if (!entity.IsSelected || !entity.IsMemberOf(_myTeam))
						continue;

					if (entity is TeamEntity and IPassengers iPass)
					{
						entity.EnqueueState(TeamEntityStates.EXITING, new Vector2(whereTo.X, whereTo.Z));
					}
				}
			}
		}
	}

	/// <summary>
	/// Build mode click. On a crate it starts placing the crate's content; on a structure that is
	/// still going up it sends the selected soldiers back to work on it.
	/// </summary>
	private void HandleBuildCommand()
	{
		if ((Time.GetTicksMsec() / 1000f - _moveToCooldown) <= CLICK_COOLDOWN)
			return;

		var target = MouseRaycastToBuildTarget() ?? GroundPickBuildSite();
		if (target == null)
			return;
		_moveToCooldown = Time.GetTicksMsec() / 1000f;

		var builders = GetSelectedSoldiers();
		if (builders.Count == 0)
			return;

		if (target is Crate crate)
		{
			BeginPlacement(crate, builders);
			return;
		}

		if (target is BuildSite site && !site.IsComplete)
		{
			foreach (var builder in builders)
				builder.Build(site);
			DeselectAllEntities();
		}
	}

	private void BeginPlacement(Crate crate, List<Soldier> builders)
	{
		// Someone has to pull the crate to the site: whoever already is, else the nearest
		// selected soldier in reach of it.
		var carrier = crate.Dragger as Soldier;
		if (carrier == null)
		{
			float best = CRATE_REACH;
			foreach (var builder in builders)
			{
				float dist = builder.GlobalPosition.DistanceTo(crate.GlobalPosition);
				if (dist <= best)
				{
					best = dist;
					carrier = builder;
				}
			}
		}

		if (carrier == null)
			return;

		_placementCrate = crate;
		_placementCarrier = carrier;
		_placementBuilders.Clear();
		_placementBuilders.AddRange(builders);
		if (!_placementBuilders.Contains(carrier) && carrier.Team == _myTeam)
			_placementBuilders.Add(carrier);

		_placementPos = crate.GlobalPosition;
		_placementNormal = Vector3.Up;
		// Front towards the camera, so the player sees the side they are lining up.
		_placementYaw = _inputs.CameraYaw + Mathf.Pi;
		_placementArmed = false;
		_placementAnchored = false;

		_placement = BuildSite.Preview(crate);
		crate.GetParent().AddChild(_placement);
		UpdatePlacementTransform();
	}

	/// <summary>
	/// The blueprint follows the mouse until a press pins it; dragging from the pinned spot turns
	/// it to face the mouse, and letting go confirms. Right click / Esc backs out.
	/// </summary>
	private void HandlePlacement(bool cancel)
	{
		if (cancel || !GodotObject.IsInstanceValid(_placementCrate) || _placementCrate.IsQueuedForDeletion())
		{
			CancelPlacement();
			return;
		}

		// The click that picked the crate may still be held - wait for it to come up first.
		if (!_placementArmed && !_inputs.IsSelecting)
			_placementArmed = true;

		if (!_placementAnchored)
		{
			if (_hasHover)
			{
				_placementPos = _hoverPos;
				_placementNormal = _hoverNormal;
				if (_placementArmed && _inputs.IsSelecting)
					_placementAnchored = true;
			}
		}
		else
		{
			if (_hasHover)
			{
				var drag = new Vector2(_hoverPos.X - _placementPos.X, _hoverPos.Z - _placementPos.Z);
				if (drag.Length() > PLACEMENT_MIN_DRAG)
					_placementYaw = Mathf.Atan2(-drag.X, -drag.Y);
			}

			if (!_inputs.IsSelecting)
			{
				if (_placementPlaceable)
				{
					ConfirmPlacement();
					return;
				}

				// Can't stand there - pick it back up and let the player try another spot.
				_placementAnchored = false;
			}
		}

		UpdatePlacementTransform();
	}

	private void UpdatePlacementTransform()
	{
		_placement.GlobalTransform = new Transform3D(new Basis(Vector3.Up, _placementYaw), _placementPos);
		_placementPlaceable = _placement.IsPlaceable();
		_placement.ShowPlaceable(_placementPlaceable);
	}

	private void ConfirmPlacement()
	{
		var site = _placement;
		site.Commit();

		if (_placementCrate.Dragger == null && GodotObject.IsInstanceValid(_placementCarrier)
			&& !_placementCarrier.IsInState(TeamEntityStates.TERMINATED))
			_placementCrate.BoardPassenger(_placementCarrier);

		foreach (var builder in _placementBuilders)
		{
			if (GodotObject.IsInstanceValid(builder) && !builder.IsInState(TeamEntityStates.TERMINATED))
				builder.Build(site);
		}

		DeselectAllEntities();
		_selectCooldown = Time.GetTicksMsec() / 1000f;
		ClearPlacement();
	}

	private void CancelPlacement()
	{
		_placement.QueueFree();
		ClearPlacement();
	}

	private void ClearPlacement()
	{
		_placement = null;
		_placementCrate = null;
		_placementCarrier = null;
		_placementBuilders.Clear();
	}

	private List<Soldier> GetSelectedSoldiers()
	{
		var soldiers = new List<Soldier>();
		var map = GetParent().GetParent() as IGameMap;
		if (map == null)
			return soldiers;

		foreach (var entity in map.GetEntities(_myTeam))
		{
			if (entity.IsSelected && entity is Soldier soldier && !soldier.IsInState(TeamEntityStates.TERMINATED))
				soldiers.Add(soldier);
		}

		return soldiers;
	}

	/// <summary>
	/// Fallback for clicks that pass between a scaffold's legs and hit the ground: a click
	/// anywhere inside a site's footprint counts as clicking the site.
	/// </summary>
	private BuildSite GroundPickBuildSite()
	{
		if (!_hasHover)
			return null;

		foreach (var node in GetTree().GetNodesInGroup(EntityGroup.BUILD_SITES))
		{
			if (node is BuildSite site && site.IsCommitted && !site.IsComplete && site.ContainsGroundPoint(_hoverPos))
				return site;
		}

		return null;
	}

	/// <summary>
	/// What the mouse points at that the build command can act on: a crate, or a structure going up
	/// (found by walking up from its collider to the site that holds it).
	/// </summary>
	private Node3D MouseRaycastToBuildTarget()
	{
		var mousePos = GetViewport().GetMousePosition();
		var from = _godCamera.ProjectRayOrigin(mousePos);
		var to = from + _godCamera.ProjectRayNormal(mousePos) * VIEW_DISTANCE;
		var space = GetWorld3D().DirectSpaceState;
		var rayQuery = new PhysicsRayQueryParameters3D();
		rayQuery.From = from;
		rayQuery.To = to;
		rayQuery.CollisionMask = 0b011 | PICK_LAYER;

		var result = space.IntersectRay(rayQuery);
		if (result.TryGetValue("collider", out var v) && v.AsGodotObject() is Node hit)
		{
			for (Node n = hit; n != null; n = n.GetParent())
			{
				if (n is Crate crate) return crate;
				if (n is BuildSite site) return site;
			}
		}

		return null;
	}
}
