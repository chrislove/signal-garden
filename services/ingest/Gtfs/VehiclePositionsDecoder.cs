using SignalGarden.Core.Models;
using TransitRealtime;

namespace SignalGarden.Ingest.Gtfs;

/// <summary>
/// Maps a decoded GTFS-Realtime VehiclePositions <see cref="FeedMessage"/> into our
/// normalised <see cref="VehicleObservation"/> records.
/// </summary>
/// <remarks>
/// The realtime feed is proto2, so most fields are optional — we treat anything
/// absent as <c>null</c> rather than a default. Entities that aren't vehicle
/// positions, or that carry no coordinates / no vehicle id, are skipped: they
/// can't be plotted or keyed, so they'd just be noise downstream.
/// </remarks>
public static class VehiclePositionsDecoder
{
    public static IReadOnlyList<VehicleObservation> ToObservations(FeedMessage feed, string source)
    {
        var ingestionTime = DateTimeOffset.UtcNow;
        var results = new List<VehicleObservation>(feed.Entity.Count);

        foreach (var entity in feed.Entity)
        {
            // FeedEntity.Vehicle is the VehiclePosition (null for trip-update/alert entities).
            var vp = entity.Vehicle;
            if (vp is null || vp.Position is null)
                continue;

            var position = vp.Position;

            // Prefer the vehicle descriptor id; fall back to the entity id.
            var vehicleId = vp.Vehicle is { HasId: true } v ? v.Id : entity.Id;
            if (string.IsNullOrEmpty(vehicleId))
                continue;

            var eventTime = vp.HasTimestamp && vp.Timestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds((long)vp.Timestamp)
                : ingestionTime;

            results.Add(new VehicleObservation
            {
                EventTime = eventTime,
                IngestionTime = ingestionTime,
                VehicleId = vehicleId,
                TripId = vp.Trip is { HasTripId: true } trip ? trip.TripId : null,
                RouteId = vp.Trip is { HasRouteId: true } route ? route.RouteId : null,
                Latitude = position.Latitude,
                Longitude = position.Longitude,
                Bearing = position.HasBearing ? position.Bearing : null,
                Speed = position.HasSpeed ? position.Speed : null,
                CurrentStopSequence = vp.HasCurrentStopSequence ? (int)vp.CurrentStopSequence : null,
                Source = source,
            });
        }

        return results;
    }
}
