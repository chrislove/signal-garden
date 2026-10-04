using SignalGarden.Core.Models;
using TransitRealtime;

namespace SignalGarden.Ingest;

/// <summary>
/// Turns a raw GTFS-Realtime VehiclePositions payload into normalised
/// <see cref="VehicleObservation"/> records.
/// </summary>
/// <remarks>
/// Kept as a pure function (bytes in, records out) so it can be exercised
/// against a saved feed snapshot without the network or ADX. GTFS-RT is proto2,
/// so optional fields have <c>Has*</c> flags — we use them to keep "not reported"
/// as <c>null</c> rather than a misleading zero.
/// </remarks>
public static class VehiclePositionDecoder
{
    public const string Source = "translink-seq/vehicle-positions";

    public static IReadOnlyList<VehicleObservation> Decode(byte[] payload, DateTimeOffset ingestionTime)
    {
        var feed = FeedMessage.Parser.ParseFrom(payload);

        // Some vehicles omit their own timestamp; fall back to the feed header's.
        var headerTime = feed.Header.HasTimestamp
            ? DateTimeOffset.FromUnixTimeSeconds((long)feed.Header.Timestamp)
            : ingestionTime;

        var observations = new List<VehicleObservation>(feed.Entity.Count);
        foreach (var entity in feed.Entity)
        {
            var vp = entity.Vehicle;

            // Entities without a position (or a vehicle id) can't go on a map — skip them.
            if (vp?.Position is null) continue;
            var vehicleId = vp.Vehicle?.Id;
            if (string.IsNullOrEmpty(vehicleId)) vehicleId = entity.Id;
            if (string.IsNullOrEmpty(vehicleId)) continue;

            observations.Add(new VehicleObservation
            {
                EventTime = vp.HasTimestamp
                    ? DateTimeOffset.FromUnixTimeSeconds((long)vp.Timestamp)
                    : headerTime,
                IngestionTime = ingestionTime,
                VehicleId = vehicleId,
                TripId = NullIfEmpty(vp.Trip?.TripId),
                RouteId = NullIfEmpty(vp.Trip?.RouteId),
                Latitude = vp.Position.Latitude,
                Longitude = vp.Position.Longitude,
                Bearing = vp.Position.HasBearing ? vp.Position.Bearing : null,
                Speed = vp.Position.HasSpeed ? vp.Position.Speed : null,
                CurrentStopSequence = vp.HasCurrentStopSequence ? (int)vp.CurrentStopSequence : null,
                StopId = NullIfEmpty(vp.StopId),
                CurrentStatus = vp.HasCurrentStatus ? StatusName(vp.CurrentStatus) : null,
                Source = Source,
            });
        }

        return observations;
    }

    // proto2 enums default to IN_TRANSIT_TO when absent — hence the HasCurrentStatus
    // check above, so a missing status stays null instead of "moving".
    private static string StatusName(VehiclePosition.Types.VehicleStopStatus status) => status switch
    {
        VehiclePosition.Types.VehicleStopStatus.StoppedAt => "STOPPED_AT",
        VehiclePosition.Types.VehicleStopStatus.IncomingAt => "INCOMING_AT",
        VehiclePosition.Types.VehicleStopStatus.InTransitTo => "IN_TRANSIT_TO",
        _ => status.ToString(),
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
