using System.Collections.Generic;
using Godot;

namespace tacticals.Code.Game;

public struct Projectile
{
    public Vector3 Pos;
    public Vector3 Vel;
    public float Life;
    public int Damage;
    public uint Mask;
    public Rid ShooterRid;     // for exclude
    public TeamEntity Shooter; // optional: for attribution

    // Debug trail (optional)
    public Vector3[]? Trail;
    public int TrailCount;
    public int TrailHead;
}

public partial class ProjectileManager : Node3D
{
    private readonly List<Projectile> _projs = new();

    [Export] public bool DebugDrawProjectiles = false;
    [Export(PropertyHint.Range, "2,256,1")] public int DebugTrailPoints = 32;

    private MeshInstance3D? _debugMeshInstance;
    private ImmediateMesh? _debugMesh;
    private Material? _debugMaterial;

    public void Spawn(Projectile p)
    {
        if (DebugDrawProjectiles)
            EnsureTrailInitialized(ref p);
        _projs.Add(p);
    }

    public override void _Ready()
    {
        // Create a simple line renderer for optional debug visualization.
        _debugMesh = new ImmediateMesh();
        _debugMeshInstance = new MeshInstance3D { Mesh = _debugMesh };
        // ImmediateMesh has a dynamic AABB; give it generous culling margin so it doesn't disappear.
        _debugMeshInstance.ExtraCullMargin = 1000f;
        _debugMeshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        
        var mat = new OrmMaterial3D()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = Colors.White
        };
        _debugMaterial = mat;
        _debugMeshInstance.MaterialOverride = _debugMaterial;
        AddChild(_debugMeshInstance);

        // Start hidden; it will be shown when DebugDrawProjectiles is enabled.
        _debugMeshInstance.Visible = DebugDrawProjectiles;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_projs.Count == 0) return;

        var space = GetWorld3D().DirectSpaceState;

        // Keep the debug renderer visibility in sync with the toggle.
        if (_debugMeshInstance != null)
            _debugMeshInstance.Visible = DebugDrawProjectiles;

        for (int i = _projs.Count - 1; i >= 0; i--)
        {
            var p = _projs[i];
            p.Life -= dt;
            if (p.Life <= 0f) { _projs.RemoveAt(i); continue; }

            Vector3 from = p.Pos;
            Vector3 to   = p.Pos + p.Vel * dt; // add gravity here if desired

            if (DebugDrawProjectiles)
                EnsureTrailInitialized(ref p);

            var q = new PhysicsRayQueryParameters3D
            {
                From = from,
                To = to,
                CollisionMask = p.Mask,
                CollideWithBodies = true,
                CollideWithAreas = true,
            };

            q.Exclude = new Godot.Collections.Array<Rid> { p.ShooterRid };

            var hit = space.IntersectRay(q);
            if (hit.Count > 0)
            {
                if (DebugDrawProjectiles && hit.TryGetValue("position", out var hp))
                    PushTrailPoint(ref p, (Vector3)hp);

                HandleHit(p, hit);
                _projs.RemoveAt(i);
                continue;
            }

            if (DebugDrawProjectiles)
                PushTrailPoint(ref p, to);

            p.Pos = to;
            _projs[i] = p;
        }

        if (DebugDrawProjectiles)
            RebuildDebugLines();
        else
            ClearDebugLines();
    }

    private void EnsureTrailInitialized(ref Projectile p)
    {
        if (DebugTrailPoints < 2) DebugTrailPoints = 2;

        if (p.Trail == null || p.Trail.Length != DebugTrailPoints)
        {
            p.Trail = new Vector3[DebugTrailPoints];
            p.TrailCount = 0;
            p.TrailHead = 0;
            // Seed with the current position so the first segment draws correctly.
            PushTrailPoint(ref p, p.Pos);
        }
    }

    private void PushTrailPoint(ref Projectile p, Vector3 pos)
    {
        if (p.Trail == null) return;

        p.Trail[p.TrailHead] = pos;
        p.TrailHead = (p.TrailHead + 1) % p.Trail.Length;
        p.TrailCount = Mathf.Min(p.TrailCount + 1, p.Trail.Length);
    }

    private void ClearDebugLines()
    {
        _debugMesh?.ClearSurfaces();
    }

    private void RebuildDebugLines()
    {
        if (_debugMesh == null) return;

        _debugMesh.ClearSurfaces();
        if (_projs.Count == 0) return;

        bool any = false;
        Vector3 min = default;
        Vector3 max = default;

        _debugMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, _debugMaterial);

        for (int i = 0; i < _projs.Count; i++)
        {
            var p = _projs[i];
            if (p.Trail == null || p.TrailCount < 2) continue;

            // Oldest point is (TrailHead - TrailCount)
            int n = p.TrailCount;
            int len = p.Trail.Length;
            int start = (p.TrailHead - n + len) % len;

            Vector3 prev = p.Trail[start];
            for (int k = 1; k < n; k++)
            {
                int idx = (start + k) % len;
                Vector3 cur = p.Trail[idx];

                if (!any)
                {
                    any = true;
                    min = prev;
                    max = prev;
                }

                min = min.Min(prev);
                min = min.Min(cur);
                max = max.Max(prev);
                max = max.Max(cur);

                _debugMesh.SurfaceAddVertex(prev);
                _debugMesh.SurfaceAddVertex(cur);

                prev = cur;
            }
        }

        _debugMesh.SurfaceEnd();

        if (_debugMeshInstance != null)
        {
            if (any)
            {
                // Slightly expand so thin lines aren't culled at the edges.
                var size = (max - min) + new Vector3(0.2f, 0.2f, 0.2f);
                _debugMeshInstance.CustomAabb = new Aabb(min - new Vector3(0.1f, 0.1f, 0.1f), size);
            }
            else
            {
                _debugMeshInstance.CustomAabb = new Aabb(Vector3.Zero, Vector3.One);
            }
        }
    }

    private void HandleHit(Projectile p, Godot.Collections.Dictionary hit)
    {
        var hitPos = (Vector3)hit["position"];
        var normal = (Vector3)hit["normal"];

        // Find root entity (same parent-walk you already use)
        if (hit.TryGetValue("collider", out var cv) && cv.AsGodotObject() is Node n)
        {
            TeamEntity? target = null;
            for (Node t = n; t != null; t = t.GetParent())
                if (t is TeamEntity te) { target = te; break; }

            if (target != null)
                target.TakeHit(p.Damage, p.Shooter, hitPos); 
        }

        // spawn impact VFX/SFX here
    }
}