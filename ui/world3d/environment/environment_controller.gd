extends Node3D
## One visual-state owner. Renderers never read gameplay time or weather.
const State = preload("res://ui/world3d/environment/environment_state.gd")
var state: Dictionary = State.fixed_daylight()
var sun: DirectionalLight3D
var world_environment: WorldEnvironment
var materials: Array[ShaderMaterial] = []

func _ready() -> void:
	sun = DirectionalLight3D.new()
	sun.name = "SharedDaylight"
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 40.0
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	add_child(sun)
	world_environment = WorldEnvironment.new()
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color("cbd8dc")
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color("d3e0d1")
	env.ambient_light_energy = 0.35
	env.reflected_light_source = Environment.REFLECTION_SOURCE_DISABLED
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.ssao_enabled = true
	env.ssao_radius = 0.35
	env.ssao_intensity = 0.55
	env.ssao_light_affect = 0.15
	world_environment.environment = env
	add_child(world_environment)
	apply_state(state)

func apply_state(value: Dictionary) -> void:
	state = value.duplicate(true)
	for material in materials: _apply_material(material)
	if sun == null: return
	sun.rotation_degrees = state.sun_state.rotation_degrees
	sun.light_energy = float(state.sun_state.energy)
	sun.light_color = state.sun_state.color
	world_environment.environment.fog_enabled = float(state.fog_density) > 0.0
	world_environment.environment.fog_density = float(state.fog_density)

func clear_materials() -> void:
	materials.clear()

func register_material(material: ShaderMaterial) -> void:
	if material == null or material.shader == null: return
	if material not in materials: materials.append(material)
	_apply_material(material)

func _apply_material(material: ShaderMaterial) -> void:
	# Reserved uniforms receive the visual state from this one owner. No clock
	# advances here; V1 values stay fixed until explicitly applied by a caller.
	for uniform in material.shader.get_shader_uniform_list():
		var key := str(uniform.name)
		if key in ["world_time", "cloudiness", "fog_density", "humidity", "wetness", "wind"] and state.has(key):
			material.set_shader_parameter(key, state[key])
