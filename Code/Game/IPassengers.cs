using System.Collections.Generic;
using Godot;

namespace tacticals.Code.Game;

public interface IPassengers
{
    bool BoardPassenger(TeamEntity entity);
    IReadOnlyList<TeamEntity> ExitPassengers();

    void UpdatePassengersPosition(Vector3 pos);

    /// <summary>How far from this entity's origin a soldier may stand and still get in.</summary>
    float BoardReach => 5f;
}