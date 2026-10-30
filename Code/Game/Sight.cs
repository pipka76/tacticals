using System;
using Godot;

namespace tacticals.Code.Game;

/// <summary>
/// Spotting shared by everything that looks out for enemies from a fixed eye - the tower's
/// occupant, the turret's gunner. What counts as a target stays with the caller.
/// </summary>
public static class Sight
{
    /// <summary>
    /// Nearest entity within <paramref name="radius"/> of <paramref name="eye"/> that
    /// <paramref name="accept"/> lets through. Only people and equipment are considered; accept is
    /// asked nearest-first, so an expensive check (line of sight) runs as little as it can.
    /// </summary>
    public static TeamEntity FindNearest(Node3D from, Vector3 eye, float radius, Func<TeamEntity, bool> accept)
    {
        var space = from.GetWorld3D().DirectSpaceState;
        var shapeQuery = new PhysicsShapeQueryParameters3D
        {
            Shape = new SphereShape3D { Radius = radius },
            Transform = new Transform3D(Basis.Identity, eye),
            CollisionMask = 0b0110,
            CollideWithAreas = true,
            CollideWithBodies = true
        };

        TeamEntity best = null;
        float bestD2 = float.PositiveInfinity;

        foreach (var h in space.IntersectShape(shapeQuery, 32))
        {
            if (!h.TryGetValue("collider", out var cv) || cv.AsGodotObject() is not Node hitNode)
                continue;

            TeamEntity entity = null;
            for (Node n = hitNode; n != null; n = n.GetParent())
                if (n is TeamEntity te) { entity = te; break; }

            if (entity == null)
                continue;

            var d2 = eye.DistanceSquaredTo(entity.GlobalPosition);
            if (d2 >= bestD2 || !accept(entity))
                continue;

            best = entity;
            bestD2 = d2;
        }

        return best;
    }

    /// <summary>
    /// Whether a ray from <paramref name="eye"/> to <paramref name="aim"/> first hits
    /// <paramref name="target"/> (or something under it). Walls and cover are left in; only what is
    /// in <paramref name="exclude"/> is looked through.
    /// </summary>
    public static bool HasLineOfSight(Node3D from, Vector3 eye, Node3D target, Vector3 aim, Godot.Collections.Array<Rid> exclude)
    {
        var ray = new PhysicsRayQueryParameters3D
        {
            From = eye,
            To = aim,
            CollideWithAreas = true,
            CollideWithBodies = true,
            CollisionMask = 0b1111,
            Exclude = exclude
        };

        var res = from.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if (!res.TryGetValue("collider", out var cv) || cv.AsGodotObject() is not Node hitNode)
            return false;

        for (Node n = hitNode; n != null; n = n.GetParent())
            if (n == target)
                return true;

        return false;
    }
}
