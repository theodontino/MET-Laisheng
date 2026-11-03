namespace MET.Core {
public sealed partial class Game {
    // Session owns External settlement; detached forecasts retain this ownership.
    public bool world_managed=false;
    public V world_effects=V.Map();
}
}
