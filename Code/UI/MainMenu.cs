using Godot;
using System;

public partial class MainMenu : Control, IGameMenu
{
    public NavigateContext Context { get; set; }

    private void JoinGame()
    {
        var main = (Main)GetParent();
        main.NavigateTo(Main.NAVIGATE_TARGET.LOBBYMENU);
    }

    private void Options()
    {
    }

    private void QuitGame()
    {
        GetTree().Quit();
    }

    public void OnNavigateTo(NavigateContext context)
    {
    }
}
