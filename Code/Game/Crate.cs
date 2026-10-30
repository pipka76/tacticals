using System;
using System.Collections.Generic;
using Godot;

namespace tacticals.Code.Game;

public partial class Crate : TeamEntity, IPassengers
{
    /// <summary>Rope length - how far behind the soldier the crate settles once it is being pulled.</summary>
    private const float DRAG_DISTANCE = 1.8f;

    /// <summary>How fast the crate catches up with the rope. Higher = stiffer rope, less swing.</summary>
    private const float DRAG_STIFFNESS = 10f;

    /// <summary>How fast the crate turns onto the drag direction / the slope under it.</summary>
    private const float TURN_STIFFNESS = 6f;

    private TeamEntity _owner;

    /// <summary>
    /// What this crate builds into. Exported so it can be picked per instance in the editor and
    /// set on the crate when it is spawned.
    /// </summary>
    [Export]
    public CrateContent Content { get; set; } = CrateContent.SMALL_TOWER;

    /// <summary>The soldier pulling the crate, or null when it lies on its own.</summary>
    public TeamEntity Dragger => _owner;

    /// <summary>
    /// Where the player has planned to build this crate's content. Planning it again replaces the
    /// old site, which notices and removes itself.
    /// </summary>
    public BuildSite Site { get; set; }

    public override void _Ready()
    {
        _owner = null;
        AddToGroup(EntityGroup.GROUND_UNIT);
    }
    
    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if (_owner == null)
            return;

        HandleMovement(delta);
    }

    /// <summary>
    /// Drags the crate behind <see cref="_owner"/>. The soldier is never put into BOARDED, it walks
    /// under its own steam - the crate just chases it, so all the movement logic lives here.
    /// </summary>
    private void HandleMovement(double delta)
    {
        // The soldier can be freed or killed while still holding the crate; drop it rather than
        // trailing a corpse around.
        if (!GodotObject.IsInstanceValid(_owner) || _owner.IsInState(TeamEntityStates.TERMINATED))
        {
            _owner = null;
            return;
        }

        var ownerFlat = new Vector3(_owner.GlobalPosition.X, 0, _owner.GlobalPosition.Z);
        var crateFlat = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);

        var toOwner = ownerFlat - crateFlat;
        var dist = toOwner.Length();
        if (dist < 0.0001f)
            return;                          // standing on top of us - no drag direction to work with

        var dragDirection = toOwner / dist;

        // Rope constraint: nothing happens until the soldier walks further out than the rope is
        // long. That slack is what makes the crate trail behind instead of gliding along at a
        // fixed offset, and it lets the crate swing in behind the soldier when he turns.
        if (dist > DRAG_DISTANCE)
        {
            var target = ownerFlat - dragDirection * DRAG_DISTANCE;
            // Framerate independent easing, same form as the tank hull uses for its slope match.
            crateFlat = crateFlat.Lerp(target, (float)(1.0 - Math.Exp(-DRAG_STIFFNESS * delta)));
        }

        // Nothing resolves collisions for us - entities write their transform directly - so the
        // terrain raycast is what keeps the crate on the ground. One frame stale, like StepTowards.
        if (!RaycastToTerrain(out var gnd, out var n))
            return;

        // Face the pull and lie over the slope in one go: -Z points at the soldier, Y at the normal.
        var rotation = RotateMatchPlane(dragDirection, n.Normalized(), (float)(1.0 - Math.Exp(-TURN_STIFFNESS * delta)));

        GlobalTransform = new Transform3D(new Basis(rotation), new Vector3(crateFlat.X, gnd.Y, crateFlat.Z));
    }

    /// <summary>The scene a given content type unpacks into.</summary>
    public static string ContentScenePath(CrateContent content)
    {
        return content switch
        {
            CrateContent.SMALL_TOWER => SmallTower.SCENE_PATH,
            CrateContent.BROWNING_TURRET => BrowningTurret.SCENE_PATH,
            _ => null
        };
    }

    /// <summary>
    /// Unpacks the crate into <paramref name="site"/>: the structure it carries goes in as the
    /// site's child, so it stands exactly where and how the player placed it, at zero build
    /// progress. Lets go of whoever was dragging it and takes the crate out of the world.
    /// Returns the new structure, or null if the content has no scene behind it.
    /// </summary>
    public Node3D BuildContent(Node3D site)
    {
        var scene = GD.Load<PackedScene>(ContentScenePath(Content));
        if (scene == null)
            return null;

        var structure = scene.Instantiate<Node3D>();
        site.AddChild(structure);

        ExitPassengers();
        QueueFree();

        return structure;
    }

    public bool HidesPassengers => false;

    public bool BoardPassenger(TeamEntity entity)
    {
        if (_owner != null)
            return false;

        if (entity is not Soldier soldier)
            return false;
        
        _owner = soldier;
        return true;
    }
    
    public IReadOnlyList<TeamEntity> ExitPassengers()
    {
        if (_owner == null)
            return new List<TeamEntity>();
        
        var result = new List<TeamEntity>() { _owner };
        _owner = null;
        return result;
    }

    public void UpdatePassengersPosition(Vector3 pos)
    {
    }
}