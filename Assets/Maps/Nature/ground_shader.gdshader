shader_type spatial;
render_mode specular_disabled;


uniform vec4 ground_color : source_color;
uniform float outline_mask = 1.0;

varying vec4 world_pos;

void vertex() {
	world_pos = MODEL_MATRIX * vec4(VERTEX, 1.0);
}

void fragment() {
	ALBEDO = ground_color.rgb;
	SPECULAR = 0.0;
	ROUGHNESS = outline_mask;
}

void light() {
	float light = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	DIFFUSE_LIGHT += light * (LIGHT_COLOR / PI) * ATTENUATION;

}
