using Godot;

namespace tacticals.Code.Game;

/// <summary>
/// A tower put up on site out of a crate. Nothing here drives the build - whoever is doing the
/// work feeds progress in through <see cref="AddProgress"/> - the tower only owns how far along it
/// is and which of its two models that maps to.
/// </summary>
public partial class SmallTower : Node3D
{
    public const string SCENE_PATH = "res://Scenes/Structures/SmallTower.tscn";
    private const string IN_PROGRESS_SCENE_PATH = "res://Scenes/Structures/SmallTower_InProgress.tscn";
    private const string FINISHED_SCENE_PATH = "res://Scenes/Structures/SmallTower_Finished.tscn";

    /// <summary>Progress at which the scaffold gives way to the finished tower. 0.8 == 80%.</summary>
    public const float FINISHED_AT = 0.8f;

    private Node3D _inProgressStage;
    private Node3D _finishedStage;
    private float _buildProgress;

    /// <summary>
    /// How far the build has got, 0..1. Assigning it swaps the model as soon as it crosses
    /// <see cref="FINISHED_AT"/>, so callers never have to touch the visuals themselves.
    /// </summary>
    public float BuildProgress
    {
        get => _buildProgress;
        set
        {
            _buildProgress = Mathf.Clamp(value, 0f, 1f);
            UpdateStage();
        }
    }

    public bool IsFinished => _buildProgress >= FINISHED_AT;

    public override void _Ready()
    {
        // SmallTower.tscn already carries both models as children; a tower instanced bare in code
        // has them loaded here instead, so either way the two stages exist.
        _inProgressStage = ResolveStage("SmallTower_InProgress", IN_PROGRESS_SCENE_PATH);
        _finishedStage = ResolveStage("SmallTower_Finished", FINISHED_SCENE_PATH);

        UpdateStage();
    }

    /// <summary>Advances the build. Feed it (build rate * delta) from whatever is doing the work.</summary>
    public void AddProgress(float amount)
    {
        BuildProgress = _buildProgress + amount;
    }

    private Node3D ResolveStage(string nodeName, string scenePath)
    {
        var stage = GetNodeOrNull<Node3D>(nodeName);
        if (stage != null)
            return stage;

        var scene = GD.Load<PackedScene>(scenePath);
        if (scene == null)
            return null;

        stage = scene.Instantiate<Node3D>();
        stage.Name = nodeName;
        AddChild(stage);
        return stage;
    }

    /// <summary>Exactly one of the two models is visible, picked by how far along the build is.</summary>
    private void UpdateStage()
    {
        // BuildProgress can be assigned before the tower enters the tree - the crate sets it up
        // right after instancing - so until _Ready has run there is nothing to show yet.
        if (_inProgressStage == null || _finishedStage == null)
            return;

        var finished = IsFinished;
        _inProgressStage.Visible = !finished;
        _finishedStage.Visible = finished;
    }
}
