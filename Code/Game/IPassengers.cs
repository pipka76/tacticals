using System.Collections.Generic;
using Godot;

namespace tacticals.Code.Game;

public interface IPassengers
{
    bool BoardPassenger(TeamEntity entity);
    IReadOnlyList<TeamEntity> ExitPassengers();

    void UpdatePassengersPosition(Vector3 pos);
}