using SignalGarden.Core.Models;

namespace SignalGarden.Api.Demo;

/// <summary>
/// Staged demo scenarios, switched on by <c>Api:Demo:SyntheticEvents</c>.
/// </summary>
/// <remarks>
/// Real detections are rarer than a demo needs (no bus has driven into the river
/// yet), so this stages one. Every synthetic event carries
/// <see cref="OperationalEvent.Synthetic"/> = true and the UI labels it plainly.
/// The point is the evidence trail and presentation, not the fiction.
/// </remarks>
public static class SyntheticEvents
{
    // Mid-river at New Farm: inside the Brisbane River polygon, ~700 m from the
    // nearest bridge (Story Bridge). Checked against the OSM layers in ADX.
    private const double Lat = -27.4705, Lon = 153.0405;

    public static OperationalEvent AquaticTransfer(DateTimeOffset now) => new()
    {
        Id = "PAE-001:SYNTHETIC-1847",
        Code = "PAE-001",
        Title = "POSSIBLE AQUATIC TRANSFER EVENT",
        VehicleId = "SYNTHETIC-1847",
        RouteId = "199-SYNTHETIC",
        Latitude = Lat,
        Longitude = Lon,
        Since = now.AddMinutes(-6),
        LastSeen = now,
        RecommendedAction = "Further investigation",
        Synthetic = true,
        Evidence =
        [
            new(EvidenceLayer.Observed, "Reported position", $"{Lat:0.00000}, {Lon:0.00000}"),
            new(EvidenceLayer.Observed, "Stop status", "IN_TRANSIT_TO"),
            new(EvidenceLayer.Derived, "Waterway relationship", "Inside Brisbane River"),
            new(EvidenceLayer.Derived, "Nearest vehicle bridge", "Bradfield Highway / Story Bridge (704 m)"),
            new(EvidenceLayer.Derived, "Vessel route", "No"),
            new(EvidenceLayer.Derived, "Current velocity", "4.2 km/h (downstream)"),
        ],
    };
}
