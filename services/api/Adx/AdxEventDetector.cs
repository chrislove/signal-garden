using System.Data;
using System.Globalization;
using SignalGarden.Core.Abstractions;
using SignalGarden.Core.Models;

namespace SignalGarden.Api.Adx;

/// <summary>
/// Detects operational events with KQL: ADX joins recent vehicle reports against
/// the OpenStreetMap reference layers (cafés, river polygons, bridges) and hands
/// back candidates with the numbers that justify them.
/// </summary>
/// <remarks>
/// Everything here is deterministic — distances, dwell times, point-in-polygon.
/// Judgement ("is this really a coffee break?") is a separate layer: see
/// <see cref="IEventAssessor"/>.
/// Note <c>latest</c> is a reserved word in KQL, hence <c>current</c> below.
/// </remarks>
public sealed class AdxEventDetector(AdxQueryClient adx, EventDetectionOptions options) : IEventDetector
{
    // RFE-001: stationary for a while, not reported at a stop, within a short walk
    // of a café — with where that is relative to the vehicle's own route.
    private const string RefreshmentQuery = """
        declare query_parameters(lookback:timespan, minDwell:timespan, stillRadius:real, cafeRadius:real);
        // Reports in the window, with the worker's repeat-sends collapsed.
        let recent = VehicleObservations
            | where EventTime > ago(lookback)
            | summarize arg_min(IngestionTime, *) by VehicleId, EventTime;
        // Where each vehicle is now (only vehicles that reported in the last 3 minutes).
        let current = recent
            | summarize arg_max(EventTime, *) by VehicleId
            | where EventTime > ago(3m)
            | project VehicleId, LastTime = EventTime, LastLat = Latitude, LastLon = Longitude,
                      RouteId, TripId, LastStopId = StopId, LastStatus = CurrentStatus;
        // How long has it been within stillRadius of where it is now?
        let dwelling = recent
            | join kind=inner current on VehicleId
            | extend Away = geo_distance_2points(Longitude, Latitude, LastLon, LastLat)
            | summarize LastAway = maxif(EventTime, Away > stillRadius), FirstSeen = min(EventTime),
                        Reports = count()
                  by VehicleId, LastTime, LastLat, LastLon, RouteId, TripId, LastStopId, LastStatus
            | extend StillSince = iff(isnull(LastAway), FirstSeen, LastAway)
            | extend Dwell = LastTime - StillSince
            | where Dwell >= minDwell and LastStatus != "STOPPED_AT"
            | extend Cell = geo_point_to_s2cell(LastLon, LastLat, 16);
        // Cafés indexed by S2 cell *and* its neighbours (~150 m cells), so joining on
        // cell finds every café in range without comparing each vehicle with all of them.
        let cafes = Cafes
            | extend Cell = geo_point_to_s2cell(Longitude, Latitude, 16)
            | mv-expand Cell = array_concat(pack_array(Cell), geo_s2cell_neighbors(Cell)) to typeof(string);
        let candidates = dwelling
            | join kind=inner cafes on Cell
            | extend CafeDistance = geo_distance_2points(LastLon, LastLat, Longitude, Latitude)
            | where CafeDistance <= cafeRadius
            | summarize arg_min(CafeDistance, CafeName = Name)
                  by VehicleId, LastTime, LastLat, LastLon, RouteId, TripId, LastStopId, LastStatus, StillSince, Dwell, Reports;
        // Where is it relative to its *own* route? Every stop the route serves
        // (static GTFS), measured from the vehicle: the nearest one, and the
        // nearest terminus (first/last stop of any of the route's trips).
        let routeStops = candidates
            | project VehicleId, RouteId, LastLat, LastLon
            | join kind=inner RouteStops on RouteId
            | join kind=inner (Stops | project StopId, StopName = Name, StopLat = Latitude, StopLon = Longitude) on StopId
            | extend StopDistance = geo_distance_2points(LastLon, LastLat, StopLon, StopLat);
        let nearestStop = routeStops
            | summarize arg_min(StopDistance, NearestStopName = StopName) by VehicleId
            | project VehicleId, NearestStopDistance = StopDistance, NearestStopName;
        let nearestTerminus = routeStops
            | where IsTerminus
            | summarize arg_min(StopDistance, TerminusName = StopName) by VehicleId
            | project VehicleId, TerminusDistance = StopDistance, TerminusName;
        candidates
        | join kind=leftouter nearestStop on VehicleId
        | join kind=leftouter nearestTerminus on VehicleId
        | project-away VehicleId1, VehicleId2
        """;

