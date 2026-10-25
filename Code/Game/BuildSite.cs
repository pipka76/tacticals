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

    private const float BLUEPRINT_ALPHA = 0.35f;
    private static readonly Color BLUEPRINT_COLOR = new Color(0.35f, 0.75f, 1.00f);
    private static readonly Color BLOCKED_COLOR = new Color(1.00f, 0.25f, 0.20f);

    private Crate _crate;
    private CrateContent _content;
    private Node3D _blueprint;
    private StandardMaterial3D _blueprintMaterial;
    private Node3D _structure;
    private bool _committed;

    /// <summary>Horizontal reach of the structure from the site's origin, measured off its model.</summary>
    public float Radius { get; private set; } = 2f;

    /// <summary>Builders further than this from the site's origin walk back before working.</summary>
    public float WorkReach => Radius + WORK_GAP + 1.5f;

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
            Radius = MeasureRadius(_blueprint);
    }

    public override void _Process(double delta)
    {
        if (IsAbandoned)
            QueueFree();
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
        return center + away * (Radius + WORK_GAP);
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
        return true;
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
            buildable.BuildProgress = 1f;
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

    /// <summary>Furthest any mesh of the structure reaches from the site's origin, in XZ.</summary>
    private float MeasureRadius(Node3D root)
    {
        float radius = 0f;
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
            }
        }

        return radius > 0f ? radius : 2f;
    }
}
