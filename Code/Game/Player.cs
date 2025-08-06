using Godot;
using System;
using System.Collections.Generic;
using Godot.NativeInterop;

public partial class Player : Node3D
{
	// Player synchronized input.
	private PlayerInput _inputs;
	private Camera3D _godCamera;
	private float _selectCooldown;
	private float _moveToCooldown;
	private List<TeamEntity> _myArmy;
	private Vector3 _homeBaseCoords;
	
	private const float CAM_MOVE_SPEED = 20f;
	private const float VIEW_DISTANCE = 100f;
	private const float CLICK_COOLDOWN = 0.2f;
	
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
		_myArmy = new List<TeamEntity>();
		_inputs = GetNode<PlayerInput>("PlayerInput");
		_godCamera = GetNode<Camera3D>("GodCamera");

		DeployArmy();
	}

	private void DeployArmy()
	{
		// just test
		var map = GetParent().GetParent() as IGameMap;
		if (map != null)
		{
			var soldier = GD.Load<PackedScene>("res://Scenes/Game/Soldier.tscn");
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
			}
		}
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
				return (collider.Obj as Node)?.GetParent();
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
		_godCamera.Fov = _inputs.CameraFov;
		
		if (_inputs.IsSelecting)
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

		if (_inputs.IsMoveArmy)
		{
			if ((Time.GetTicksMsec() / 1000f - _moveToCooldown) > CLICK_COOLDOWN)
			{
				var whereTo = MouseRaycastToTerrain();
				if (whereTo != Vector3.Inf)
				{
					_moveToCooldown = Time.GetTicksMsec() / 1000f;
					foreach (var entity in _myArmy)
					{
						if (!entity.IsSelected)
							continue;

						if (entity is MovableTeamEntity)
						{
							((MovableTeamEntity)entity).MoveTo(new Vector2(whereTo.X, whereTo.Z));
						}
					}
				}
			}
		}

		if (_inputs.MapToggle)
		{
            var map = GetParent().GetParent() as IGameMap;
			if (map != null)
			{
				map.ToggleMinimap();
				_inputs.MapToggle = false;
            }
        }

        this.Position += new Vector3( _inputs.CameraMove.X, 0,  _inputs.CameraMove.Y) * (float)delta * CAM_MOVE_SPEED;
	}
}