    // PAE-001: reported position inside a river polygon — unless it's a ferry, or on a bridge.
    private const string AquaticQuery = """
        declare query_parameters(bridgeRadius:real);
        let current = VehicleObservations
            | where EventTime > ago(3m)
            | summarize arg_max(EventTime, *) by VehicleId;
        // ~800 vehicles x ~95 polygons is small enough to simply test them all.
        let wet = current
            | extend k = 1
            | join kind=inner (WaterBodies | project WaterName = Name, Geometry, k = 1) on k
            | where geo_point_in_polygon(Longitude, Latitude, Geometry)
            | project-away k*, Geometry;
        // A vehicle "in" the river is usually on a bridge over it: find the nearest one.
        let nearestBridge = wet
            | extend k = 1
            | join kind=inner (Bridges | project BridgeName = Name, Line = Geometry, k = 1) on k
            | extend BridgeDistance = geo_distance_point_to_line(Longitude, Latitude, Line)
            | summarize arg_min(BridgeDistance, BridgeName) by VehicleId;
        wet
        | join kind=leftouter nearestBridge on VehicleId
        | extend Classification = case(
              RouteId startswith "F", "VESSEL",
              isnotnull(BridgeDistance) and BridgeDistance <= bridgeRadius, "BRIDGE",
              "AQUATIC")
        | project VehicleId, EventTime, Latitude, Longitude, RouteId, StopId, CurrentStatus,
                  WaterName, BridgeName, BridgeDistance, Classification
        """;

    public async Task<EventScan> DetectAsync(CancellationToken cancellationToken = default)
    {
        var refreshment = adx.QueryAsync(RefreshmentQuery, new Dictionary<string, object>
        {
            ["lookback"] = options.Lookback,
            ["minDwell"] = options.MinDwell,
            ["stillRadius"] = options.StillRadiusMetres,
            ["cafeRadius"] = options.CafeRadiusMetres,
        }, ToRefreshmentEvent, cancellationToken);

        var aquatic = adx.QueryAsync(AquaticQuery, new Dictionary<string, object>
        {
            ["bridgeRadius"] = options.BridgeRadiusMetres,
        }, ReadAquatic, cancellationToken);

        await Task.WhenAll(refreshment, aquatic);

        var wet = aquatic.Result;
        var events = refreshment.Result
            .Concat(wet.Where(w => w.Classification == "AQUATIC").Select(w => w.Event))
            .OrderByDescending(e => e.LastSeen - e.Since)
            .ToList();

        return new EventScan(
            events,
            VesselsInWater: wet.Count(w => w.Classification == "VESSEL"),
            VehiclesOnBridges: wet.Count(w => w.Classification == "BRIDGE"),
            ScannedAt: DateTimeOffset.UtcNow);
    }

    private OperationalEvent ToRefreshmentEvent(IDataReader r)
    {
        var vehicleId = r.GetString(r.GetOrdinal("VehicleId"));
        var since = r.Utc("StillSince");
        var lastSeen = r.Utc("LastTime");
        var dwell = r.Span("Dwell");
        var lat = r.GetDouble(r.GetOrdinal("LastLat"));
        var lon = r.GetDouble(r.GetOrdinal("LastLon"));
        var status = r.StringOrNull("LastStatus");
        var cafeName = r.GetString(r.GetOrdinal("CafeName"));
        var cafeDistance = r.GetDouble(r.GetOrdinal("CafeDistance"));
        var reports = r.GetInt64(r.GetOrdinal("Reports"));
        var routeId = r.StringOrNull("RouteId");
        var tripId = r.StringOrNull("TripId");
        var stopDistance = r.DoubleOrNull("NearestStopDistance");
        var stopName = r.StringOrNull("NearestStopName");
        var terminusDistance = r.DoubleOrNull("TerminusDistance");
        var terminusName = r.StringOrNull("TerminusName");
        // The feed's status only says what the vehicle reported; geometry says
        // whether it's actually standing at one of its route's stops.
        bool? atRouteStop = stopDistance is { } sd ? sd <= options.AtStopRadiusMetres : null;
        var unplanned = tripId?.StartsWith("UNPLANNED", StringComparison.Ordinal) == true;

        return new OperationalEvent
        {
            Id = $"RFE-001:{vehicleId}:{since:O}",
            Code = "RFE-001",
            Title = "POSSIBLE REFRESHMENT EVENT",
            VehicleId = vehicleId,
            RouteId = routeId,
            Latitude = lat,
            Longitude = lon,
            Since = since,
            LastSeen = lastSeen,
            RecommendedAction = "Monitor until departure",
            Facts = new Dictionary<string, object?>
            {
                ["route"] = routeId?.Split('-')[0],
                ["stationary_minutes"] = Math.Round(dwell.TotalMinutes, 1),
                ["reported_at_a_stop"] = false,
                ["stop_status"] = status,
                ["at_a_stop_on_its_route"] = atRouteStop,
                ["nearest_route_stop"] = stopName,
                ["nearest_route_stop_m"] = stopDistance is { } d1 ? Math.Round(d1) : null,
                ["nearest_route_terminus"] = terminusName,
                ["nearest_route_terminus_m"] = terminusDistance is { } d2 ? Math.Round(d2) : null,
                ["unplanned_trip"] = unplanned,
                ["nearest_cafe"] = cafeName,
                ["cafe_distance_m"] = Math.Round(cafeDistance),
                ["position_reports_in_window"] = reports,
            },
            Evidence =
            [
                new(EvidenceLayer.Observed, "Reported position", Position(lat, lon)),
                new(EvidenceLayer.Observed, "Stop status", status ?? "not reported"),
                new(EvidenceLayer.Observed, "Reports in window", reports.ToString(CultureInfo.InvariantCulture)),
                new(EvidenceLayer.Derived, "Stationary",
                    $"{dwell.TotalMinutes:0.0} min within {options.StillRadiusMetres:0} m"),
                new(EvidenceLayer.Observed, "Trip", unplanned ? "Unplanned (not in the timetable)" : "Timetabled"),
                new(EvidenceLayer.Derived, "At a stop on its route", (atRouteStop, stopName, stopDistance) switch
                {
                    (null, _, _) => "unknown (route not in timetable)",
                    (true, var n, var d) => $"Yes: {n} ({d:0} m)",
                    (false, var n, var d) => $"No: nearest is {n} ({d:0} m)",
                }),
                new(EvidenceLayer.Derived, "Route terminus", terminusDistance is { } td
                    ? $"{terminusName} ({Distance(td)})"
                    : "unknown"),
                new(EvidenceLayer.Derived, "Nearest café", $"{cafeName} ({cafeDistance:0} m)"),
            ],
        };
    }

