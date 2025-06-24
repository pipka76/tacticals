using Godot;
using System;

public partial class LoginMenu : Control
{
    Control _registerMenu;

    public override void _Ready()
	{
        _registerMenu = GetNode<Control>("RegisterMenu");
    }

	public override void _Process(double delta)
	{

	}
    private void OnRegisterPressed()
    {
        _registerMenu.Visible = true;
    }
    private void OnCancelPressed()
    {
        this.Visible = false;
    }
}
