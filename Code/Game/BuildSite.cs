using Godot;

namespace tacticals.Code.Game;

/// <summary>
/// The spot a crate's content is going to be built on. It lives through three phases:
///  - preview: the player is still choosing where and how it stands; it is just a blueprint
///    following the mouse (<see cref="Commit"/> ends it);
///  - planned: the blueprint stays on the ground while the crate is dragged over to it;
///  - unpacked: the crate has been turned into the real structure, which is this node's child, and
///    builders feed it progress until it is complete.
/// Everything generic about building lives here, so a new crate content only needs a scene and an
/// <see cref="IBuildable"/> on its root.
/// </summary>
public partial class BuildSite : Node3D
{
    /// <summary>How far past the structure's edge builders stand.</summary>
    private const float WORK_GAP = 1f;

    /// <summary>
    /// How close to the site the crate has to be before it can be unpacked, past the structure's
    /// edge. Generous on purpose - the crate trails its dragger on a rope, so it never gets as
    /// close as he does.
    /// </summary>
    private const float UNPACK_GAP = 5f;

    /// <summary>Step a work spot is pushed out by until it is off the structure's blocked cells.</summary>
    private const float WORK_SPOT_STEP = 0.5f;
    private const int WORK_SPOT_MAX_STEPS = 24;

    private const float BLUEPRINT_ALPHA = 0.35f;
    private static readonly Color BLUEPRINT_COLOR = new Color(0.35f, 0.75f, 1.00f);
    private static readonly Color BLOCKED_COLOR = new Color(1.00f, 0.25f, 0.20f);
    private static readonly Color PROGRESS_COLOR = new Color(1.00f, 0.90f, 0.35f);

    /// <summary>How high above the top of the finished model the progress bar floats.</summary>
    private const float PROGRESS_BAR_GAP = 1.5f;

    private Crate _crate;
    private CrateContent _content;
    private Node3D _blueprint;
    private StandardMaterial3D _blueprintMaterial;
    private Node3D _structure;
    private bool _committed;
    private ProgressBar3D _progressBar;
    /// <summary>Height of the finished model's top above the site's origin, measured with <see cref="Radius"/>.</summary>
    private float _top = 4f;
    /// <summary>The model's XZ bounds in site space, measured with <see cref="Radius"/>.</summary>
    private Rect2 _footprint = new Rect2(-2f, -2f, 4f, 4f);
    /// <summary>The structure's hold on the path field, valid while it stands.</summary>
    private FlowFieldManager.TerrainEdit _obstacle;
    /// <summary>Distance from the origin to the far corner of the blocked cells; 0 while nothing is blocked.</summary>
    private float _blockedReach;

    /// <summary>Horizontal reach of the structure from the site's origin, measured off its model.</summary>
    public float Radius { get; private set; } = 2f;

    /// <summary>Builders further than this from the site's origin walk back before working.</summary>
    public float WorkReach => Mathf.Max(Radius + WORK_GAP, _blockedReach + WORK_SPOT_STEP) + 1.5f;

    public IBuildable Buildable => _structure as IBuildable;

    public bool IsUnpacked => _structure != null;

    /// <summary>Placed for good - past the preview the player drags around.</summary>
    public bool IsCommitted => _committed;

    /// <summary>Content that is not an <see cref="IBuildable"/> has nothing to build - it is done once unpacked.</summary>
    public bool IsComplete => Buildable?.IsComplete ?? IsUnpacked;

    /// <summary>
    /// The plan fell through before anything was built: the crate is gone, someone planned it
    /// elsewhere, or nobody is left pulling it here.
    /// </summary>
    public bool IsAbandoned
    {
        get
        {
            if (!_committed || _structure != null)
                return false;
            if (!GodotObject.IsInstanceValid(_crate) || _crate.IsQueuedForDeletion())
                return true;
            if (_crate.Site != this)
                return true;
            return _crate.Dragger == null && !IsCrateInReach();
        }
    }

    /// <summary>Makes a preview site for <paramref name="crate"/>. Add it to the level, then move it with the mouse.</summary>
    public static BuildSite Preview(Crate crate)
    {
        return new BuildSite
        {
            Name = "BuildSite",
            _crate = crate,
            _content = crate.Content
        };
    }

    /// <summary>Walks up from a node (e.g. a raycast collider) to the build site it belongs to.</summary>
    public static BuildSite FindFrom(Node node)
    {
        for (var n = node; n != null; n = n.GetParent())
            if (n is BuildSite site)
                return site;
        return null;
    }

    /// <summary>Whether a ground point is inside the structure's footprint ring.</summary>
    public bool ContainsGroundPoint(Vector3 point)
    {
        var center = new Vector2(GlobalPosition.X, GlobalPosition.Z);
        return center.DistanceTo(new Vector2(point.X, point.Z)) <= Radius;
    }

