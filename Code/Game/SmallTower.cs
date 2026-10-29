using Godot;

namespace tacticals.Code.Game;

/// <summary>
/// A tower put up on site out of a crate. Nothing here drives the build - whoever is doing the
/// work feeds progress in through <see cref="AddProgress"/> - the tower only owns how far along it
/// is and which of its two models that maps to.
///
/// Once complete, one soldier of any team can get in while it stands empty; the tower then belongs
/// to his team until he is out or dead. He stands in the cabin with his chest at the window slits
/// (front, left and right - the back is the ladder hatch), and the walls are real: he sees and
/// shoots only through the slits, and only a bullet that gets through one can reach him. The tower
/// itself is never a target and does not wear down.
/// </summary>
public partial class SmallTower : TeamEntity, IBuildable, IPassengers
{
    public const string SCENE_PATH = "res://Scenes/Structures/SmallTower.tscn";
    private const string IN_PROGRESS_SCENE_PATH = "res://Scenes/Structures/SmallTower_InProgress.tscn";
    private const string FINISHED_SCENE_PATH = "res://Scenes/Structures/SmallTower_Finished.tscn";
    private const string SELECTION_RING_PATH = "res://Assets/Game/SelectionRing.fbx";
    /// <summary>
    /// Animation in the finished model that swings the hatch shut from wide open. Played backwards
    /// and then forwards it opens and closes again, whenever someone gets in or out; at rest the
    /// tower holds its last frame, closed. A model without it just skips the animation.
    /// </summary>
    private const string HATCH_ANIMATION = "SmallTowerDoorAction";

    /// <summary>Progress at which the scaffold gives way to the finished tower. 0.8 == 80%.</summary>
    public const float FINISHED_AT = 0.8f;

    private const int CAPACITY = 1;
    /// <summary>
    /// Middle of the window slits above the tower's base, measured off the finished model's
    /// collider (they run 2.1 - 2.3).
    /// </summary>
    private const float WINDOW_HEIGHT = 2.2f;
    /// <summary>Same as the soldier's: everyone aims this far above a unit's feet.</summary>
    private const float AIM_HEIGHT = 1.2f;
    /// <summary>
    /// Which ways the slits face, in the frame the tower was placed in (forward is -Z, where the
    /// placement arrow points). The back has an opening too, but it is the hatch, not a firing slit.
    /// </summary>
    private static readonly Vector3[] WINDOWS = { Vector3.Forward, Vector3.Left, Vector3.Right };
    /// <summary>How far off a window's facing a target may be, either side.</summary>
    private const float WINDOW_HALF_ARC = 40f;
    /// <summary>
    /// How far towards a slit the occupant steps to look and shoot through it - and so where he
    /// can be hit. At 0.4 the slit itself would show him 45 degrees either side, so the
    /// <see cref="WINDOW_HALF_ARC"/> limit is what bounds his view, and he sees the ground from
    /// 4m out (from the middle of the cabin it was 26 degrees and 8m). Further in, the slits
    /// would overlap.
    /// </summary>
    private const float WINDOW_LEAN = 0.4f;
    /// <summary>Same as a soldier on foot - the cover is advantage enough.</summary>
    private const float AWARE_RADIUS = 50f;
    private const float AWARE_CHECK_INTERVAL = 0.2f;
    /// <summary>Same as a soldier on foot.</summary>
    private const float ATTACK_INTERVAL = 3.0f;
    private const float FIRST_SHOT_DELAY = 1.5f;

    private Node3D _inProgressStage;
    private Node3D _finishedStage;
    private float _buildProgress;
    /// <summary>Stage last applied, so feeding progress every frame only touches the nodes on a change.</summary>
    private bool? _shownFinished;

    private float _footRadius = 2f;
    private TeamEntity _enemyTarget;
    private double _awareT;
    private double _attackT;
    private bool _opened;
    private AnimationPlayer _hatch;

    public SmallTower()
    {
        _maxPassengersCapacity = CAPACITY;
    }

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

    public bool IsBlueprint { get; set; }

    /// <summary>From its edge rather than its middle, and past the cells it blocks - nobody can stand closer.</summary>
    float IPassengers.BoardReach => (BuildSite.FindFrom(this)?.WorkReach ?? _footRadius) + 1f;

    private TeamEntity Occupant => _passengers.Count > 0 ? _passengers[0] : null;

    /// <summary>
    /// The frame the tower was placed in. The scene root carries a turn of its own to line the
    /// model up, so the window facings and heights are given relative to this, not the root.
    /// </summary>
    private Transform3D PlacedFrame => GlobalTransform * Transform.AffineInverse();

