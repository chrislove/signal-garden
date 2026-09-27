using SignalGarden.Core.Models;

namespace SignalGarden.Core.Abstractions;

/// <summary>
/// Read-side access to vehicle observations. The API depends on this
/// abstraction; the ADX-backed implementation (KQL queries) is wired in later.
/// </summary>
public interface IVehicleReadRepository
{
    /// <summary>
    /// The most recent observation per vehicle currently in service.
    /// Backed by a KQL query using <c>summarize arg_max(EventTime, *) by VehicleId</c>.
    /// </summary>
    Task<IReadOnlyList<VehicleObservation>> GetLatestPositionsAsync(
        string? routeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The position history for a single vehicle over a time window,
    /// oldest first — the basis for the "evidence trail" and delay charts.
    /// </summary>
    Task<IReadOnlyList<VehicleObservation>> GetVehicleHistoryAsync(
        string vehicleId,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}
