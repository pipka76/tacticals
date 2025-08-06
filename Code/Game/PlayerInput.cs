using Godot;

public partial class PlayerInput : MultiplayerSynchronizer
{
	private bool _processEnabled;
	private float _draggingBeginTime;
	private bool _possibleDragging;
	
	private const float ZOOM_STEP = 2f;       // degrees per scroll notch
	private const float MIN_FOV   = 5f;      // tightest zoom
	private const float MAX_FOV   = 120f;      // widest view

	public Godot.Vector2 CameraMove = Godot.Vector2.Zero;
	public bool IsSelecting;
	public bool IsMoveArmy;
	public bool IsDragging;
	public bool MapToggle;
	public float CameraFov = 100f;
	
	public override void _Input(InputEvent @event)
	{
		if(!_processEnabled)
			return;
		
		// only interested in pressed mouse‐wheel events
		if (@event is InputEventPanGesture pg)
		{
			AdjustFov(pg.GetDelta().Y);
		}

		if ((@event is InputEventMouseButton mb) && mb.Pressed)
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
	}
	
	public override void _Ready()
	{
		// Only process for the local player
		_processEnabled = true;
		//_processEnabled = GetMultiplayerAuthority() == Multiplayer.GetUniqueId();
		//SetProcess(_processEnabled);
	}
	
	public override void _Process(double delta)
	{
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
//		else
//			IsSelecting = false;
		
		CameraMove = Input.GetVector("cam_right", "cam_left", "cam_backward", "cam_forward");
	}
}
