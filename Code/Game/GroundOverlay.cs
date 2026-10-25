using System.Collections.Generic;
using Godot;

namespace tacticals.Code.Game;

/// <summary>
/// Draws flat shapes onto the ground for one frame at a time. Same immediate-mode idea as
/// <see cref="GameDebug"/>, but player-facing: it is not gated on the debug toggle, and it can
/// draw filled translucent shapes as well as outlines.
///
/// Callers wrap their drawing in <see cref="Begin"/> / <see cref="End"/> every frame. Shapes come
/// in two families:
///  - the <c>...At</c> family builds geometry in the tangent plane of a surface hit (position +
///    normal, straight off a raycast), which is exact for anything up to a few units across and
///    needs no height sampling at all;
///  - the <c>Ground...</c> family drapes world-XZ shapes over the terrain by height lookup, for
///    shapes too large for a single tangent plane.
/// </summary>
public partial class GroundOverlay : Node3D
{
    private readonly struct Vertex
    {
        public readonly Vector3 Position;
        public readonly Color Color;

        public Vertex(Vector3 position, Color color)
        {
            Position = position;
            Color = color;
        }
    }

    private const int MIN_CIRCLE_SEGMENTS = 16;
    private const int MAX_CIRCLE_SEGMENTS = 128;

    private ImmediateMesh _fillMesh;
    private ImmediateMesh _lineMesh;

    // Outlines and fills are interleaved by callers but ImmediateMesh cannot interleave surfaces,
    // so both are buffered and emitted in one go on End().
    private readonly List<Vertex> _fillVerts = new List<Vertex>();
    private readonly List<Vertex> _lineVerts = new List<Vertex>();

    /// <summary>How far off the surface shapes sit, to stay clear of z-fighting.</summary>
    public float Lift { get; set; } = 0.06f;

    /// <summary>
    /// Longest span allowed before a shape is subdivided. Keeps draped lines and big fills
    /// following the ground instead of cutting through it.
    /// </summary>
    public float MaxSegmentLength { get; set; } = 1.5f;

    public override void _Ready()
    {
        // Fills first, outlines second, so an outline always reads on top of its own fill.
        _fillMesh = AddSurface(1);
        _lineMesh = AddSurface(2);
    }

    public void Begin()
    {
        _fillVerts.Clear();
        _lineVerts.Clear();
    }

    public void End()
    {
        Emit(_fillMesh, _fillVerts, Mesh.PrimitiveType.Triangles);
        Emit(_lineMesh, _lineVerts, Mesh.PrimitiveType.Lines);
    }

    #region Tangent-plane shapes

    /// <summary>Filled disc lying in the surface plane at a raycast hit.</summary>
    public void DiscAt(Vector3 origin, Vector3 normal, float radius, Color color)
    {
        RingAt(origin, normal, 0f, radius, color);
    }

    /// <summary>Filled annulus lying in the surface plane at a raycast hit.</summary>
    public void RingAt(Vector3 origin, Vector3 normal, float innerRadius, float outerRadius, Color color)
    {
        if (outerRadius <= 0f || outerRadius <= innerRadius)
            return;

        var center = Anchor(origin, normal, out var right, out var forward);
        int segments = CircleSegments(outerRadius);

        // Subdivide radially as well as around, so a wide ring can be draped later without one
        // triangle spanning a whole hill.
        int bands = Mathf.Max(1, Mathf.CeilToInt((outerRadius - innerRadius) / MaxSegmentLength));

        for (int band = 0; band < bands; band++)
        {
            float r0 = Mathf.Lerp(innerRadius, outerRadius, band / (float)bands);
            float r1 = Mathf.Lerp(innerRadius, outerRadius, (band + 1) / (float)bands);

            for (int s = 0; s < segments; s++)
            {
                float a0 = s / (float)segments * Mathf.Tau;
                float a1 = (s + 1) / (float)segments * Mathf.Tau;

                var inner0 = center + Radial(right, forward, a0, r0);
                var inner1 = center + Radial(right, forward, a1, r0);
                var outer0 = center + Radial(right, forward, a0, r1);
                var outer1 = center + Radial(right, forward, a1, r1);

                AddTriangle(inner0, outer0, outer1, color);
                AddTriangle(inner0, outer1, inner1, color);
            }
        }
    }

    /// <summary>Circle outline lying in the surface plane at a raycast hit.</summary>
    public void CircleOutlineAt(Vector3 origin, Vector3 normal, float radius, Color color)
    {
        if (radius <= 0f)
            return;

        var center = Anchor(origin, normal, out var right, out var forward);
        int segments = CircleSegments(radius);

        var previous = center + Radial(right, forward, 0f, radius);
        for (int s = 1; s <= segments; s++)
        {
            var next = center + Radial(right, forward, s / (float)segments * Mathf.Tau, radius);
            AddLine(previous, next, color);
            previous = next;
        }
    }

    /// <summary>
    /// A vertical stalk rising from the hit, fading out at the top. This is what keeps a marker
    /// findable when the camera looks along the ground and the flat shapes foreshorten to nothing.
    /// Deliberately world-vertical rather than normal-aligned - it reads as a plumb line.
    /// </summary>
    public void PostAt(Vector3 origin, Vector3 normal, float height, Color color)
    {
        if (height <= 0f)
            return;

        var basePoint = Anchor(origin, normal, out _, out _);
        var faded = new Color(color, 0f);

        _lineVerts.Add(new Vertex(basePoint, color));
        _lineVerts.Add(new Vertex(basePoint + Vector3.Up * height, faded));
    }

