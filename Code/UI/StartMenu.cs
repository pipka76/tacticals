using Godot;
using System;

public partial class StartMenu : Control
{
    private const int ARMY_GRID_ROWS = 3;
    private const int ARMY_GRID_COLS = 10;
    
	TextureButton _classicModeButton;
    TextureButton _trainModeButton;
    TextureButton _hamButton;
    HBoxContainer _unitContainer;
    HBoxContainer _popupMenu;
    private UnitPlacement[][] _army;
    private Panel _gridPanel;
    private UnitType _selectedUnit;
    private int _unitId;
    private bool _forceRefresh = false;
    
    private enum UnitType
    {
        NONE = 0,
        SOLDIER = 0x0101,
        TANK = 0x0204,
        MOTORCYCLE = 0x0302,
        TRUCK = 0x0404,
        HELICOPTER = 0x0502,
        ARTILERY = 0x0604,
        TRASH = 0x0700
    }

    private record UnitPlacement
    {
        public UnitType Unit;
        public int Id;
    }

    public override void _Ready()
	{
		_classicModeButton = GetNode<TextureButton>("MarginContainer/VBoxContainer/TopMarginContainer/CenterContainer/TopMenu/HBoxContainer/btnClassicMode");
        _trainModeButton = GetNode<TextureButton>("MarginContainer/VBoxContainer/TopMarginContainer/CenterContainer/TopMenu/HBoxContainer/btnTrainDefence");
        _unitContainer = GetNode<HBoxContainer>("MarginContainer/VBoxContainer/BottomMarginContainer/UnitCatalog");
        _gridPanel = GetNode<Panel>("MarginContainer/VBoxContainer/MiddleMarginContainer/MarginContainer/Panel");
        _popupMenu = GetNode<HBoxContainer>("MarginContainer/PopupMenu");
        _hamButton = GetNode<TextureButton>("MarginContainer/VBoxContainer/TopMarginContainer/Hamburger");

        ArmyGridInit();
    }

    private void ArmyGridInit()
    {
        _selectedUnit = UnitType.NONE;
        _army = new UnitPlacement[ARMY_GRID_ROWS][];
        for (int r = 0; r < ARMY_GRID_ROWS; r++)
        {
            _army[r] = new UnitPlacement[ARMY_GRID_COLS];
            for (int c = 0; c < ARMY_GRID_COLS; c++)
            {
                _army[r][c] = new UnitPlacement()
                {
                    Unit = UnitType.NONE,
                    Id = 0
                };
            }
        }
    }

    public override void _Process(double delta)
	{
        if (_forceRefresh)
        {
            DrawArmy();
            _forceRefresh = false;
        }
    }

    private void OnResized()
    {
        _forceRefresh = true;
    }

