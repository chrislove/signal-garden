using SignalGarden.Core.Abstractions;
using SignalGarden.Core.Models;

namespace SignalGarden.Core.Contracts;

/// <summary>
/// <c>/api/events</c> response: current events plus the context of what was
/// deliberately not raised, so "0 events" can be told apart from "not looking".
/// </summary>
public sealed record EventsResponseDto(
    IReadOnlyList<EventDto> Events,
    int VesselsInWater,
    int VehiclesOnBridges,
    DateTimeOffset ScannedAt)
{
    public static EventsResponseDto From(EventScan scan) => new(
        scan.Events.Select(EventDto.From).ToList(),
        scan.VesselsInWater,
        scan.VehiclesOnBridges,
        scan.ScannedAt);
}

/// <summary>One operational event as the dashboard shows it.</summary>
public sealed record EventDto
{
    public required string Id { get; init; }
    public required string Code { get; init; }
    public required string Title { get; init; }
    public required string VehicleId { get; init; }

    /// <summary>Rider-facing route, e.g. "199" (see <see cref="LiveVehicleDto.Route"/>).</summary>
    public string? Route { get; init; }

    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required DateTimeOffset Since { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
    public required int DurationSeconds { get; init; }
    public required string RecommendedAction { get; init; }
    public required bool Synthetic { get; init; }
    public required IReadOnlyList<EvidenceDto> Evidence { get; init; }

    public static EventDto From(OperationalEvent e) => new()
    {
        Id = e.Id,
        Code = e.Code,
        Title = e.Title,
        VehicleId = e.VehicleId,
        Route = e.RouteId?.Split('-')[0],
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        Since = e.Since,
        LastSeen = e.LastSeen,
        DurationSeconds = (int)Math.Max(0, (e.LastSeen - e.Since).TotalSeconds),
        RecommendedAction = e.RecommendedAction,
        Synthetic = e.Synthetic,
        Evidence = e.Evidence.Select(x => new EvidenceDto(x.Layer.ToString().ToUpperInvariant(), x.Label, x.Value)).ToList(),
    };
}

/// <param name="Layer">OBSERVED, DERIVED or INFERRED.</param>
public sealed record EvidenceDto(string Layer, string Label, string Value);
