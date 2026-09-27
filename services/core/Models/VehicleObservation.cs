namespace SignalGarden.Core.Models;

/// <summary>
/// A single normalised vehicle position, decoded from a GTFS-Realtime
/// VehiclePositions feed entity. This is the shape we ingest into the ADX
/// <c>VehicleObservations</c> table and the shape the API returns to the UI.
/// </summary>
/// <remarks>
/// The realtime feed only carries IDs (route/trip/stop). Human-readable names
/// (route short name, stop name, headsign) come from joining the static GTFS
/// schedule — see <c>data/README.md</c>. Those enrichment fields are optional
/// here and populated later in the pipeline, not by the raw decoder.
/// </remarks>
public sealed record VehicleObservation
{
    /// <summary>When the vehicle reported this position (from the feed's timestamp).</summary>
    public required DateTimeOffset EventTime { get; init; }

    /// <summary>When our ingestion worker received/decoded the record.</summary>
    public required DateTimeOffset IngestionTime { get; init; }

    public required string VehicleId { get; init; }
    public string? TripId { get; init; }
    public string? RouteId { get; init; }

    public required double Latitude { get; init; }
    public required double Longitude { get; init; }

    /// <summary>Degrees clockwise from true north, if reported.</summary>
    public double? Bearing { get; init; }

    /// <summary>Metres per second, if reported.</summary>
    public double? Speed { get; init; }

    public int? CurrentStopSequence { get; init; }

    /// <summary>Feed identifier, e.g. "translink-seq/vehicle-positions".</summary>
    public required string Source { get; init; }
}
