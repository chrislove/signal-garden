using System.Data;
using Azure.Identity;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using SignalGarden.Core.Abstractions;
using SignalGarden.Core.Models;

namespace SignalGarden.Api.Adx;

/// <summary>
/// Reads vehicle observations from ADX with KQL. This is the only place in the
/// API that knows about Kusto — endpoints see <see cref="IVehicleReadRepository"/>.
/// </summary>
/// <remarks>
/// The API owns its queries (inline below) rather than calling the stored
/// functions in <c>infra/adx/schema.kql</c>, so a query change ships with the code
/// that depends on it. User input only ever reaches KQL as a declared query
/// parameter, never by string concatenation — the KQL version of SQL injection
/// protection.
/// </remarks>
public sealed class AdxVehicleReadRepository : IVehicleReadRepository, IDisposable
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

    private readonly ICslQueryProvider _client;
    private readonly string _database;
    private readonly TimeSpan _latestWindow;

    public AdxVehicleReadRepository(string queryUri, string database, TimeSpan latestWindow)
    {
        // Same credential chain as the ingest worker: az login on a laptop,
        // a service principal or managed identity when deployed.
        var connection = new KustoConnectionStringBuilder(queryUri)
            .WithAadAzureTokenCredentialsAuthentication(new DefaultAzureCredential());
        _client = KustoClientFactory.CreateCslQueryProvider(connection);
        _database = database;
        _latestWindow = latestWindow;
    }

    public Task<IReadOnlyList<VehicleObservation>> GetLatestPositionsAsync(
        string? routeId = null,
        CancellationToken cancellationToken = default) =>
        QueryAsync(LatestQuery, new()
        {
            ["routeFilter"] = routeId ?? "",
            ["lookback"] = _latestWindow,
        }, cancellationToken);

    public Task<IReadOnlyList<VehicleObservation>> GetVehicleHistoryAsync(
        string vehicleId,
        TimeSpan window,
        CancellationToken cancellationToken = default) =>
        QueryAsync(HistoryQuery, new()
        {
            ["vehicle"] = vehicleId,
            ["window"] = window,
        }, cancellationToken);

    private async Task<IReadOnlyList<VehicleObservation>> QueryAsync(
        string query,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        var properties = new ClientRequestProperties();
        foreach (var (name, value) in parameters)
        {
            switch (value)
            {
                case string s: properties.SetParameter(name, s); break;
                case TimeSpan t: properties.SetParameter(name, t); break;
                default: throw new ArgumentException($"Unsupported parameter type for '{name}'.");
            }
        }

        using var reader = await _client.ExecuteQueryAsync(_database, query, properties, cancellationToken);
        var results = new List<VehicleObservation>();
        while (reader.Read()) results.Add(Map(reader));
        return results;
    }

    private static VehicleObservation Map(IDataReader r) => new()
    {
        EventTime = new DateTimeOffset(r.GetDateTime(r.GetOrdinal("EventTime")), TimeSpan.Zero),
        IngestionTime = new DateTimeOffset(r.GetDateTime(r.GetOrdinal("IngestionTime")), TimeSpan.Zero),
        VehicleId = r.GetString(r.GetOrdinal("VehicleId")),
        TripId = StringOrNull(r, "TripId"),
        RouteId = StringOrNull(r, "RouteId"),
        Latitude = r.GetDouble(r.GetOrdinal("Latitude")),
        Longitude = r.GetDouble(r.GetOrdinal("Longitude")),
        Bearing = DoubleOrNull(r, "Bearing"),
        Speed = DoubleOrNull(r, "Speed"),
        CurrentStopSequence = r.IsDBNull(r.GetOrdinal("CurrentStopSequence"))
            ? null
            : r.GetInt32(r.GetOrdinal("CurrentStopSequence")),
        StopId = StringOrNull(r, "StopId"),
        CurrentStatus = StringOrNull(r, "CurrentStatus"),
        Source = r.GetString(r.GetOrdinal("Source")),
    };

    // ADX strings are never null — an empty string is its "no value".
    private static string? StringOrNull(IDataReader r, string column)
    {
        var value = r.GetString(r.GetOrdinal(column));
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static double? DoubleOrNull(IDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetDouble(i);
    }

    public void Dispose() => _client.Dispose();
}
