extends RefCounted
## V1 is fixed daylight. These are visual parameters, not a climate simulation.
static func fixed_daylight() -> Dictionary:
	return {"world_time": 36000.0,
		"sun_state": {"rotation_degrees": Vector3(-48, -32, 0), "energy": 1.15, "color": Color("fff0d6")},
		"cloudiness": 0.05, "fog_density": 0.0, "humidity": 0.4,
		"wetness": 0.0, "wind": Vector3.ZERO, "temperature_band": "temperate"}
