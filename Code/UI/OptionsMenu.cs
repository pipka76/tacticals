using Godot;
using System;

public partial class OptionsMenu : Control
{
    Button _keybindsButton;
    Button _audioButton;
    Button _videoButton;
    ScrollContainer _keybindsMenu;
    ScrollContainer _videoMenu;
    ScrollContainer _audioMenu;
    public override void _Ready()
	{
        _keybindsButton = GetNode<Button>("MarginContainer/VBoxContainer/HBoxContainer/Keybinds");
        _audioButton = GetNode<Button>("MarginContainer/VBoxContainer/HBoxContainer/Video");
        _videoButton = GetNode<Button>("MarginContainer/VBoxContainer/HBoxContainer/Audio");
        _keybindsMenu = GetNode<ScrollContainer>("MarginContainer/VBoxContainer/KeybindsMenu");
        _videoMenu = GetNode<ScrollContainer>("MarginContainer/VBoxContainer/VideoMenu");
        _audioMenu = GetNode<ScrollContainer>("MarginContainer/VBoxContainer/AudioMenu");
    }
	
	public override void _Process(double delta)
	{
        if (_keybindsButton.ButtonPressed == false && _videoButton.ButtonPressed == false && _audioButton.ButtonPressed == false) 
        {
            _keybindsButton.ButtonPressed = true;
        }
	}

	private void OnKeybinds(bool toggledOn)
	{
        _videoButton.ButtonPressed = false;
        _audioButton.ButtonPressed = false;
        _keybindsMenu.Visible = toggledOn;
    }

    private void OnVideo(bool toggledOn)
    {
        _keybindsButton.ButtonPressed = false;
        _audioButton.ButtonPressed = false;
        _videoMenu.Visible = toggledOn;
    }

    private void OnAudio(bool toggledOn)
    {
        _keybindsButton.ButtonPressed = false;
        _videoButton.ButtonPressed = false;
        _audioMenu.Visible = toggledOn;
    }
}
