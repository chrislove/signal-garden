using SignalGarden.Core.Models;

namespace SignalGarden.Core.Contracts;

/// <summary>
/// What the dashboard map needs for one vehicle — our public API contract.
/// </summary>
/// <remarks>
/// Deliberately not <see cref="VehicleObservation"/>: the domain model mirrors
/// what we store, this mirrors what a consumer needs. Keeping them separate means
/// a new ADX column doesn't silently become API surface, and the UI gets
/// ready-to-use values (an age in seconds, a display route) instead of
/// re-deriving them. Speed and bearing are absent on purpose: the feed doesn't
/// send them, and an always-null field would only invite misuse.
/// </remarks>
public sealed record LiveVehicleDto
{
    public required string VehicleId { get; init; }

    /// <summary>
    /// Route as a rider would say it, e.g. "66". Derived from the feed's RouteId
    /// ("66-4838" = route + schedule version) until we join static GTFS for
    /// proper names.
    /// </summary>
    public string? Route { get; init; }

    public string? RouteId { get; init; }
    public string? TripId { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public string? StopId { get; init; }

    /// <summary><c>STOPPED_AT</c>, <c>INCOMING_AT</c>, <c>IN_TRANSIT_TO</c>, or null if not reported.</summary>
    public string? Status { get; init; }

    /// <summary>When the vehicle reported this position.</summary>
    public required DateTimeOffset ReportedAt { get; init; }

    /// <summary>Seconds between the report and the API answering — the freshness signal.</summary>
    public required int AgeSeconds { get; init; }

    public static LiveVehicleDto From(VehicleObservation o, DateTimeOffset now) => new()
    {
        VehicleId = o.VehicleId,
        Route = o.RouteId?.Split('-')[0],
        RouteId = o.RouteId,
        TripId = o.TripId,
        Latitude = o.Latitude,
        Longitude = o.Longitude,
        StopId = o.StopId,
        Status = o.CurrentStatus,
        ReportedAt = o.EventTime,
        AgeSeconds = (int)Math.Max(0, (now - o.EventTime).TotalSeconds),
    };
}
