extends SceneTree
## Lightweight headless checks for the continuous visual elevation field.
const Elevation = preload("res://ui/map/elevation040.gd")

var failures := 0

func _initialize() -> void:
	var cells := {
		"0,0": {"q": 0, "r": 0, "terrain": "water"},
		"1,0": {"q": 1, "r": 0, "terrain": "plain"},
		"2,0": {"q": 2, "r": 0, "terrain": "hill"},
		"3,0": {"q": 3, "r": 0, "terrain": "plain"},
		"4,3": {"q": 4, "r": 3, "terrain": "hill"},
		"0,1": {"q": 0, "r": 1, "terrain": "forest"},
		"1,1": {"q": 1, "r": 1, "terrain": "forest"},
		"2,1": {"q": 2, "r": 1, "terrain": "plain"},
		"-3,3": {"q": -3, "r": 3, "terrain": "forest"},
	}
	var field := Elevation.derive(cells, 7040)
	var repeated := Elevation.derive(cells, 7040)
	var water := Elevation.world_center(Vector2i(0, 0))
	var bank := Elevation.world_center(Vector2i(1, 0))
	var hill := Elevation.world_center(Vector2i(2, 0))
	var forest := Elevation.world_center(Vector2i(-3, 3))
	var stable_a := Elevation.height_at(field, hill + Vector2(13.25, 4.5))
	var stable_b := Elevation.height_at(repeated, hill + Vector2(13.25, 4.5))
	_check(is_equal_approx(stable_a, stable_b), "same cells and seed derive the same height")
	_check(field["peaks"].size() >= 2, "separate hill regions create separate broad landforms")
	_check(is_zero_approx(Elevation.height_at(field, water)), "water cell center remains at zero")
	_check(is_zero_approx(Elevation.height_at(field, bank)), "waterfront cell center joins the zero-height shore")
	var summit := Elevation.height_at(field, hill)
	_check(summit >= 50.0 and summit <= 100.0, "hill core rises into the 50-100 unit range")
	var plateau := Elevation.height_at(field, hill + Vector2(20.0, 0.0))
	_check(absf(summit - plateau) < 4.0, "hill core includes a broad flat buildable plateau")
	_check(Elevation.height_at(field, forest) < 15.0, "forest remains lowland beside the hill")
	var slope_a := Elevation.height_at(field, hill + Vector2(150.0, 0.0))
	var slope_b := Elevation.height_at(field, hill + Vector2(152.0, 0.0))
	var slope_c := Elevation.height_at(field, hill + Vector2(154.0, 0.0))
	_check(absf(slope_b - slope_a) < 3.0 and absf(slope_c - slope_b) < 3.0,
		"broad hill shoulder changes continuously across nearby samples")
	_check(absf((slope_c - slope_b) - (slope_b - slope_a)) < 1.0,
		"nearby shoulder slopes change gradually")
	var gradient := Elevation.gradient_at(field, hill + Vector2(150.0, 0.0))
	_check(is_finite(gradient.x) and is_finite(gradient.y), "finite-difference slope is finite")
	var isolated := Elevation.derive({"0,0": {"q": 0, "r": 0, "terrain": "hill"}}, 7040)
	var distant_jump := 0.0
	for x in range(500, 1000, 2):
		distant_jump = maxf(distant_jump, absf(Elevation.height_at(isolated, Vector2(x, 0)) - Elevation.height_at(isolated, Vector2(x + 2, 0))))
	_check(distant_jump < 1.0, "distant terrain fades smoothly without a cutoff cliff")
	print("ELEVATION040 ", "11 checks, ", failures, " failed")
	quit(1 if failures else 0)


func _check(condition: bool, label: String) -> void:
	if not condition:
		failures += 1
	print(("PASS " if condition else "FAIL ") + label)
