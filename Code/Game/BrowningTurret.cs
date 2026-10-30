using Godot;

namespace tacticals.Code.Game;

/// <summary>
/// A heavy machine gun on a stand, put up on site out of a crate like <see cref="SmallTower"/>.
/// Nothing here drives the build - whoever is doing the work feeds progress in through
/// <see cref="AddProgress"/> - the turret only owns how far along it is and which parts that shows.
///
/// The stand is the half-built piece and stays put; at <see cref="FINISHED_AT"/> the gun is
/// mounted on it, bullets and all. The gun sits under its own pivot and traverses on the stand.
///
/// Once complete, one soldier of any team can man it while it stands empty; the turret then
/// belongs to his team until he is off it or dead. Unlike in the tower he stays in view, standing
/// behind the gun at its gunner spot, with nothing around him but the gun shield.
///
/// Manned, it fights on its own. The gunner only spots what is within <see cref="SPOT_HALF_ARC"/>
/// of where the gun points, but the gun goes all the way round, at <see cref="TURN_SPEED"/>, to
/// follow what it has spotted. Lined up, it fires a burst of <see cref="BURST_ROUNDS"/> that
/// always runs out: a target that drops mid-burst is swapped for the next one in view and the gun
/// swings onto it still firing - or keeps hosing where it points if there is none. Then it cools
/// down for <see cref="COOLDOWN"/> seconds. Ammo is unlimited.
///
/// The stand and gun are too thin to click reliably, so the scene carries a PickBody: a cylinder
/// over the whole turret on the click-only Picking layer, which nothing but the player's mouse
/// rays look at.
/// </summary>
public partial class BrowningTurret : TeamEntity, IBuildable, IPassengers
{
    public const string SCENE_PATH = "res://Scenes/Structures/BrowningTurret.tscn";

    /// <summary>Progress at which the gun goes onto the stand. 0.8 == 80%, same as the tower.</summary>
    public const float FINISHED_AT = 0.8f;

    private const int CAPACITY = 1;

    private const float RANGE = 80f;
    /// <summary>How far either side of the barrel the gunner spots new targets, in degrees.</summary>
    private const float SPOT_HALF_ARC = 60f;
    private const float AWARE_CHECK_INTERVAL = 0.2f;
    /// <summary>Traverse speed in degrees per second. A target behind it takes two seconds to face.</summary>
    private const float TURN_SPEED = 90f;
    /// <summary>How close to the target the barrel has to point, in degrees, before a burst opens.</summary>
    private const float FIRE_ALIGN = 5f;
    private const int BURST_ROUNDS = 15;
    /// <summary>Seconds between rounds: 15 go in about two seconds. No faster than the belt feed animation.</summary>
    private const float ROUND_INTERVAL = 0.15f;
    /// <summary>Seconds after a burst before the next one may open.</summary>
    private const float COOLDOWN = 10f;
    /// <summary>Wider than a rifle's - a burst walks around the target.</summary>
    private const float SHOT_SPREAD = 2f;
    private const int DAMAGE = 20;
    /// <summary>Same as everyone's: aim this far above a unit's feet.</summary>
    private const float AIM_HEIGHT = 1.2f;
    /// <summary>Placeholder until the gun has a sound of its own - see the library entry.</summary>
    private const string SHOT_SOUND = "mg_shot";
    private const string FEED_ANIMATION = "Shooting";
    private const string EJECT_ANIMATION = "BulletEmptyAction";

    /// <summary>Pivot over the stand's axis carrying the gun and its ammo belt - what will turn to aim.</summary>
    private Node3D _gun;
    /// <summary>Where the gunner stands, under the gun's pivot so he goes round with it.</summary>
    private Node3D _gunnerSpot;
    private Node3D _muzzle;
    /// <summary>Pulls the belt in, one round per shot.</summary>
    private AnimationPlayer _feed;
    /// <summary>Throws the spent case out, one per shot.</summary>
    private AnimationPlayer _ejector;
    /// <summary>The turret's own colliders - sight lines from the muzzle look through them.</summary>
    private readonly Godot.Collections.Array<Rid> _selfRids = new();
    private float _buildProgress;
    /// <summary>Stage last applied, so feeding progress every frame only touches the nodes on a change.</summary>
    private bool? _shownFinished;

    private float _footRadius = 1.5f;
    private bool _opened;

