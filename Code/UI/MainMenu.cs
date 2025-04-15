using Godot;
using System;

public partial class MainMenu : Control
{
    private void NewGame()
    {
        var g = (Main)GetParent();
        g.StartServer();
    }

    private void JoinGame()
    {
        //var serverAddress = this.GetNodeOrNull<LineEdit>("HFlowContainer/btnJoinServer/tbServerAddress");
        var serverAddress = "localhost"; // TODO
        if (String.IsNullOrEmpty(serverAddress))
        {
            GD.Print("Server address is empty. Unable to join.");
            return;
        }

        var nm = (Main)GetParent();
        nm.JoinServer(serverAddress);
    }
    
    private void QuitGame()
    {
        GetTree().Quit();
    }
}
