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

    /// <summary>
    /// Whether whoever gets in disappears from view. A vehicle or tower hides him; a crate only
    /// has him pulling it, and a gun has him standing at it, out in the open.
    /// </summary>
    bool HidesPassengers => true;
}