    private TeamEntity _target;
    private double _awareT;
    /// <summary>Rounds still to go in the burst being fired; 0 when not firing.</summary>
    private int _roundsLeft;
    private double _roundT;
    private double _cooldownT;

    public BrowningTurret()
    {
        _maxPassengersCapacity = CAPACITY;
    }

    /// <summary>
    /// How far the build has got, 0..1. Assigning it mounts the gun as soon as it crosses
    /// <see cref="FINISHED_AT"/>. Exported so a turret placed by hand in a level can start built.
    /// </summary>
    [Export(PropertyHint.Range, "0,1")]
    public float BuildProgress
    {
        get => _buildProgress;
        set
        {
            _buildProgress = Mathf.Clamp(value, 0f, 1f);
            UpdateStage();
        }
    }

    /// <summary>The gun is showing. The build itself still runs on to 100%.</summary>
    public bool IsFinished => _buildProgress >= FINISHED_AT;

    public bool IsComplete => _buildProgress >= 1f;

    public bool IsBlueprint { get; set; }

    /// <summary>From its edge rather than its middle, and past the cells it blocks - nobody can stand closer.</summary>
    float IPassengers.BoardReach => (BuildSite.FindFrom(this)?.WorkReach ?? _footRadius) + 1f;

    /// <summary>The gunner stands out in the open behind the gun.</summary>
    bool IPassengers.HidesPassengers => false;

    private TeamEntity Gunner => _passengers.Count > 0 ? _passengers[0] : null;

    public override void _Ready()
    {
        _gun = GetNodeOrNull<Node3D>("Gun");
        _gunnerSpot = GetNodeOrNull<Node3D>("Gun/GunnerSpot");
        _muzzle = GetNodeOrNull<Node3D>("Gun/Muzzle");
        _feed = FindAnimationPlayer("Gun/TurretBulletsIn");
        _ejector = FindAnimationPlayer("Gun/TurretBulletsOut");

        UpdateStage();

        if (IsBlueprint)
        {
            SetProcess(false);
            return;
        }

        // No body of its own: the model's colliders are what stop bullets and sight lines, and
        // selection finds the turret through them.
        CollisionLayer = 0;
        CollisionMask = 0;
        AddToGroup(EntityGroup.GARRISONS);
    }

    public override void _Process(double delta)
    {
        if (!IsComplete)
            return;

        if (!_opened)
            OpenForGunner();

        DropFallen();

        // The turret never moves, so the only thing it is ever ordered to do is let people out.
        var next = GetNextState();
        if (next.Item1 == TeamEntityStates.EXITING && next.Item2 is Vector2 whereTo)
            Unload(whereTo);

        _cooldownT -= delta;

        if (Gunner == null)
        {
            CeaseFire();
            return;
        }

        StandAtGun(Gunner);
        ReportDebug();
        HandleGunnery(delta);
    }

    public void AddProgress(float amount)
    {
        BuildProgress = _buildProgress + amount;
    }

    /// <summary>Only a finished turret, and only soldiers. The team check is the base one: empty takes anyone.</summary>
    bool IPassengers.BoardPassenger(TeamEntity entity)
    {
        if (IsBlueprint || !IsComplete)
            return false;
        if (entity is not Soldier || entity.IsInState(TeamEntityStates.TERMINATED))
            return false;
        if (!BoardPassenger(entity))
            return false;

        StandAtGun(entity);
        return true;
    }

    /// <summary>
    /// Puts the gunner at the gunner spot, facing down the barrel. Every frame while he is on it,
    /// so he keeps behind the gun once it traverses.
    /// </summary>
    private void StandAtGun(TeamEntity gunner)
    {
        var spot = _gunnerSpot ?? _gun ?? this;
        gunner.GlobalPosition = spot.GlobalPosition;

        // Same convention as the units: yaw 0 faces -Z, and so does the barrel.
        var facing = (_gun ?? this).GlobalBasis.Orthonormalized() * Vector3.Forward;
        gunner.GlobalRotation = new Vector3(0f, Mathf.Atan2(-facing.X, -facing.Z), 0f);
    }

    private Vector3 Muzzle => (_muzzle ?? _gun ?? this).GlobalPosition;

    /// <summary>Where the barrel points, flattened.</summary>
    private Vector3 GunForward
    {
        get
        {
            var forward = (_gun ?? this).GlobalBasis * Vector3.Forward;
            forward.Y = 0f;
            return forward.LengthSquared() < 0.0001f ? Vector3.Forward : forward.Normalized();
        }
    }

