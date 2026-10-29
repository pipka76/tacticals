namespace tacticals.Code.Game;

/// <summary>
/// A structure that is put up over time. Builders only ever talk to this, so a new crate content
/// needs no changes in the soldier or player code - it just implements the interface.
/// </summary>
public interface IBuildable
{
    /// <summary>How far the build has got, 0..1. Setting 1 before the node enters the tree shows it finished.</summary>
    float BuildProgress { get; set; }

    /// <summary>True once the build has reached 100% and there is nothing left to do.</summary>
    bool IsComplete { get; }

    /// <summary>
    /// Set before the node enters the tree on the see-through copy shown while placing: it only
    /// shows the model, nothing in it may run or be found by the game.
    /// </summary>
    bool IsBlueprint { get; set; }

    /// <summary>Advances the build. Feed it (build rate * delta) from whoever is doing the work.</summary>
    void AddProgress(float amount);
}
