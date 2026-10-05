namespace SignalGarden.Core.Models;

/// <summary>
/// Something about the network worth an operator's attention, with the evidence
/// that produced it. Events are derived on demand from recent observations plus
/// reference layers (cafés, waterways, bridges); they aren't stored yet.
/// </summary>
/// <remarks>
/// The evidence list is the point: an operator should be able to see what was
/// observed, what we calculated from it, and (later) what a model inferred —
/// and never have to take a bare verdict on trust.
/// </remarks>
public sealed record OperationalEvent
{
    /// <summary>Stable for the life of the event, e.g. "RFE-001:vehicle:2026-10-05T01:20:00Z".</summary>
    public required string Id { get; init; }

    /// <summary>Event type code, e.g. "PAE-001" (Possible Aquatic Transfer Event).</summary>
    public required string Code { get; init; }

    /// <summary>Operator-facing headline, e.g. "POSSIBLE REFRESHMENT EVENT".</summary>
    public required string Title { get; init; }

    public required string VehicleId { get; init; }
    public string? RouteId { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }

    /// <summary>When the condition began (e.g. when the vehicle stopped moving).</summary>
    public required DateTimeOffset Since { get; init; }

    /// <summary>The latest report that still shows the condition.</summary>
    public required DateTimeOffset LastSeen { get; init; }

    public required IReadOnlyList<Evidence> Evidence { get; init; }

    public required string RecommendedAction { get; init; }

    /// <summary>
    /// True for staged demo scenarios. Synthetic events are always labelled as
    /// such in the UI — a fabricated event must never pass as an observed one.
    /// </summary>
    public bool Synthetic { get; init; }
}

/// <summary>One line of an event's evidence trail.</summary>
public sealed record Evidence(EvidenceLayer Layer, string Label, string Value);

/// <summary>
/// Where a piece of evidence came from. Kept separate so the UI can show the
/// difference between a fact, a calculation, and a judgement.
/// </summary>
public enum EvidenceLayer
{
    /// <summary>Straight from the feed, e.g. a reported position or stop status.</summary>
    Observed,
    /// <summary>Calculated deterministically, e.g. a distance or a dwell time.</summary>
    Derived,
    /// <summary>A model's judgement, with its probability (e.g. Jev). Not used yet.</summary>
    Inferred,
}
