using Godot;
using System;

public partial class PlayerDebug : Node
{
	private bool _debugEnabled = false;

	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("toggle_debug"))
		{
			_debugEnabled = !_debugEnabled;
			GD.Print($"Debug mode: {_debugEnabled}");
			ToggleFOV();
		}
	}

	public void ToggleFOV()
	{
		
	}
}
