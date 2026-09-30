using System.Text.Json.Serialization;

namespace SignalGarden.Decisions.Jev;

/// <summary>
/// A Jev request: a <see cref="State"/> to judge plus one or more typed questions,
/// each keyed by a name you choose (the same name keys the answer). Build it
/// fluently:
/// <code>
/// var req = new DecisionRequest(state)
///     .Choice("operational_state", "Overall network state", options)
///     .Score("commuter_impact", "How badly commuters are affected", levels)
///     .Noul("weather_driven", "The disruption is primarily weather");
/// </code>
/// Questions are independent — add or remove them freely.
/// </summary>
public sealed class DecisionRequest
{
    private readonly Dictionary<string, JevQuestion> _questions = new();

    /// <param name="state">Any object; serialised to JSON as the state Jev evaluates.</param>
    public DecisionRequest(object state) => State = state;

    public object State { get; }

    public IReadOnlyDictionary<string, JevQuestion> Questions => _questions;

    /// <summary>Pick one of <paramref name="options"/> (name → description).</summary>
    public DecisionRequest Choice(string name, string instructions, IReadOnlyDictionary<string, string> options)
    {
        _questions[name] = new JevQuestion { Type = "choice", Instructions = instructions, Criteria = options };
        return this;
    }

    /// <summary>Position on an ordered spectrum of <paramref name="levels"/> (low → high).</summary>
    public DecisionRequest Score(string name, string instructions, IReadOnlyList<string> levels)
    {
        _questions[name] = new JevQuestion { Type = "score", Instructions = instructions, Criteria = levels };
        return this;
    }

    /// <summary>Is the statement true? Optional <paramref name="criteria"/> clarifies yes/no.</summary>
    public DecisionRequest Noul(string name, string instructions, IReadOnlyDictionary<string, string>? criteria = null)
    {
        _questions[name] = new JevQuestion { Type = "noul", Instructions = instructions, Criteria = criteria };
        return this;
    }
}

/// <summary>Wire shape of a single question. <c>Criteria</c> is a map (choice), a
/// list (score), or null/absent (noul).</summary>
public sealed class JevQuestion
{
    public required string Type { get; init; }
    public required string Instructions { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Criteria { get; init; }
}