    /// <summary>The middle of the cabin, level with the slits - where the occupant scans from.</summary>
    private Vector3 Eye => PlacedFrame * new Vector3(0f, WINDOW_HEIGHT, 0f);

    /// <summary>The occupant's eye and muzzle while leaning to the slit facing <paramref name="window"/>.</summary>
    private Vector3 WindowEye(Vector3 window) => PlacedFrame * (window * WINDOW_LEAN + Vector3.Up * WINDOW_HEIGHT);

    public override void _Ready()
    {
        // SmallTower.tscn already carries both models as children; a tower instanced bare in code
        // has them loaded here instead, so either way the two stages exist.
        _inProgressStage = ResolveStage("SmallTower_InProgress", IN_PROGRESS_SCENE_PATH);
        _finishedStage = ResolveStage("SmallTower_Finished", FINISHED_SCENE_PATH);

        UpdateStage();

        if (IsBlueprint)
        {
            SetProcess(false);
            return;
        }

        // No body of its own: the model's colliders are what stop bullets and sight lines, and
        // selection finds the tower through them.
        CollisionLayer = 0;
        CollisionMask = 0;
        AddToGroup(EntityGroup.GARRISONS);
    }

    public override void _Process(double delta)
    {
        if (!IsComplete)
            return;

        if (!_opened)
            OpenForGarrison();

        DropFallen();

        // The tower never moves, so the only thing it is ever ordered to do is let people out.
        var next = GetNextState();
        if (next.Item1 == TeamEntityStates.EXITING && next.Item2 is Vector2 whereTo)
            Unload(whereTo);

        if (Occupant == null)
        {
            _enemyTarget = null;
            return;
        }

        ReportDebug();
        HandleCombat(delta);
    }

    /// <summary>
    /// What the occupant sees through each slit. Cast from his eye in the middle of the cabin, so
    /// the slit's edges cut each window's arc down to the view it really gives.
    /// </summary>
    private void ReportDebug()
    {
        if (GameDebug.Current == null || !GameDebug.Current.IsEnabled)
            return;

        var space = GetWorld3D().DirectSpaceState;
        var frame = PlacedFrame.Basis.Orthonormalized();
        var exclude = new Godot.Collections.Array<Rid> { Occupant.GetRid() };
        foreach (var window in WINDOWS)
            GameDebug.Current.RegisterSight(space, WindowEye(window), frame * window, WINDOW_HALF_ARC * 2f, AWARE_RADIUS, exclude);
    }

    public void AddProgress(float amount)
    {
        BuildProgress = _buildProgress + amount;
    }

    /// <summary>Only a finished tower, and only soldiers. The team check is the base one: empty takes anyone.</summary>
    bool IPassengers.BoardPassenger(TeamEntity entity)
    {
        if (IsBlueprint || !IsComplete)
            return false;
        if (entity is not Soldier || entity.IsInState(TeamEntityStates.TERMINATED))
            return false;
        if (!BoardPassenger(entity))
            return false;

        // Hidden - his model does not fit the cabin - but his collider stays where he stands, so
        // a bullet through a slit finds him and one into the wall does not.
        StepTo(entity, Vector3.Forward);
        PlayHatch();
        return true;
    }

    /// <summary>
    /// Stands the occupant at the slit facing <paramref name="window"/>, turned to it, with his
    /// chest in the opening. He is hidden, but his collider is what bullets coming through the
    /// slit hit, so it has to be at the window he is using.
    /// </summary>
    private void StepTo(TeamEntity occupant, Vector3 window)
    {
        occupant.GlobalPosition = PlacedFrame * (window * WINDOW_LEAN + Vector3.Up * (WINDOW_HEIGHT - AIM_HEIGHT));

        // Same convention as the units: yaw 0 faces -Z.
        var facing = PlacedFrame.Basis.Orthonormalized() * window;
        occupant.GlobalRotation = new Vector3(0f, Mathf.Atan2(-facing.X, -facing.Z), 0f);
    }

    /// <summary>Hits on the walls. They stop the bullet and that is all.</summary>
    protected override void HitTaken(int pDamage, TeamEntity pShooter, Vector3 hitPos)
    {
        _damageTaken = 0;
    }

    /// <summary>A soldier killed inside leaves the tower free again, for either side to take.</summary>
    private void DropFallen()
    {
        var occupant = Occupant;
        if (occupant == null || (GodotObject.IsInstanceValid(occupant) && !occupant.IsInState(TeamEntityStates.TERMINATED)))
            return;

        _passengers.Clear();
        _teamMembership = TeamMembership.NEUTRAL;
    }

