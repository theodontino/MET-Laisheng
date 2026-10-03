shader_type spatial;
render_mode cull_back;

uniform vec4 deep_water : source_color = vec4(0.15, 0.34, 0.39, 1.0);
uniform vec4 shallow_water : source_color = vec4(0.38, 0.54, 0.46, 1.0);
// Fixed V1 state; an EnvironmentController can later supply this explicitly.
uniform float world_time = 0.0;
uniform float wetness : hint_range(0.0, 1.0) = 0.0;

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float bank = pow(abs(UV.x * 2.0 - 1.0), 2.3);
	// World-space, low-contrast static currents are independent of triangle
	// tangents and ribbon segment UV orientation. A flat upward normal keeps
	// one continuous water surface under the shared real-time daylight.
	vec2 plane = world_position.xz;
	float ripple = sin(plane.x * 7.3 + plane.y * 4.1 + sin(plane.y * 2.4)) * 0.5 + 0.5;
	float slow = sin(plane.x * 1.1 - plane.y * 1.7) * 0.5 + 0.5;
	ALBEDO = mix(deep_water.rgb, shallow_water.rgb, bank * 0.72) * (0.99 + ripple * 0.02 + slow * 0.018);
	ROUGHNESS = 0.43 + bank * 0.16 + ripple * 0.035;
	SPECULAR = 0.32;
	METALLIC = 0.0;
}
