using SignalGarden.Core.Models;

namespace SignalGarden.Core.Abstractions;

/// <summary>
/// Finds operational events in the current state of the network. The ADX-backed
/// implementation does the spatial work in KQL; endpoints only see this.
/// </summary>
public interface IEventDetector
{
    Task<EventScan> DetectAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of one scan: the events, plus the context that explains what was
/// deliberately *not* raised (ferries in the river, buses on bridges).
/// </summary>
public sealed record EventScan(
    IReadOnlyList<OperationalEvent> Events,
    int VesselsInWater,
    int VehiclesOnBridges,
    DateTimeOffset ScannedAt);