    private sealed record WetVehicle(string Classification, OperationalEvent Event);

    private static WetVehicle ReadAquatic(IDataReader r)
    {
        var vehicleId = r.GetString(r.GetOrdinal("VehicleId"));
        var seen = r.Utc("EventTime");
        var lat = r.GetDouble(r.GetOrdinal("Latitude"));
        var lon = r.GetDouble(r.GetOrdinal("Longitude"));
        var bridge = r.StringOrNull("BridgeName");
        var bridgeDistance = r.DoubleOrNull("BridgeDistance");
        var water = r.StringOrNull("WaterName");
        var status = r.StringOrNull("CurrentStatus");
        var routeId = r.StringOrNull("RouteId");

        var evt = new OperationalEvent
        {
            Id = $"PAE-001:{vehicleId}:{seen:O}",
            Code = "PAE-001",
            Title = "POSSIBLE AQUATIC TRANSFER EVENT",
            VehicleId = vehicleId,
            RouteId = routeId,
            Latitude = lat,
            Longitude = lon,
            Since = seen,
            LastSeen = seen,
            RecommendedAction = "Further investigation",
            Facts = new Dictionary<string, object?>
            {
                ["route"] = routeId?.Split('-')[0],
                ["inside_water_body"] = water ?? "unnamed waterway",
                ["nearest_vehicle_bridge"] = bridge,
                ["nearest_bridge_distance_m"] = bridgeDistance is { } bd ? Math.Round(bd) : null,
                ["is_ferry_route"] = false,
                ["stop_status"] = status,
            },
            Evidence =
            [
                new(EvidenceLayer.Observed, "Reported position", Position(lat, lon)),
                new(EvidenceLayer.Observed, "Stop status", status ?? "not reported"),
                new(EvidenceLayer.Derived, "Waterway relationship", $"Inside {water ?? "unnamed waterway"}"),
                new(EvidenceLayer.Derived, "Nearest vehicle bridge", bridgeDistance is { } d
                    ? $"{(string.IsNullOrEmpty(bridge) ? "unnamed" : bridge)} ({d:0} m)"
                    : "none found"),
                new(EvidenceLayer.Derived, "Vessel route", "No"),
            ],
        };
        return new WetVehicle(r.GetString(r.GetOrdinal("Classification")), evt);
    }

    private static string Distance(double metres) =>
        metres < 1000 ? $"{metres:0} m" : string.Create(CultureInfo.InvariantCulture, $"{metres / 1000:0.0} km");

    private static string Position(double lat, double lon) =>
        string.Create(CultureInfo.InvariantCulture, $"{lat:0.00000}, {lon:0.00000}");
}

/// <summary>Detection thresholds. Defaults are a first guess — tune them in config.</summary>
public sealed record EventDetectionOptions
{
    public TimeSpan Lookback { get; init; } = TimeSpan.FromMinutes(20);
    public TimeSpan MinDwell { get; init; } = TimeSpan.FromMinutes(5);
    public double StillRadiusMetres { get; init; } = 40;
    public double CafeRadiusMetres { get; init; } = 50;
    public double BridgeRadiusMetres { get; init; } = 30;

    /// <summary>Within this of one of its route's stops, a vehicle counts as "at a stop".</summary>
    public double AtStopRadiusMetres { get; init; } = 30;
}