    public override void _Ready()
    {
        AddToGroup(EntityGroup.BUILD_SITES);
        _blueprint = CreateBlueprint();
        if (_blueprint != null)
            MeasureExtent(_blueprint);
    }

    public override void _ExitTree()
    {
        ReleaseObstacle();
    }

    public override void _Process(double delta)
    {
        if (IsAbandoned)
        {
            QueueFree();
            return;
        }

        UpdateProgressBar();
    }

    /// <summary>
    /// Shown while the structure is going up, gone once it is complete. Made on first need, so a
    /// preview or a site still waiting for its crate carries none.
    /// </summary>
    private void UpdateProgressBar()
    {
        var buildable = Buildable;
        bool show = buildable != null && !buildable.IsComplete;

        if (!show)
        {
            if (_progressBar != null)
            {
                _progressBar.QueueFree();
                _progressBar = null;
            }
            return;
        }

        if (_progressBar == null)
        {
            float width = Mathf.Clamp(Radius * 1.5f, 2f, 8f);
            _progressBar = ProgressBar3D.Create(width, width * 0.14f, PROGRESS_COLOR);
            _progressBar.Position = new Vector3(0f, _top + PROGRESS_BAR_GAP, 0f);
            AddChild(_progressBar);
        }

        _progressBar.Value = buildable.BuildProgress;
    }

    /// <summary>Ends the preview: the site stays where it is and the crate is promised to it.</summary>
    public void Commit()
    {
        _committed = true;
        _crate.Site = this;
        SetBlueprintColor(BLUEPRINT_COLOR);
    }

    /// <summary>
    /// Whether the structure can stand here: its middle and the four points of its footprint
    /// must all be open ground.
    /// </summary>
    public bool IsPlaceable()
    {
        var pathField = Main3d.Current?.Map?.PathField;
        if (pathField == null || !pathField.IsInitialized)
            return true;

        var center = new Vector2(GlobalPosition.X, GlobalPosition.Z);
        var offsets = new[] { Vector2.Zero, Vector2.Right, Vector2.Left, Vector2.Up, Vector2.Down };
        foreach (var offset in offsets)
        {
            var cell = pathField.WorldToCell(center + offset * Radius);
            if (!pathField.IsPassable(cell, MovementDomain.Ground))
                return false;
        }

        return true;
    }

    /// <summary>Tints the preview red where it cannot be placed.</summary>
    public void ShowPlaceable(bool placeable)
    {
        SetBlueprintColor(placeable ? BLUEPRINT_COLOR : BLOCKED_COLOR);
    }

    /// <summary>
    /// Where a builder coming from <paramref name="fromXZ"/> should stand: just outside the
    /// structure, on his side of it, so a group fans out around it instead of queueing up.
    /// </summary>
    public Vector2 WorkSpotFor(Vector2 fromXZ)
    {
        var center = new Vector2(GlobalPosition.X, GlobalPosition.Z);
        var away = fromXZ - center;
        away = away.LengthSquared() < 0.0001f ? Vector2.Right : away.Normalized();
        var spot = center + away * (Radius + WORK_GAP);

        // The path grid is coarser than the structure, so the spot right by its wall can still be
        // in a blocked cell - nobody could walk there. Back off until it is open ground.
        var pathField = Main3d.Current?.Map?.PathField;
        if (pathField == null || !pathField.IsInitialized)
            return spot;

        for (int i = 0; i < WORK_SPOT_MAX_STEPS; i++)
        {
            if (pathField.IsPassable(pathField.WorldToCell(spot), MovementDomain.Ground))
                break;
            spot += away * WORK_SPOT_STEP;
        }

        return spot;
    }

    /// <summary>
    /// Turns the crate into the real structure once it has been brought close enough. Safe to
    /// call every frame from every builder: true once the structure exists.
    /// </summary>
    public bool TryUnpack()
    {
        if (_structure != null)
            return true;
        if (!_committed || !GodotObject.IsInstanceValid(_crate) || !IsCrateInReach())
            return false;

        _structure = _crate.BuildContent(this);
        _crate = null;
        if (_structure == null)
            return false;

        _blueprint?.QueueFree();
        _blueprint = null;
        BlockGround();
        return true;
    }