    /// <summary>Puts the occupant back on open ground on the <paramref name="whereTo"/> side of the tower.</summary>
    private void Unload(Vector2 whereTo)
    {
        var site = BuildSite.FindFrom(this);
        var center = new Vector2(GlobalPosition.X, GlobalPosition.Z);
        var toward = whereTo - center;
        if (toward.LengthSquared() < 0.0001f)
            toward = Vector2.Right;

        var passengers = ExitPassengers();
        if (passengers.Count > 0)
            PlayHatch();

        foreach (var p in passengers)
        {
            var from = center + toward;
            var spot = site?.WorkSpotFor(from) ?? from;

            p.GlobalPosition = new Vector3(spot.X, GlobalPosition.Y, spot.Y);
            p.SetVisible(true);
        }

        _enemyTarget = null;
    }

    private void HandleCombat(double delta)
    {
        _awareT -= delta;
        if (_awareT <= 0.0)
        {
            _awareT = AWARE_CHECK_INTERVAL;
            if (!IsValidTarget(_enemyTarget))
            {
                _enemyTarget = FindVisibleEnemy();
                if (_enemyTarget != null)
                {
                    _attackT = FIRST_SHOT_DELAY;
                    // Over to the slit he spotted it through - exposed there from now on.
                    var spotted = _enemyTarget.GlobalPosition + Vector3.Up * AIM_HEIGHT;
                    if (TrySightThroughWindow(_enemyTarget, spotted, out var spottedThrough))
                        StepTo(Occupant, spottedThrough);
                }
            }
        }

        if (_enemyTarget == null)
            return;

        _attackT -= delta;
        if (_attackT > 0.0)
            return;
        _attackT = ATTACK_INTERVAL;

        if (!IsValidTarget(_enemyTarget))
        {
            _enemyTarget = null;
            return;
        }

        var aim = _enemyTarget.GlobalPosition + Vector3.Up * AIM_HEIGHT;
        if (TrySightThroughWindow(_enemyTarget, aim, out var window))
        {
            StepTo(Occupant, window);
            FireOnTarget(Occupant, WindowEye(window), aim);
        }
    }