    /// <summary>The gunner's view: the spotting arc off the barrel, as far as the gun reaches.</summary>
    private void ReportDebug()
    {
        if (GameDebug.Current == null || !GameDebug.Current.IsEnabled)
            return;

        var exclude = new Godot.Collections.Array<Rid>(_selfRids) { Gunner.GetRid() };
        GameDebug.Current.RegisterSight(GetWorld3D().DirectSpaceState, Muzzle, GunForward, SPOT_HALF_ARC * 2f, RANGE, exclude);
    }

    private void HandleGunnery(double delta)
    {
        _awareT -= delta;
        if (_awareT <= 0.0)
        {
            _awareT = AWARE_CHECK_INTERVAL;
            if (!IsValidTarget(_target))
                _target = FindTarget();
        }

        if (_target != null)
            TraverseTowards(_target.GlobalPosition, delta);

        // A burst always runs out, target or not - it is only the aim that follows the target.
        if (_roundsLeft > 0)
        {
            _roundT -= delta;
            if (_roundT > 0.0)
                return;
            _roundT += ROUND_INTERVAL;

            FireRound();
            if (--_roundsLeft == 0)
                _cooldownT = COOLDOWN;
            return;
        }

        if (_cooldownT > 0.0 || _target == null)
            return;
        if (Mathf.Abs(Mathf.RadToDeg(YawOffTo(_target.GlobalPosition))) > FIRE_ALIGN)
            return;

        _roundsLeft = BURST_ROUNDS;
        _roundT = 0.0;
    }

    /// <summary>Nobody at the gun: it stops mid-burst and forgets what it was aiming at. The cooldown runs on.</summary>
    private void CeaseFire()
    {
        _target = null;
        _roundsLeft = 0;
    }

    /// <summary>Nearest enemy the gunner can see within the spotting arc off the barrel.</summary>
    private TeamEntity FindTarget()
    {
        return Sight.FindNearest(this, Muzzle, RANGE, e =>
            e != this && e != Gunner
            && Mathf.Abs(Mathf.RadToDeg(YawOffTo(e.GlobalPosition))) <= SPOT_HALF_ARC
            && IsValidTarget(e));
    }