    /// <summary>
    /// Closes the structure's footprint to ground units, scaffold included - it stands in the way
    /// from the moment the crate is unpacked. Only cells whose middle lies under the structure are
    /// taken, so a small tower does not wall off every cell its edge brushes, but the cell under
    /// its middle always is. Units already standing there are let walk out.
    /// </summary>
    private void BlockGround()
    {
        var pathField = Main3d.Current?.Map?.PathField;
        if (pathField == null || !pathField.IsInitialized || _obstacle.IsValid)
            return;

        // World XZ bounds of the footprint, turned with the site.
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        var corners = new[]
        {
            _footprint.Position, _footprint.End,
            new Vector2(_footprint.Position.X, _footprint.End.Y),
            new Vector2(_footprint.End.X, _footprint.Position.Y)
        };
        foreach (var corner in corners)
        {
            var world = GlobalTransform * new Vector3(corner.X, 0f, corner.Y);
            min = min.Min(new Vector2(world.X, world.Z));
            max = max.Max(new Vector2(world.X, world.Z));
        }

        var halfCell = Vector2.One * (pathField.CellSize * 0.5f);
        var minCell = pathField.WorldToCell(min + halfCell);
        var maxCell = pathField.WorldToCell(max - halfCell);
        var centerCell = pathField.WorldToCell(new Vector2(GlobalPosition.X, GlobalPosition.Z));
        if (minCell.X > maxCell.X)
            minCell.X = maxCell.X = centerCell.X;
        if (minCell.Y > maxCell.Y)
            minCell.Y = maxCell.Y = centerCell.Y;

        // Cell centres map back to exactly these cells.
        var from = pathField.CellToWorldCenter(minCell);
        var to = pathField.CellToWorldCenter(maxCell);
        _obstacle = pathField.AddObstacle(new Rect2(from, to - from), MovementDomain.Ground);

        var center = new Vector2(GlobalPosition.X, GlobalPosition.Z);
        var low = from - halfCell;
        var high = to + halfCell;
        _blockedReach = Mathf.Max(
            Mathf.Max(center.DistanceTo(low), center.DistanceTo(high)),
            Mathf.Max(center.DistanceTo(new Vector2(low.X, high.Y)), center.DistanceTo(new Vector2(high.X, low.Y))));
    }

    private void ReleaseObstacle()
    {
        if (!_obstacle.IsValid)
            return;

        Main3d.Current?.Map?.PathField?.RemoveEdit(_obstacle);
        _obstacle = default;
        _blockedReach = 0f;
    }

    private bool IsCrateInReach()
    {
        var crate = new Vector2(_crate.GlobalPosition.X, _crate.GlobalPosition.Z);
        var center = new Vector2(GlobalPosition.X, GlobalPosition.Z);
        return crate.DistanceTo(center) <= Radius + UNPACK_GAP;
    }

    /// <summary>
    /// The finished structure, see-through and without collisions - the collisions would
    /// otherwise catch the very mouse ray that is placing it, and block units walking past a
    /// building that does not exist yet.
    /// </summary>
    private Node3D CreateBlueprint()
    {
        var scene = GD.Load<PackedScene>(Crate.ContentScenePath(_content));
        if (scene == null)
            return null;

        var blueprint = scene.Instantiate<Node3D>();
        blueprint.Name = "Blueprint";
        // Before AddChild, so the structure comes up showing its finished model.
        if (blueprint is IBuildable buildable)
        {
            buildable.BuildProgress = 1f;
            buildable.IsBlueprint = true;
        }
        AddChild(blueprint);

        _blueprintMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            DisableReceiveShadows = true
        };
        SetBlueprintColor(BLUEPRINT_COLOR);

        StripToBlueprint(blueprint);
        return blueprint;
    }

    private void StripToBlueprint(Node node)
    {
        switch (node)
        {
            case CollisionObject3D body:
                body.CollisionLayer = 0;
                body.CollisionMask = 0;
                break;
            case CollisionShape3D shape:
                shape.Disabled = true;
                break;
            case MeshInstance3D mesh:
                mesh.MaterialOverride = _blueprintMaterial;
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                break;
        }

        foreach (var child in node.GetChildren())
            StripToBlueprint(child);
    }

    private void SetBlueprintColor(Color color)
    {
        if (_blueprintMaterial != null)
            _blueprintMaterial.AlbedoColor = new Color(color, BLUEPRINT_ALPHA);
    }

    /// <summary>
    /// Sets <see cref="Radius"/> to the furthest any mesh of the structure reaches from the site's
    /// origin in XZ, <see cref="_footprint"/> to its XZ bounds and <see cref="_top"/> to its highest point.
    /// </summary>
    private void MeasureExtent(Node3D root)
    {
        float radius = 0f;
        float top = 0f;
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        var toSite = GlobalTransform.AffineInverse();

        foreach (var node in root.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            var mesh = (MeshInstance3D)node;
            var aabb = mesh.GetAabb();
            var toLocal = toSite * mesh.GlobalTransform;
            for (int i = 0; i < 8; i++)
            {
                var corner = toLocal * aabb.GetEndpoint(i);
                radius = Mathf.Max(radius, new Vector2(corner.X, corner.Z).Length());
                top = Mathf.Max(top, corner.Y);
                min = min.Min(new Vector2(corner.X, corner.Z));
                max = max.Max(new Vector2(corner.X, corner.Z));
            }
        }

        if (min.X <= max.X)
            _footprint = new Rect2(min, max - min);

        Radius = radius > 0f ? radius : 2f;
        _top = top > 0f ? top : 4f;
    }
}
