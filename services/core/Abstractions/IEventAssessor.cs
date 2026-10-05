using SignalGarden.Core.Models;

namespace SignalGarden.Core.Abstractions;

/// <summary>
/// Judges what an event most likely is — the INFERRED layer of its evidence.
/// Kept apart from <see cref="IEventDetector"/> on purpose: detection is
/// deterministic and checkable; assessment is a probabilistic opinion on top.
/// </summary>
public interface IEventAssessor
{
    /// <summary>The assessment, or null if this kind of event isn't assessed.</summary>
    Task<Assessment?> AssessAsync(OperationalEvent operationalEvent, CancellationToken cancellationToken = default);
}
