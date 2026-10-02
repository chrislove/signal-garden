using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalGarden.Decisions.Jev;

/// <summary>
/// A Jev response. Raw answers are kept as <see cref="JsonElement"/> keyed by the
/// question name; the typed accessors (<see cref="AsChoice"/>, <see cref="AsScore"/>,
/// <see cref="AsNoul"/>) read the fields for the primitive you asked. Reading a
/// question with the wrong accessor, or a name you didn't ask, throws.
/// </summary>
public sealed class DecisionResponse
{
    public string Model { get; init; } = "";

    public IReadOnlyDictionary<string, JsonElement> Answers { get; init; }
        = new Dictionary<string, JsonElement>();

    public JevUsage? Usage { get; init; }

    /// <summary>Read a <c>choice</c> answer: selected option + full distribution + confidence.</summary>
    public ChoiceAnswer AsChoice(string name)
    {
        var a = Get(name);
        return new ChoiceAnswer(
            a.GetProperty("choice").GetString()!,
            a.GetProperty("probabilities").Deserialize<Dictionary<string, double>>()!,
            a.GetProperty("confidence").GetDouble());
    }

    /// <summary>Read a <c>score</c> answer: fractional position + level legend + distribution.</summary>
    public ScoreAnswer AsScore(string name)
    {
        var a = Get(name);
        return new ScoreAnswer(
            a.GetProperty("score").GetDouble(),
            a.GetProperty("legend").Deserialize<Dictionary<string, string>>()!,
            a.GetProperty("probabilities").Deserialize<Dictionary<string, double>>()!,
            a.GetProperty("confidence").GetDouble());
    }

    /// <summary>Read a <c>noul</c> answer: probability the statement is true (0–1).</summary>
    public double AsNoul(string name) => Get(name).GetProperty("noul").GetDouble();

    private JsonElement Get(string name) =>
        Answers.TryGetValue(name, out var a)
            ? a
            : throw new KeyNotFoundException($"No answer named '{name}' in the Jev response.");
}

/// <summary>A <c>choice</c> answer.</summary>
public sealed record ChoiceAnswer(
    string Choice,
    IReadOnlyDictionary<string, double> Probabilities,
    double Confidence);

/// <summary>A <c>score</c> answer. <see cref="Score"/> can fall between levels.</summary>
public sealed record ScoreAnswer(
    double Score,
    IReadOnlyDictionary<string, string> Legend,
    IReadOnlyDictionary<string, double> Probabilities,
    double Confidence);

/// <summary>Token usage reported by Jev.</summary>
public sealed record JevUsage(
    [property: JsonPropertyName("input_tokens")] int InputTokens,
    [property: JsonPropertyName("output_tokens")] int OutputTokens);
