using Godot;
using System;
using System.Threading.Tasks;
using tacticals_api_server.Domain;

public partial class LoginMenu : Control
{
    private Control _registerMenu;
    private Label _loginError;
    private LineEdit _email;
    private LineEdit _pwd;
    private CheckBox _rememberMe;

    public override void _Ready()
	{
        _registerMenu = GetNode<Control>("RegisterMenu");
        _loginError = GetNode<Label>("MarginContainer/VBoxContainer/Error");
        _email = GetNode<LineEdit>("MarginContainer/VBoxContainer/BottomContainer/EInput");
        _pwd = GetNode<LineEdit>("MarginContainer/VBoxContainer/BottomContainer/PInput");
        _rememberMe = GetNode<CheckBox>("MarginContainer/VBoxContainer/RememberMe");
    }

    /// <summary>
    /// Logs in with the "Remember me" credentials, if any, without showing the dialog.
    /// On failure the dialog is left pre-filled, so the player only has to press Log in.
    /// </summary>
    public async Task TryAutoLogin()
    {
        if (!SavedLogin.TryLoad(out var email, out var password))
            return;

        _email.Text = email;
        _pwd.Text = password;
        _rememberMe.ButtonPressed = true;

        try
        {
            if (await BattleServer.Current.LoginProfile(new UProfile(string.Empty, email, password)))
            {
                var profile = await BattleServer.Current.GetProfile();
                if (profile != null)
                    NotifyLoggedIn(profile);
            }
        }
        catch (Exception ex)
        {
            GD.Print($"TryAutoLogin: {ex.Message}");
        }
    }

    private void OnRegisterPressed()
    {
        _registerMenu.Visible = true;
    }

    private async void OnLoginPressed()
    {
        try
        {
            if (await BattleServer.Current.LoginProfile(new UProfile(string.Empty, _email.Text, _pwd.Text)))
            {
                var profile = await BattleServer.Current.GetProfile();
                if (profile != null)
                {
                    if (_rememberMe.ButtonPressed)
                        SavedLogin.Save(_email.Text, _pwd.Text);
                    else
                        SavedLogin.Clear();

                    NotifyLoggedIn(profile);
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

    private void NotifyLoggedIn(UProfile profile)
    {
        this.Visible = false;
        var sm = (StartMenu)GetParent();
        var c = new NavigateContext() { Command = "LOGIN_SUCCESS" };
        c.Metadata.Add("LoggedAs", profile.Name + " (Logout)");
        sm.OnNavigateTo(c);
    }

    private void OnCancelPressed()
    {
        this.Visible = false;
    }
}
