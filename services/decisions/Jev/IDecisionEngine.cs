namespace SignalGarden.Decisions.Jev;

/// <summary>
/// Evaluates a <see cref="DecisionRequest"/> and returns Jev's typed verdicts.
/// The "operations brain" depends on this abstraction so it can be unit-tested
/// with fixtures instead of live calls.
/// </summary>
public interface IDecisionEngine
{
    Task<DecisionResponse> EvaluateAsync(DecisionRequest request, CancellationToken cancellationToken = default);
}