    private bool IsValidTarget(TeamEntity target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target) || target.IsInState(TeamEntityStates.TERMINATED))
            return false;
        if (target.IsMemberOf(TeamMembership.NEUTRAL) || target.IsMemberOf(_teamMembership))
            return false;

        var aim = target.GlobalPosition + Vector3.Up * AIM_HEIGHT;
        var eye = Eye;
        if (eye.DistanceTo(aim) > AWARE_RADIUS)
            return false;

        return TrySightThroughWindow(target, aim, out _);
    }

    /// <summary>
    /// Whether the occupant can see <paramref name="aim"/> through any slit whose arc it is in,
    /// standing at that slit. <paramref name="window"/> is the one he sees it through.
    /// </summary>
    private bool TrySightThroughWindow(Node3D target, Vector3 aim, out Vector3 window)
    {
        window = default;
        var frame = PlacedFrame.Basis.Orthonormalized();
        var toAim = aim - Eye;
        toAim.Y = 0f;
        if (toAim.LengthSquared() < 0.0001f)
            return false;
        toAim = toAim.Normalized();

        float minDot = Mathf.Cos(Mathf.DegToRad(WINDOW_HALF_ARC));
        foreach (var candidate in WINDOWS)
        {
            var facing = frame * candidate;
            facing.Y = 0f;
            if (facing.Normalized().Dot(toAim) < minDot)
                continue;

            if (!HasLineOfSight(WindowEye(candidate), target, aim))
                continue;

            window = candidate;
            return true;
        }

        return false;
    }

    /// <summary>Nearest enemy the occupant can see through one of the slits.</summary>
    private TeamEntity FindVisibleEnemy()
    {
        var space = GetWorld3D().DirectSpaceState;
        var shapeQuery = new PhysicsShapeQueryParameters3D
        {
            Shape = new SphereShape3D { Radius = AWARE_RADIUS },
            Transform = new Transform3D(Basis.Identity, Eye),
            CollisionMask = 0b0110,
            CollideWithAreas = true,
            CollideWithBodies = true
        };

        TeamEntity best = null;
        float bestD2 = float.PositiveInfinity;
        var eye = Eye;

        foreach (var h in space.IntersectShape(shapeQuery, 32))
        {
            if (!h.TryGetValue("collider", out var cv) || cv.AsGodotObject() is not Node hitNode)
                continue;

            TeamEntity entity = null;
            for (Node n = hitNode; n != null; n = n.GetParent())
                if (n is TeamEntity te) { entity = te; break; }

            if (entity == null || entity == this || entity == Occupant)
                continue;

            var d2 = eye.DistanceSquaredTo(entity.GlobalPosition);
            if (d2 >= bestD2 || !IsValidTarget(entity))
                continue;

            best = entity;
            bestD2 = d2;
        }

        return best;
    }

    /// <summary>
    /// From the occupant's eye with the walls left in: a target is only seen when the ray makes it
    /// out through a slit. Only the occupant himself is skipped.
    /// </summary>
    private bool HasLineOfSight(Vector3 eye, Node3D enemyRoot, Vector3 aim)
    {
        var ray = new PhysicsRayQueryParameters3D
        {
            From = eye,
            To = aim,
            CollideWithAreas = true,
            CollideWithBodies = true,
            CollisionMask = 0b1111,
            Exclude = new Godot.Collections.Array<Rid> { Occupant.GetRid() }
        };

        var res = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if (!res.TryGetValue("collider", out var cv) || cv.AsGodotObject() is not Node hitNode)
            return false;

        for (Node n = hitNode; n != null; n = n.GetParent())
            if (n == enemyRoot)
                return true;

        return false;
    }

    /// <summary>
    /// Fired by the occupant from where he stands, out through the slit the sight line found. A
    /// stray one may clip the frame, which stops it like any other wall hit.
    /// </summary>
    private void FireOnTarget(TeamEntity shooter, Vector3 muzzle, Vector3 target)
    {
        // He is a soldier like any other: same aim, same rifle.
        var dir = Projectile.Scatter(target - muzzle, Soldier.SHOT_SPREAD);

        Main3d.Current.Audio.Play3D("handgun_shot", muzzle);
        Main3d.Current.Projectiles.Spawn(new Projectile()
        {
            Shooter = shooter,
            Pos = muzzle,
            Vel = dir * Soldier.BULLET_SPEED,
            Mask = 0b111,
            ShooterRid = shooter.GetRid(),
            Life = 1,
            Damage = 10
        });
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

    /// <summary>First frame at 100%: a scaffold cannot be selected, so the ring only comes now.</summary>
    private void OpenForGarrison()
    {
        _opened = true;
        MeasureFootprint();
        CreateSelectionRing();

        var players = _finishedStage?.FindChildren("*", nameof(AnimationPlayer), true, false);
        if (players != null && players.Count > 0)
            _hatch = (AnimationPlayer)players[0];

        // The model's own pose has the hatch open; a tower nobody is using stands shut.
        if (_hatch != null && _hatch.HasAnimation(HATCH_ANIMATION))
        {
            _hatch.Play(HATCH_ANIMATION);
            _hatch.Seek(_hatch.GetAnimation(HATCH_ANIMATION).Length, true);
        }
    }

    private void PlayHatch()
    {
        if (_hatch == null || !_hatch.HasAnimation(HATCH_ANIMATION))
            return;

        // Open, then shut behind him. Restarts from closed even if it is still swinging.
        _hatch.ClearQueue();
        _hatch.PlayBackwards(HATCH_ANIMATION);
        _hatch.Queue(HATCH_ANIMATION);
    }

    /// <summary>Footprint radius of the finished model, in the tower's own space.</summary>
    private void MeasureFootprint()
    {
        if (_finishedStage == null)
            return;

        float radius = 0f;
        var toTower = GlobalTransform.AffineInverse();

        foreach (var node in _finishedStage.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            var mesh = (MeshInstance3D)node;
            var aabb = mesh.GetAabb();
            var toLocal = toTower * mesh.GlobalTransform;
            for (int i = 0; i < 8; i++)
            {
                var corner = toLocal * aabb.GetEndpoint(i);
                radius = Mathf.Max(radius, new Vector2(corner.X, corner.Z).Length());
            }
        }

        if (radius > 0f) _footRadius = radius;
    }

    private void CreateSelectionRing()
    {
        var scene = GD.Load<PackedScene>(SELECTION_RING_PATH);
        if (scene == null)
            return;

        var ring = scene.Instantiate<Node3D>();
        ring.Name = "SelectionRing";
        AddChild(ring);

        // Sized off its own mesh, so it hugs the tower whatever units the model came in.
        float ringRadius = 0f;
        var toRing = ring.GlobalTransform.AffineInverse();
        foreach (var node in ring.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            var mesh = (MeshInstance3D)node;
            var aabb = mesh.GetAabb();
            var toLocal = toRing * mesh.GlobalTransform;
            for (int i = 0; i < 8; i++)
            {
                var corner = toLocal * aabb.GetEndpoint(i);
                ringRadius = Mathf.Max(ringRadius, new Vector2(corner.X, corner.Z).Length());
            }
        }

        if (ringRadius > 0f)
            ring.Scale = Vector3.One * ((_footRadius + 0.5f) / ringRadius);
        ring.Visible = false;
        _selectorObject = ring;
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
