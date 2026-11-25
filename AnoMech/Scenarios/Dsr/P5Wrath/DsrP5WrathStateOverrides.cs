namespace AnoMech.Scenarios.Dsr.P5Wrath;

// User-forced rolls for DsrP5WrathState; null leaves a field randomized.
public sealed class DsrP5WrathStateOverrides
{
    // The local player's own marker.
    public WrathMarker? Marker { get; set; }
    // Whether Thunderstruck lands on the local player.
    public bool? Thunderstruck { get; set; }
    public bool? GrinnauxOnVedrfolnirSide { get; set; }
}