    /// <summary>Cross-hair through the hit, lying in the surface plane.</summary>
    public void CrossAt(Vector3 origin, Vector3 normal, float size, Color color)
    {
        if (size <= 0f)
            return;

        var center = Anchor(origin, normal, out var right, out var forward);

        AddLine(center - right * size, center + right * size, color);
        AddLine(center - forward * size, center + forward * size, color);
    }

    /// <summary>
    /// Flat arrow lying in the surface plane, starting at the hit and pointing along
    /// <paramref name="direction"/> (projected onto the plane): a filled head on an outlined shaft.
    /// </summary>
    public void ArrowAt(Vector3 origin, Vector3 normal, Vector3 direction, float length, Color color)
    {
        if (length <= 0f)
            return;

        var start = Anchor(origin, normal, out var right, out var forward);
        var n = right.Cross(forward).Normalized();

        var along = direction - n * direction.Dot(n);
        if (along.LengthSquared() < 0.000001f)
            return;
        along = along.Normalized();
        var side = along.Cross(n).Normalized();

        float headLength = length * 0.35f;
        float headWidth = length * 0.22f;
        var tip = start + along * length;
        var headBase = tip - along * headLength;

        AddLine(start, headBase, color);
        AddTriangle(tip, headBase + side * headWidth, headBase - side * headWidth, color);
    }

    #endregion

    #region Draped shapes

    /// <summary>
    /// Straight segment in world XZ, draped over the terrain. Unlike GameDebug's equivalent this
    /// samples along the span, not just at the ends, so a long line follows hills.
    /// </summary>
    public void GroundLine(Vector2 from, Vector2 to, Color color)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(to) / MaxSegmentLength));

        var previous = GroundPoint(from);
        for (int i = 1; i <= steps; i++)
        {
            var next = GroundPoint(from.Lerp(to, i / (float)steps));
            AddLine(previous, next, color);
            previous = next;
        }
    }

    public void GroundPolyline(IReadOnlyList<Vector2> points, Color color, bool closed = false)
    {
        if (points == null || points.Count < 2)
            return;

        for (int i = 0; i < points.Count - 1; i++)
            GroundLine(points[i], points[i + 1], color);

        if (closed)
            GroundLine(points[points.Count - 1], points[0], color);
    }

    public void GroundCircleOutline(Vector2 center, float radius, Color color)
    {
        if (radius <= 0f)
            return;

        int segments = CircleSegments(radius);

        var previous = GroundPoint(center + new Vector2(radius, 0f));
        for (int s = 1; s <= segments; s++)
        {
            float angle = s / (float)segments * Mathf.Tau;
            var next = GroundPoint(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            AddLine(previous, next, color);
            previous = next;
        }
    }

    /// <summary>
    /// Terrain height at a world XZ, lifted clear of the surface.
    /// NOTE: Plains.GetTerrainHeight is a nearest-sample lookup at sub-block resolution and does
    /// not reproduce the rendered mesh, so draped shapes step rather than glide. Shapes small
    /// enough to sit in one tangent plane should use the ...At family instead, which is exact.
    /// </summary>
    private Vector3 GroundPoint(Vector2 worldXZ)
    {
        float height = (Main3d.Current?.Map?.GetTerrainHeight(worldXZ) ?? 0f) + Lift;
        return new Vector3(worldXZ.X, height, worldXZ.Y);
    }

    #endregion

    private ImmediateMesh AddSurface(int renderPriority)
    {
        var mesh = new ImmediateMesh();

        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            // Translucent shapes overlap each other constantly; writing depth would make them
            // fight over which one gets to exist.
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            DisableReceiveShadows = true,
            RenderPriority = renderPriority
        };

        var instance = new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };

        AddChild(instance);
        return mesh;
    }

    private static void Emit(ImmediateMesh mesh, List<Vertex> verts, Mesh.PrimitiveType primitive)
    {
        if (mesh == null)
            return;

        mesh.ClearSurfaces();
        if (verts.Count == 0)
            return;

        mesh.SurfaceBegin(primitive);
        foreach (var vertex in verts)
        {
            mesh.SurfaceSetColor(vertex.Color);
            mesh.SurfaceAddVertex(vertex.Position);
        }
        mesh.SurfaceEnd();
    }

    /// <summary>
    /// Lifts a surface hit clear of the ground and hands back two axes spanning its tangent plane.
    /// </summary>
    private Vector3 Anchor(Vector3 origin, Vector3 normal, out Vector3 right, out Vector3 forward)
    {
        var n = normal.LengthSquared() < 0.000001f ? Vector3.Up : normal.Normalized();

        // Any seed not parallel to the normal spans the plane; swap it near vertical faces.
        var seed = Mathf.Abs(n.Dot(Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;
        right = seed.Cross(n).Normalized();
        forward = n.Cross(right).Normalized();

        return origin + n * Lift;
    }

    private static Vector3 Radial(Vector3 right, Vector3 forward, float angle, float radius)
    {
        return (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
    }

    private int CircleSegments(float radius)
    {
        int segments = Mathf.CeilToInt(Mathf.Tau * radius / MaxSegmentLength);
        return Mathf.Clamp(segments, MIN_CIRCLE_SEGMENTS, MAX_CIRCLE_SEGMENTS);
    }

    private void AddLine(Vector3 from, Vector3 to, Color color)
    {
        _lineVerts.Add(new Vertex(from, color));
        _lineVerts.Add(new Vertex(to, color));
    }

    private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        _fillVerts.Add(new Vertex(a, color));
        _fillVerts.Add(new Vertex(b, color));
        _fillVerts.Add(new Vertex(c, color));
    }
}
