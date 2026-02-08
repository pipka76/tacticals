using System;
using Godot;

public partial class PlayerInput : MultiplayerSynchronizer
{
	private bool _processEnabled;
	private float _draggingBeginTime;
	private bool _possibleDragging;
	
	private const float ZOOM_STEP = 2f;       // degrees per scroll notch
	private const float MIN_FOV   = 5f;      // tightest zoom
	private const float MAX_FOV   = 120f;      // widest view
	private const float MIN_CAMERA_Y = 5.0f;          // close-to-ground height
	private const float MAX_CAMERA_Y = 300.0f;          // far height
	private const float MIN_CAMERA_DEG = -60.0f; // zoomed out: look slightly down
	private const float MAX_CAMERA_DEG = -15.0f;  // zoomed in: pitch up (level up view)
	private const float YAW_SENSITIVITY = 0.005f; // radians per pixel
	private const float EDGE_YAW_PERCENT = 0.05f; // 5% of viewport width on each side
	private const float EDGE_YAW_SPEED   = 1.75f; // radians/sec at the extreme edge
	private float _groundLevel = 0;
	
	public Godot.Vector2 PlayerMove = Godot.Vector2.Zero;
	public bool IsSelecting;
	public bool IsMoveArmy;
	public bool IsDragging;
	public bool IsBoarding;
	public bool IsExiting;
	public bool MapToggle;
	public float CameraFov = 50f;
	public float CameraDegX = MIN_CAMERA_DEG;
	public float CameraY = MAX_CAMERA_Y;
	public float CameraYaw = 0f;

	public static PlayerInput Current
	{
		get;
		internal set;
	}

	public void SetGroundLevel(float val)
	{
		if (_groundLevel != val)
		{
			_groundLevel = val;
			AdjustFov(0);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!_processEnabled)
			return;

		// if (@event is InputEventMouseMotion mm)
		// {
		// 	// Positive mouse X => rotate right
		// 	//CameraYaw -= mm.Relative.X * YAW_SENSITIVITY;
		// 	var viewport = GetViewport();
		// 	float width = viewport.GetVisibleRect().Size.X;
		// 	float edge = width * 0.05f;
		// 	float mouseX = viewport.GetMousePosition().X;
		//
		// 	if (mouseX <= edge || mouseX >= (width - edge))
		// 	{
		// 		// Positive mouse X => rotate right
		// 		CameraYaw -= mm.Relative.X * YAW_SENSITIVITY;
		// 	}
		// 	return;
		// }

		// Zoom handling (trackpad)
		if (@event is InputEventPanGesture pg)
		{
			AdjustFov(pg.GetDelta().Y);
			return;
		}

		// Zoom handling (mouse wheel)
		if (@event is InputEventMouseButton mb && mb.Pressed)
		{
			switch (mb.ButtonIndex)
			{
				case MouseButton.WheelUp:
					AdjustFov(-ZOOM_STEP);
					break;
				case MouseButton.WheelDown:
					AdjustFov(+ZOOM_STEP);
					break;
			}
		}
	}
	
	private void AdjustFov(float delta)
	{
		float fov = CameraFov + delta;
		CameraFov = Mathf.Clamp(fov, MIN_FOV, MAX_FOV);
		
		// t: 0 (zoomed in) -> 1 (zoomed out)
		float t = Mathf.InverseLerp(MIN_FOV, MAX_FOV, CameraFov);
		
		// As FOV decreases (zoom in => t goes down), Y should decrease:
		// so lerp from MinY at t=0 to MaxY at t=1.
		CameraY = Mathf.Lerp(_groundLevel + MIN_CAMERA_Y, _groundLevel + MAX_CAMERA_Y, t*t);

		// As FOV decreases (t down), X rotation increases (pitch up):
		// so lerp from MaxPitchDeg at t=0 to MinPitchDeg at t=1, or invert t.
		CameraDegX = Mathf.Pi * Mathf.Lerp(MAX_CAMERA_DEG, MIN_CAMERA_DEG, t) / 360;
	}
	
	public override void _Ready()
	{
		Current = this;
		// Only process for the local player
		_processEnabled = true;
		//_processEnabled = GetMultiplayerAuthority() == Multiplayer.GetUniqueId();
		//SetProcess(_processEnabled);
	}
	
	public override void _Process(double delta)
	{
		// Edge-based camera yaw: rotate left when cursor is near the left edge,
		// rotate right when cursor is near the right edge.
		// This is continuous (doesn't require mouse movement).
		var viewport = GetViewport();
		float width = viewport.GetVisibleRect().Size.X;
		if (width > 0f)
		{
			float edge = width * EDGE_YAW_PERCENT;
			float mouseX = viewport.GetMousePosition().X;

			// strength in [-1, 1]
			float strength = 0f;
			if (mouseX <= edge)
			{
				// left edge: strength goes 1 at x=0 -> 0 at x=edge
				strength = 1f - Mathf.Clamp(mouseX / edge, 0f, 1f);
			}
			else if (mouseX >= (width - edge))
			{
				// right edge: strength goes 0 at x=width-edge -> 1 at x=width
				strength = -Mathf.Clamp((mouseX - (width - edge)) / edge, 0f, 1f);
			}

			// Match existing convention: decreasing CameraYaw rotates right.
			CameraYaw += strength * EDGE_YAW_SPEED * (float)delta;
		}
		
		if (IsSelecting && !_possibleDragging && !IsDragging && Input.IsActionPressed("select_entity"))
		{
			_draggingBeginTime = Time.GetTicksMsec() / 1000f;
			_possibleDragging = true;
		}

		if (_possibleDragging && ((Time.GetTicksMsec() / 1000f - _draggingBeginTime) > 0.5f))
		{
			IsDragging = true;
			IsSelecting = false;
		}

		if (Input.IsActionJustReleased("map_toggle"))
            MapToggle = !MapToggle;

        if (IsDragging && !Input.IsActionPressed("select_entity"))
			IsDragging = false;
		
//		if (!IsDragging)
			IsSelecting = Input.IsActionPressed("select_entity");
			IsMoveArmy = Input.IsActionPressed("move_army");
			IsBoarding = Input.IsActionPressed("board_entity");
			IsExiting = Input.IsActionPressed("exit_entity");
//		else
//			IsSelecting = false;
		
		var move = Input.GetVector("cam_left", "cam_right", "cam_forward", "cam_backward");
		// Rotate movement by camera yaw so WASD is relative to where the camera is looking.
		var move3 = new Vector3(move.X, 0f, move.Y).Rotated(Vector3.Up, CameraYaw);
		PlayerMove = new Vector2(move3.X, move3.Z);
	}
}
