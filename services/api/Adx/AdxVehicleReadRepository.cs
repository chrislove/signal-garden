using System.Data;
using SignalGarden.Core.Abstractions;
using SignalGarden.Core.Models;

namespace SignalGarden.Api.Adx;

/// <summary>
/// Reads vehicle observations from ADX with KQL. Endpoints only see
/// <see cref="IVehicleReadRepository"/>; Kusto stays inside this Adx folder.
/// </summary>
/// <remarks>
/// The API owns its queries (inline below) rather than calling the stored
/// functions in <c>infra/adx/schema.kql</c>, so a query change ships with the code
/// that depends on it.
/// </remarks>
public sealed class AdxVehicleReadRepository(AdxQueryClient adx, TimeSpan latestWindow)
    : IVehicleReadRepository
{
    // "Latest" means latest *recent* report: filtering by time first lets ADX skip
    // old data entirely, and hides vehicles whose last report is hours stale.
    private const string LatestQuery = """
        declare query_parameters(routeFilter:string, lookback:timespan);
        VehicleObservations
        | where EventTime > ago(lookback)
        | where isempty(routeFilter) or RouteId == routeFilter
        | summarize arg_max(EventTime, *) by VehicleId
        """;

    // The worker re-sends a vehicle's last report until it reports again, so
    // collapse exact repeats to keep the trail honest.
    private const string HistoryQuery = """
        declare query_parameters(vehicle:string, window:timespan);
        VehicleObservations
        | where EventTime > ago(window) and VehicleId == vehicle
        | summarize arg_min(IngestionTime, *) by EventTime
        | order by EventTime asc
        """;

    public Task<IReadOnlyList<VehicleObservation>> GetLatestPositionsAsync(
        string? routeId = null,
        CancellationToken cancellationToken = default) =>
        adx.QueryAsync(LatestQuery, new Dictionary<string, object>
        {
            ["routeFilter"] = routeId ?? "",
            ["lookback"] = latestWindow,
        }, Map, cancellationToken);

    public Task<IReadOnlyList<VehicleObservation>> GetVehicleHistoryAsync(
        string vehicleId,
        TimeSpan window,
        CancellationToken cancellationToken = default) =>
        adx.QueryAsync(HistoryQuery, new Dictionary<string, object>
        {
            ["vehicle"] = vehicleId,
            ["window"] = window,
        }, Map, cancellationToken);

    private static VehicleObservation Map(IDataReader r) => new()
    {
        EventTime = r.Utc("EventTime"),
        IngestionTime = r.Utc("IngestionTime"),
        VehicleId = r.GetString(r.GetOrdinal("VehicleId")),
        TripId = r.StringOrNull("TripId"),
        RouteId = r.StringOrNull("RouteId"),
        Latitude = r.GetDouble(r.GetOrdinal("Latitude")),
        Longitude = r.GetDouble(r.GetOrdinal("Longitude")),
        Bearing = r.DoubleOrNull("Bearing"),
        Speed = r.DoubleOrNull("Speed"),
        CurrentStopSequence = r.IntOrNull("CurrentStopSequence"),
        StopId = r.StringOrNull("StopId"),
        CurrentStatus = r.StringOrNull("CurrentStatus"),
        Source = r.GetString(r.GetOrdinal("Source")),
    };
}
