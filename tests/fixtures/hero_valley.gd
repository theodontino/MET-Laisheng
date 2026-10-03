extends RefCounted
## Explicit, isolated real-Game fixture. Never used to replace a player's city.
const Game = preload("res://core/game.gd")
const Hex = preload("res://core/hex.gd")
const WORLD_SEED := 905003
const VISUAL_SEED := 314159

static func make_game():
	var g := Game.new()
	g.new_game(WORLD_SEED, 0.0)
	g.select_block(0)
	g.choose_origin(str(g.s.origin_choices[0]))
	# 37 real authored tiles: radius-three valley plus its authored headwater bend.
	# Exchange three western woodland edge cells for three upstream cells; terrain
	# traits/elevation remain the current world's actual authored data.
	for key in g.s.cells.keys():
		var upstream := str(key) in ["0,-4", "-1,-3", "-2,-2"]
		if (Hex.distance(str(key), "0,0") > 3 and not upstream) or str(key) in ["-3,0", "-3,1", "-3,2"]:
			g.s.cells.erase(key)
	g.create_building("neighborhood", "0,-1", "civic", true)
	var residence: Dictionary = g.create_building("residence", "2,0", "commons", true)
	g.Population.create(g, str(residence.id), 12.0)
	g.s.projects.municipal.inventory.food = 180.0
	g.s.projects.municipal.inventory.material = 160.0
	g.refresh_metrics()
	var error: String = g.Sim.tick(g, 0.02)
	assert(error == "", error)
	g.s.settings.user_paused = true
	return g

static func committed(g) -> Dictionary:
	return {"tiles": g.s.cells, "projects": g.s.projects, "groups": g.s.groups,
		"metrics": g.s.metrics, "compiled": g.compiled, "audit": g.s.audit,
		"forecast": false, "source_phase": "committed_tick", "tick": g.s.simulation_tick,
		"snapshot_id": "hero-valley-v1:" + str(g.revision) + ":" + str(g.s.simulation_tick)}
