using Godot;
using System;
using tacticals_api_server.Domain;

public partial class RegisterMenu : Control
{
	private Label _registerError;
    
	public override void _Ready()
	{
		_registerError = GetNode<Label>("MarginContainer/TopContainer/Error");
	}

	private async void OnRegisterActionPressed()
	{
		var name = GetNode<LineEdit>("MarginContainer/TopContainer/BottomContainer/IGNInput");
		var email = GetNode<LineEdit>("MarginContainer/TopContainer/BottomContainer/EInput");
		var pwd = GetNode<LineEdit>("MarginContainer/TopContainer/BottomContainer/PInput");
        
		if(await BattleServer.Current.RegisterProfile(new UProfile(name.Text, email.Text, pwd.Text)))
			this.Visible = false;
		else
			_registerError.Text = "Register failed.";
	}

	private void OnCancelPressed()
	{
		this.Visible = false;
	}
}
