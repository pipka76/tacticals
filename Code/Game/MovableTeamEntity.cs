using Godot;
using tacticals.Code.Game;

public partial class MovableTeamEntity : TeamEntity
{
    private Vector2 _moveToCoords;

    public Vector3 MoveToCoordinates
    {
        get
        {
            return new Vector3(_moveToCoords.X, 0, _moveToCoords.Y);
        }
    }

    public void MoveTo(Vector2 coords)
    {
        SetNewState(TeamEntityStates.ONTHEWAY);
        _moveToCoords = coords;
        RotateTowards(new Vector3(coords.X, 0, coords.Y));
    }
}