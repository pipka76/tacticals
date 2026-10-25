using Godot;

namespace tacticals.Code.Game;

/// <summary>
/// A tower put up on site out of a crate. Nothing here drives the build - whoever is doing the
/// work feeds progress in through <see cref="AddProgress"/> - the tower only owns how far along it
/// is and which of its two models that maps to.
/// </summary>
public partial class SmallTower : Node3D, IBuildable
{
    public const string SCENE_PATH = "res://Scenes/Structures/SmallTower.tscn";
    private const string IN_PROGRESS_SCENE_PATH = "res://Scenes/Structures/SmallTower_InProgress.tscn";
    private const string FINISHED_SCENE_PATH = "res://Scenes/Structures/SmallTower_Finished.tscn";

    /// <summary>Progress at which the scaffold gives way to the finished tower. 0.8 == 80%.</summary>
    public const float FINISHED_AT = 0.8f;

    private Node3D _inProgressStage;
    private Node3D _finishedStage;
    private float _buildProgress;
    /// <summary>Stage last applied, so feeding progress every frame only touches the nodes on a change.</summary>
    private bool? _shownFinished;

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

    /// <summary>The finished model is showing. The build itself still runs on to 100%.</summary>
    public bool IsFinished => _buildProgress >= FINISHED_AT;

    public bool IsComplete => _buildProgress >= 1f;

    public override void _Ready()
    {
        // SmallTower.tscn already carries both models as children; a tower instanced bare in code
        // has them loaded here instead, so either way the two stages exist.
        _inProgressStage = ResolveStage("SmallTower_InProgress", IN_PROGRESS_SCENE_PATH);
        _finishedStage = ResolveStage("SmallTower_Finished", FINISHED_SCENE_PATH);

        UpdateStage();
    }

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

    /// <summary>
    /// Exactly one of the two models is visible and solid, picked by how far along the build is.
    /// Hiding a stage does not stop its collider, so that has to be switched by hand - otherwise
    /// the finished tower's full collider stands from the first second of the build, and a
    /// scaffold would give the same cover as a finished tower.
    /// </summary>
    private void UpdateStage()
    {
        // BuildProgress can be assigned before the tower enters the tree - the crate sets it up
        // right after instancing - so until _Ready has run there is nothing to show yet.
        if (_inProgressStage == null || _finishedStage == null)
            return;

        var finished = IsFinished;
        if (_shownFinished == finished)
            return;
        _shownFinished = finished;

        _inProgressStage.Visible = !finished;
        _finishedStage.Visible = finished;
        SetStageSolid(_inProgressStage, !finished);
        SetStageSolid(_finishedStage, finished);
    }

    private static void SetStageSolid(Node node, bool solid)
    {
        if (node is CollisionShape3D shape)
            shape.Disabled = !solid;

        foreach (var child in node.GetChildren())
            SetStageSolid(child, solid);
    }
}
