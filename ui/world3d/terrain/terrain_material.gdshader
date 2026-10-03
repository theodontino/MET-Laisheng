shader_type spatial;
render_mode cull_back;

uniform vec4 grass_color : source_color = vec4(0.38, 0.48, 0.27, 1.0);
uniform vec4 grass_light : source_color = vec4(0.57, 0.61, 0.37, 1.0);
uniform vec4 dirt_color : source_color = vec4(0.56, 0.47, 0.33, 1.0);
uniform vec4 rock_color : source_color = vec4(0.54, 0.55, 0.50, 1.0);
uniform float visual_seed = 0.0;
// Uniform EnvironmentController owns this interface; terrain has no clock.
uniform float wetness : hint_range(0.0, 1.0) = 0.0;

float surface_hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7)) + visual_seed * 0.071) * 43758.5453);
}

float surface_noise(vec2 p) {
	vec2 cell = floor(p);
	vec2 local = fract(p);
	local = local * local * (3.0 - 2.0 * local);
	return mix(mix(surface_hash(cell), surface_hash(cell + vec2(1.0, 0.0)), local.x),
		mix(surface_hash(cell + vec2(0.0, 1.0)), surface_hash(cell + vec2(1.0)), local.x), local.y);
}

void fragment() {
	float micro = surface_noise(UV * 22.0);
	float broad = surface_noise(UV * 0.85);
	float patch = surface_noise(UV * 3.1 + vec2(9.1, 31.7));
	vec3 grass = mix(grass_color.rgb, grass_light.rgb, COLOR.a * 0.35 + broad * 0.42 + patch * 0.12);
	vec3 surface = mix(grass, dirt_color.rgb, COLOR.r);
	vec3 rock = rock_color.rgb * (0.88 + surface_noise(UV * 9.0) * 0.18);
	surface = mix(surface, rock, smoothstep(0.12, 0.65, COLOR.g) * 0.90);
	surface *= 0.98 + micro * 0.04;
	surface *= 1.0 - COLOR.b * 0.14 - wetness * 0.13;
	ALBEDO = surface;
	ROUGHNESS = mix(0.91, 0.69, wetness);
	SPECULAR = 0.18;
}
