extends RefCounted
## Park–Miller RNG: a small JSON-exact state, separated by gameplay domain.
## No presentation or simulation frame consumes gameplay randomness.
static func next(state: Dictionary, stream: String) -> float:
	var streams: Dictionary = state["rng"]
	var v: int = int(streams.get(stream, 1))
	v = (v * 16807) % 2147483647
	streams[stream] = v
	return float(v - 1) / 2147483646.0

static func pick_weighted(state: Dictionary, stream: String, items: Array, weights: Array):
	if items.is_empty():
		return null
	var total: float = 0.0
	for w in weights:
		total += maxf(float(w), 0.0)
	if total <= 0.0:
		return items[0]
	var point: float = next(state, stream) * total
	for i in items.size():
		point -= maxf(float(weights[i]), 0.0)
		if point < 0.0:
			return items[i]
	return items[-1]

static func shuffled(state: Dictionary, stream: String, source: Array) -> Array:
	var result: Array = source.duplicate(true)
	for i in range(result.size() - 1, 0, -1):
		var j: int = mini(int(next(state, stream) * (i + 1)), i)
		var temp = result[i]
		result[i] = result[j]
		result[j] = temp
	return result
