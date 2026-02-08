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
}

public partial class ProjectileManager : Node3D
{
    private readonly List<Projectile> _projs = new();

    public void Spawn(Projectile p) => _projs.Add(p);

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_projs.Count == 0) return;

        var space = GetWorld3D().DirectSpaceState;

        for (int i = _projs.Count - 1; i >= 0; i--)
        {
            var p = _projs[i];
            p.Life -= dt;
            if (p.Life <= 0f) { _projs.RemoveAt(i); continue; }

            Vector3 from = p.Pos;
            Vector3 to   = p.Pos + p.Vel * dt; // add gravity here if desired

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
                HandleHit(p, hit);
                _projs.RemoveAt(i);
                continue;
            }

            p.Pos = to;
            _projs[i] = p;
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