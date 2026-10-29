using Godot;

namespace tacticals.Code.Game;

/// <summary>
/// A bar floating in the world that always faces the camera and draws over whatever is in front
/// of it, e.g. the build progress above a structure going up. One quad and a shader - the fill is
/// worked out per pixel, so moving <see cref="Value"/> every frame costs one uniform write.
/// </summary>
public partial class ProgressBar3D : MeshInstance3D
{
    private const string SHADER_CODE = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_test_disabled, shadows_disabled;

uniform float value = 0.0;
uniform vec2 size = vec2(4.0, 0.5);
uniform float border = 0.1;
uniform vec4 fill_color : source_color = vec4(1.0, 0.9, 0.35, 1.0);
uniform vec4 empty_color : source_color = vec4(0.1, 0.1, 0.1, 0.55);
uniform vec4 border_color : source_color = vec4(0.0, 0.0, 0.0, 0.85);

void vertex() {
    // Billboard: keep the node's position, take the camera's orientation.
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}

void fragment() {
    // In world units, so the border is equally thick on all four sides.
    vec2 p = UV * size;
    float b = border * size.y;
    vec4 c;
    if (p.x < b || p.x > size.x - b || p.y < b || p.y > size.y - b)
        c = border_color;
    else
        c = (p.x - b) / (size.x - 2.0 * b) <= value ? fill_color : empty_color;
    ALBEDO = c.rgb;
    ALPHA = c.a;
}
";

    private static Shader _shader;

    private ShaderMaterial _material;
    private float _value;

    /// <summary>How full the bar is, 0..1.</summary>
    public float Value
    {
        get => _value;
        set
        {
            _value = Mathf.Clamp(value, 0f, 1f);
            _material?.SetShaderParameter("value", _value);
        }
    }

    /// <summary>Makes a bar <paramref name="width"/> by <paramref name="height"/> world units in <paramref name="color"/>.</summary>
    public static ProgressBar3D Create(float width, float height, Color color)
    {
        _shader ??= new Shader { Code = SHADER_CODE };

        var bar = new ProgressBar3D
        {
            Name = "ProgressBar",
            Mesh = new QuadMesh { Size = new Vector2(width, height) },
            CastShadow = ShadowCastingSetting.Off
        };
        bar._material = new ShaderMaterial { Shader = _shader };
        bar._material.SetShaderParameter("size", new Vector2(width, height));
        bar._material.SetShaderParameter("fill_color", color);
        bar._material.SetShaderParameter("value", 0f);
        bar.MaterialOverride = bar._material;
        // Billboarded in the shader, so the culling box has to cover any way it can turn.
        bar.ExtraCullMargin = width;
        return bar;
    }
}
