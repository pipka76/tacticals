using Godot;

public partial class PlayerInput : MultiplayerSynchronizer
{
	private bool _processEnabled;
	private float _draggingBeginTime;
	private bool _possibleDragging;

	public Godot.Vector2 CameraMove = Godot.Vector2.Zero;
	public bool IsSelecting;
	public bool IsMoveArmy;
	public bool IsDragging;
	
	public override void _Input(InputEvent @event)
	{
		if(!_processEnabled)
			return;
	}
	
	public override void _Ready()
	{
		// Only process for the local player
		_processEnabled = GetMultiplayerAuthority() == Multiplayer.GetUniqueId();
		SetProcess(_processEnabled);
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

		if (IsDragging && !Input.IsActionPressed("select_entity"))
			IsDragging = false;
		
//		if (!IsDragging)
			IsSelecting = Input.IsActionPressed("select_entity");
			IsMoveArmy = Input.IsActionPressed("move_army");
//		else
//			IsSelecting = false;
		
		CameraMove = Input.GetVector("cam_left", "cam_right", "cam_forward", "cam_backward");
	}
}