    /// <summary>
    /// Still worth shooting at: alive, an enemy, in range and in sight from the muzzle. Not tied
    /// to the arc - once spotted, the gun turns to follow.
    /// </summary>
    private bool IsValidTarget(TeamEntity target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target) || target.IsInState(TeamEntityStates.TERMINATED))
            return false;
        if (target.IsMemberOf(TeamMembership.NEUTRAL) || target.IsMemberOf(_teamMembership))
            return false;

        var aim = target.GlobalPosition + Vector3.Up * AIM_HEIGHT;
        var muzzle = Muzzle;
        if (muzzle.DistanceTo(aim) > RANGE)
            return false;

        var exclude = new Godot.Collections.Array<Rid>(_selfRids) { Gunner.GetRid() };
        return Sight.HasLineOfSight(this, muzzle, target, aim, exclude);
    }

    /// <summary>
    /// Signed turn, in radians, from where the gun points to <paramref name="worldPos"/>, measured
    /// on the stand - so a turret stood on a slope still turns about its own axis.
    /// </summary>
    private float YawOffTo(Vector3 worldPos)
    {
        var local = GlobalTransform.AffineInverse() * worldPos;
        if (new Vector2(local.X, local.Z).LengthSquared() < 0.0001f)
            return 0f;

        // Same convention as the units: yaw 0 faces -Z.
        float wanted = Mathf.Atan2(-local.X, -local.Z);
        return Mathf.Wrap(wanted - (_gun?.Rotation.Y ?? 0f), -Mathf.Pi, Mathf.Pi);
    }

    private void TraverseTowards(Vector3 worldPos, double delta)
    {
        if (_gun == null)
            return;

        float off = YawOffTo(worldPos);
        float step = Mathf.DegToRad(TURN_SPEED) * (float)delta;
        _gun.Rotation = new Vector3(0f, _gun.Rotation.Y + Mathf.Clamp(off, -step, step), 0f);
    }

    /// <summary>
    /// One round down the barrel. The gun only traverses, so the round goes the way the barrel
    /// points, raised or lowered onto the target's height - hosing level when there is none.
    /// </summary>
    private void FireRound()
    {
        var muzzle = Muzzle;
        var dir = GunForward;
        if (_target != null && GodotObject.IsInstanceValid(_target))
        {
            var to = _target.GlobalPosition + Vector3.Up * AIM_HEIGHT - muzzle;
            float elevation = Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
            dir = dir * Mathf.Cos(elevation) + Vector3.Up * Mathf.Sin(elevation);
        }
        dir = Projectile.Scatter(dir, SHOT_SPREAD);

        var gunner = Gunner;
        Main3d.Current?.Audio?.Play3D(SHOT_SOUND, muzzle);
        Main3d.Current?.Projectiles?.Spawn(new Projectile()
        {
            Shooter = gunner,
            Pos = muzzle,
            Vel = dir * Soldier.BULLET_SPEED,
            Mask = 0b111,
            ShooterRid = gunner.GetRid(),
            Life = 1,
            Damage = DAMAGE
        });

        Replay(_feed, FEED_ANIMATION);
        Replay(_ejector, EJECT_ANIMATION);
    }

    /// <summary>Plays from the start even if the last round's is still running.</summary>
    private static void Replay(AnimationPlayer player, string animation)
    {
        if (player == null || !player.HasAnimation(animation))
            return;

        player.Play(animation);
        player.Seek(0, true);
    }

    private AnimationPlayer FindAnimationPlayer(string path)
    {
        var node = GetNodeOrNull(path);
        var players = node?.FindChildren("*", nameof(AnimationPlayer), true, false);
        return players != null && players.Count > 0 ? (AnimationPlayer)players[0] : null;
    }

    /// <summary>Hits on the stand or the gun shield. They stop the bullet and that is all.</summary>
    protected override void HitTaken(int pDamage, TeamEntity pShooter, Vector3 hitPos)
    {
        _damageTaken = 0;
    }

    /// <summary>A gunner killed at the gun leaves it free again, for either side to take.</summary>
    private void DropFallen()
    {
        var gunner = Gunner;
        if (gunner == null || (GodotObject.IsInstanceValid(gunner) && !gunner.IsInState(TeamEntityStates.TERMINATED)))
            return;

        _passengers.Clear();
        _teamMembership = TeamMembership.NEUTRAL;
    }

    /// <summary>Steps the gunner off onto open ground on the <paramref name="whereTo"/> side of the turret.</summary>
    private void Unload(Vector2 whereTo)
    {
        var site = BuildSite.FindFrom(this);
        var center = new Vector2(GlobalPosition.X, GlobalPosition.Z);
        var toward = whereTo - center;
        if (toward.LengthSquared() < 0.0001f)
            toward = Vector2.Right;

        foreach (var p in ExitPassengers())
        {
            var from = center + toward;
            var spot = site?.WorkSpotFor(from) ?? from;

            p.GlobalPosition = new Vector3(spot.X, GlobalPosition.Y, spot.Y);
            p.SetVisible(true);
        }
    }

    /// <summary>First frame at 100%: a half-built turret cannot be selected, so the ring only comes now.</summary>
    private void OpenForGunner()
    {
        _opened = true;

        var radius = GameUtils.FlatRadius(this, this);
        if (radius > 0f) _footRadius = radius;
        _selectorObject = GameUtils.CreateSelectionRing(this, _footRadius);

        foreach (var node in FindChildren("*", nameof(CollisionObject3D), true, false))
            _selfRids.Add(((CollisionObject3D)node).GetRid());
    }

    /// <summary>
    /// The gun is visible and solid only once the build is far enough along. Hiding it does not
    /// stop its collider, so that has to be switched by hand - otherwise a bare stand would
    /// already give the cover of the gun shield.
    /// </summary>
    private void UpdateStage()
    {
        // BuildProgress can be assigned before the turret enters the tree - the build site sets it
        // up right after instancing - so until _Ready has run there is nothing to show yet.
        if (_gun == null)
            return;

        var finished = IsFinished;
        if (_shownFinished == finished)
            return;
        _shownFinished = finished;

        _gun.Visible = finished;
        SetSolid(_gun, finished);
    }

    private static void SetSolid(Node node, bool solid)
    {
        if (node is CollisionShape3D shape)
            shape.Disabled = !solid;

        foreach (var child in node.GetChildren())
            SetSolid(child, solid);
    }
}
