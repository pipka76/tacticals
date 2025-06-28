using Godot;
using System;
using tacticals_api_server.Domain;

public partial class LoginMenu : Control
{
    private Control _registerMenu;
    private Label _loginError;
    
    public override void _Ready()
	{
        _registerMenu = GetNode<Control>("RegisterMenu");
        _loginError = GetNode<Label>("MarginContainer/VBoxContainer/Error");
    }

    private void OnRegisterPressed()
    {
        _registerMenu.Visible = true;
    }

    private async void OnLoginPressed()
    {
        var email = GetNode<LineEdit>("MarginContainer/VBoxContainer/BottomContainer/EInput");
        var pwd = GetNode<LineEdit>("MarginContainer/VBoxContainer/BottomContainer/PInput");

        try
        {
            if (await BattleServer.Current.LoginProfile(new UProfile(string.Empty, email.Text, pwd.Text)))
            {
                var profile = await BattleServer.Current.GetProfile();
                if (profile != null)
                {
                    this.Visible = false;
                    var sm = (StartMenu)GetParent();
                    var c = new NavigateContext() { Command = "LOGIN_SUCCESS" };
                    c.Metadata.Add("LoggedAs", profile.Name + " (Logout)");
                    sm.OnNavigateTo(c);
                }
            }
            else
                _loginError.Text = "Login failed. Invalid credentials.";
        }
        catch (Exception ex)
        {
            _loginError.Text = ex.Message;
            GD.Print(ex.Message);
        }   
    }
    
    private void OnCancelPressed()
    {
        this.Visible = false;
    }
}
