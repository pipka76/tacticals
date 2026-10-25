using Godot;
using System;
using tacticals_api_server.Domain;

public partial class StartMenu : Control, IGameMenu
{
    TextureButton _hamButton;
    HBoxContainer _popupMenu;
    Control _optionsMenu;
    Control _loginMenu;
    ArmyGrid _armyGrid;

    private bool _isLoggedIn = false;
    private GameMode _gameModeSelected = GameMode.CLASSIC;
    
    private enum GameMode
    {
        CLASSIC,
        TRAIN_DEFENSE
    }

    public NavigateContext Context { get; set; }

    public override void _Ready()
	{
        _popupMenu = GetNode<HBoxContainer>("StartMenuContents/PopupMenu");
        _hamButton = GetNode<TextureButton>("StartMenuContents/VBoxContainer/TopMarginContainer/Hamburger");
        _optionsMenu = GetNode<Control>("OptionsMenu");
        _loginMenu = GetNode<Control>("LoginMenu");
        _armyGrid = GetNode<ArmyGrid>("StartMenuContents/VBoxContainer/ArmyGrid");
    }

    private void OnClassicMode(bool toggledOn)
    {
        _gameModeSelected = GameMode.CLASSIC;
    }

    private void OnTrainMode(bool toggledOn)
    {
        _gameModeSelected = GameMode.TRAIN_DEFENSE;
    }

    private void OnShowPopupMenu()
    {
        _popupMenu.Visible = true;
        _hamButton.Visible = false;
    }

    private void OnHidePopupMenu()
    {
        _popupMenu.Visible = false;
        _hamButton.Visible = true;
    }

    private void OnBattlePressed()
    {
        // Battles are tied to the profile, so the lobby needs a login.
        if (BattleServer.Current.CurrentProfile == null)
        {
            _loginMenu.Visible = true;
            return;
        }

        Main3d.Current.Session.ArmyLayout = _armyGrid.GetLayout();
        var c = new NavigateContext() { Command = (_gameModeSelected == GameMode.CLASSIC) ? "CLASSIC" : "TRAIN_DEFENSE" };
        Main3d.Current.NavigateTo(Main3d.NAVIGATE_TARGET.LOBBYMENU, c);
    }

    public void OnNavigateTo(NavigateContext context)
    {
        if (context != null && context.Metadata.ContainsKey("LoggedAs"))
        {
            var loginBtn = GetNode<Button>("StartMenuContents/VBoxContainer/TopMarginContainer/btnLoginLogout");
            context.Metadata.TryGetValue("LoggedAs", out string lasVal);
            loginBtn.Text = lasVal;
            _isLoggedIn = true;
        }
    }

    private void OnLoginRegisterPressed()
    {
        if (_isLoggedIn)
        {
            // logout
            var loginBtn = GetNode<Button>("StartMenuContents/VBoxContainer/TopMarginContainer/btnLoginLogout");
            BattleServer.Current.Logout();
            loginBtn.Text = "LOGIN/REGISTER";
            _isLoggedIn = false;
        }
        else
        {
            // show login dialog
            _loginMenu.Visible = true;
        }
    }
   
    private void OnOptionsPressed()
    {
        this.Visible = false;
        _optionsMenu.Visible = true;
    }
}
