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
    
	private void OnKeybinds(bool toggledOn)
	{
        GD.Print($"OnKeybinds: {toggledOn}");
        _keybindsMenu.Visible = toggledOn;
    }

    private void OnVideo(bool toggledOn)
    {
        GD.Print($"OnVideo: {toggledOn}");
        _videoMenu.Visible = toggledOn;
    }

    private void OnAudio(bool toggledOn)
    {
        _audioMenu.Visible = toggledOn;
    }
}
