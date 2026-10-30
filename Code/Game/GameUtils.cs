using System;
using Godot;

public class GameUtils
{
    /// <summary>
    /// Generates a rectangular grid of Vector2 positions such that `center` is at the grid's center.
    /// </summary>
    /// <param name="center">The coordinate that will lie at the center of the grid.</param>
    /// <param name="columns">Number of points across (width).</param>
    /// <param name="rows">Number of points up (height).</param>
    /// <param name="spacing">Distance between adjacent points.</param>
    /// <returns>Flat array of Vector2 positions, length = columns * rows.</returns>
    public static Vector2[] GenerateGrid(Vector2 center, int columns, int rows, float spacing)
    {
        if (columns < 1 || rows < 1)
            throw new ArgumentException("columns and rows must both be at least 1");

        var result = new Vector2[columns * rows];

        // Calculate the offset so that the center lies in the exact middle
        // (columns - 1) * spacing is total width; half of that is how far to go left from center
        float halfWidth  = (columns - 1) * spacing * 0.5f;
        float halfHeight = (rows    - 1) * spacing * 0.5f;

        int idx = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                float px = center.X - halfWidth  + x * spacing;
                float py = center.Y - halfHeight + y * spacing;
                result[idx++] = new Vector2(px, py);
            }
        }

        return result;
    }

    private const string SELECTION_RING_PATH = "res://Assets/Game/SelectionRing.fbx";

    /// <summary>
    /// The furthest any mesh under <paramref name="root"/> reaches from <paramref name="frame"/>'s
    /// origin in its XZ plane, or 0 when there are no meshes.
    /// </summary>
    public static float FlatRadius(Node3D root, Node3D frame)
    {
        float radius = 0f;
        var toFrame = frame.GlobalTransform.AffineInverse();

        foreach (var node in root.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            var mesh = (MeshInstance3D)node;
            var aabb = mesh.GetAabb();
            var toLocal = toFrame * mesh.GlobalTransform;
            for (int i = 0; i < 8; i++)
            {
                var corner = toLocal * aabb.GetEndpoint(i);
                radius = Mathf.Max(radius, new Vector2(corner.X, corner.Z).Length());
            }
        }

        return radius;
    }

    /// <summary>
    /// A selection ring around a structure <paramref name="footRadius"/> wide, added under
    /// <paramref name="owner"/> and hidden. Units bring their own ring in their scene; structures
    /// get one made like this.
    /// </summary>
    public static Node3D CreateSelectionRing(Node3D owner, float footRadius)
    {
        var scene = GD.Load<PackedScene>(SELECTION_RING_PATH);
        if (scene == null)
            return null;

        var ring = scene.Instantiate<Node3D>();
        ring.Name = "SelectionRing";
        owner.AddChild(ring);

        // Sized off its own mesh, so it hugs the structure whatever units the model came in.
        float ringRadius = FlatRadius(ring, ring);
        if (ringRadius > 0f)
            ring.Scale = Vector3.One * ((footRadius + 0.5f) / ringRadius);
        ring.Visible = false;
        return ring;
    }
}
