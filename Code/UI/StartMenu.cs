using Godot;
using System;

public partial class StartMenu : Control
{
	TextureButton _classicModeButton;
    TextureButton _trainModeButton;
    HBoxContainer _unitContainer;

    private bool _isUpdating = false;
    public override void _Ready()
	{
		_classicModeButton = GetNode<TextureButton>("MarginContainer/VBoxContainer/TopMarginContainer/TopMenu/HBoxContainer/btnClassicMode");
        _trainModeButton = GetNode<TextureButton>("MarginContainer/VBoxContainer/TopMarginContainer/TopMenu/HBoxContainer/btnTrainDefence");
        _unitContainer = GetNode<HBoxContainer>("MarginContainer/VBoxContainer/BottomMarginContainer/UnitCatalog");
    }

	public override void _Process(double delta)
	{

	}

    private void OnClassicMode(bool toggledOn)
    {
        GD.Print($"OnClassicMode: {toggledOn}");
        _trainModeButton.ButtonPressed = !toggledOn;

    }

    private void OnTrainMode(bool toggledOn)
    {
        GD.Print($"OnTrainMode: {toggledOn}");
        _classicModeButton.ButtonPressed = !toggledOn;
    }

    private void OnUnitSelected(bool toggledOn, string unitId)
    {
        GD.Print($"OnUnitSelected: {toggledOn} {unitId}");
        if(toggledOn)
            UnselectOtherUnits(unitId);
    }
    private void UnselectOtherUnits(string unitId) 
    {
        for (int i = 0; i < _unitContainer.GetChildCount(); i++)
        {
            var tButton = _unitContainer.GetChild<TextureButton>(i);
            if(tButton.Name != unitId)
                tButton.ButtonPressed = false;
        }

    }
}