    private void DrawArmy()
    {
        // Clear existing grid children
        foreach (Node child in _gridPanel.GetChildren())
        {
            _gridPanel.RemoveChild(child);
            child.QueueFree();
        }

        var colW = _gridPanel.Size.X / ARMY_GRID_COLS;
        var rowH = _gridPanel.Size.Y / ARMY_GRID_ROWS;

        for (int r = 0; r < ARMY_GRID_ROWS; r++)
        {
            for (int c = 0; c < ARMY_GRID_COLS; c++)
            {
                var placement = _army[r][c];
                if (placement.Unit == UnitType.NONE)
                    continue;

                string texturePath;
                switch (placement.Unit)
                {
                    case UnitType.SOLDIER:
                        texturePath = "res://Assets/UI/SoldierUnit.png";
                        break;
                    case UnitType.TANK:
                        texturePath = "res://Assets/UI/TankUnit.png";
                        break;
                    case UnitType.MOTORCYCLE:
                        texturePath = "res://Assets/UI/MotorcycleUnit.png";
                        break;
                    case UnitType.TRUCK:
                        texturePath = "res://Assets/UI/TruckUnit.png";
                        break;
                    case UnitType.HELICOPTER:
                        texturePath = "res://Assets/UI/HelicopterUnit.png";
                        break;
                    case UnitType.ARTILERY:
                        texturePath = "res://Assets/UI/ArtileryUnit.png";
                        break;
                    default:
                        continue;
                }

                var texture = GD.Load<Texture2D>(texturePath);
                if (texture == null)
                    continue;

                var unitRect = new TextureRect
                {
                    Texture = texture,
                    Position = new Vector2(c * colW, r * rowH),
                    Size = new Vector2(colW, rowH),
                    StretchMode = TextureRect.StretchModeEnum.Scale
                };
                _gridPanel.AddChild(unitRect);
            }
        }
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
        //GD.Print($"OnUnitSelected: {toggledOn} {unitId}");
        if (toggledOn)
        {
            _selectedUnit = ConvertToUnitType(unitId);
            UnselectOtherUnits(unitId);
        }
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

    private void OnGridPanelInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            Vector2 localPos = _gridPanel.GetLocalMousePosition();
            int col = (int)(localPos.X / _gridPanel.Size.X * ARMY_GRID_COLS);
            int row = (int)(localPos.Y / _gridPanel.Size.Y * ARMY_GRID_ROWS);
            row = Mathf.Clamp(row, 0, ARMY_GRID_ROWS - 1);
            col = Mathf.Clamp(col, 0, ARMY_GRID_COLS - 1);
            ProcessGridClick(row, col);
        }
    }

    private bool IsEnoughSpaceForUnit(Vector2I coords, UnitType uType)
    {
        if (uType == UnitType.TRASH)
            return true;
        
        if (_army[coords.Y][coords.X].Unit != UnitType.NONE)
            return false;

        int tileSize = (int)uType & 0xFF;
        switch (tileSize)
        {
            case 1:
                return true;
            case 2:
                if ((coords.X + 1) >= ARMY_GRID_COLS) return false;
                return (_army[coords.Y][coords.X + 1].Unit == UnitType.NONE);
            case 4:
                if ((coords.X + 1) >= ARMY_GRID_COLS) return false;
                if ((coords.Y + 1) >= ARMY_GRID_ROWS) return false;
                return (_army[coords.Y][coords.X + 1].Unit == UnitType.NONE) && (_army[coords.Y + 1][coords.X].Unit == UnitType.NONE) && (_army[coords.Y + 1][coords.X + 1].Unit == UnitType.NONE);
            default: return false;
        }
    }

    private void ProcessGridClick(int row, int col)
    {
        GD.Print($"Grid click at row {row}, col {col}");

        var coords = new Vector2I(col, row);
        if (IsEnoughSpaceForUnit(coords, _selectedUnit))
        {
            PlaceUnit(coords, _selectedUnit);
        }
    }

    private void PlaceUnit(Vector2I coords, UnitType uType)
    {
        if (uType == UnitType.TRASH)
        {
            int delId = _army[coords.Y][coords.X].Id;
            for (int r = 0; r < ARMY_GRID_ROWS; r++)
            {
                for (int c = 0; c < ARMY_GRID_COLS; c++)
                {
                    if (_army[r][c].Id == delId)
                    {
                        _army[r][c].Unit = UnitType.NONE; 
                        _army[r][c].Id = 0;
                    }
                }
            }

            _forceRefresh = true;
            return;
        }

        _unitId++;
        int tileSize = (int)uType & 0xFF;
        switch (tileSize)
        {
            case 1:
                _army[coords.Y][coords.X].Unit = uType; 
                _army[coords.Y][coords.X].Id = _unitId;
                break;
            case 2:
                _army[coords.Y][coords.X].Unit = uType; 
                _army[coords.Y][coords.X].Id = _unitId;
                _army[coords.Y][coords.X+1].Unit = uType; 
                _army[coords.Y][coords.X+1].Id = _unitId;
                break;
            case 4:
                _army[coords.Y][coords.X].Unit = uType; 
                _army[coords.Y][coords.X].Id = _unitId;
                _army[coords.Y][coords.X+1].Unit = uType; 
                _army[coords.Y][coords.X+1].Id = _unitId;
                _army[coords.Y+1][coords.X+1].Unit = uType; 
                _army[coords.Y+1][coords.X+1].Id = _unitId;
                _army[coords.Y+1][coords.X].Unit = uType; 
                _army[coords.Y+1][coords.X].Id = _unitId;
                break;
        }

        _forceRefresh = true;
    }

    private UnitType ConvertToUnitType(string unitId)
    {
        if (Enum.TryParse<UnitType>(unitId, out var type))
        {
            return type;
        }
        GD.PrintErr($"Unknown unit type: {unitId}");
        return UnitType.NONE;
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

    private void OnOptionsPressed()
    {
        
    }
}
