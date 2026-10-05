using SignalGarden.Core.Abstractions;
using SignalGarden.Core.Models;
using SignalGarden.Decisions.Jev;

namespace SignalGarden.Api.Inference;

/// <summary>
/// Asks Jev what an event most likely is: one <c>choice</c> question per event
/// type, judged against the event's raw facts.
/// </summary>
/// <remarks>
/// Jev can only pick from the options we give it — it can't invent an
/// explanation — and it returns the full probability spread, so a torn verdict
/// shows up as a torn verdict. The facts stay ours; Jev only adds an opinion.
/// </remarks>
public sealed class JevEventAssessor(IDecisionEngine jev) : IEventAssessor
{
    private const string Question = "explanation";
    private static readonly TimeZoneInfo Brisbane = TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane");

    // What we ask about each kind of event. Options are written as plain
    // descriptions because that's what Jev weighs against the facts.
    private static readonly Dictionary<string, (string Instructions, Dictionary<string, string> Options)> Questions = new()
    {
        ["RFE-001"] = (
            "A public transport vehicle in Brisbane has been stationary for several minutes, " +
            "not at a stop, close to a café. What is the most likely explanation?",
            new()
            {
                ["refreshment"] = "The driver is on a break near the café (coffee, food, rest)",
                ["layover"] = "Scheduled layover or recovery time at or near the end of a route",
                ["traffic"] = "Held up by traffic, roadworks, lights or an incident",
                ["breakdown"] = "Vehicle fault or breakdown",
                ["data_anomaly"] = "GPS or feed error: the vehicle isn't really there, or isn't really stopped",
            }),
        ["PAE-001"] = (
            "A land vehicle's reported position is inside a river or waterway polygon. " +
            "What is the most likely explanation?",
            new()
            {
                ["aquatic_transfer"] = "The vehicle has genuinely entered the water",
                ["gps_anomaly"] = "Position error: the vehicle is really on the bank or the road",
                ["missing_bridge"] = "It is on a bridge or crossing that is missing from the map data",
                ["unrecognised_ferry"] = "It is actually a ferry or water service not recognised as one",
            }),
    };

    public async Task<Assessment?> AssessAsync(
        OperationalEvent operationalEvent,
        CancellationToken cancellationToken = default)
    {
        if (!Questions.TryGetValue(operationalEvent.Code, out var question)) return null;

        var local = TimeZoneInfo.ConvertTime(operationalEvent.LastSeen, Brisbane);
        var state = new Dictionary<string, object?>(operationalEvent.Facts)
        {
            ["event"] = operationalEvent.Title.ToLowerInvariant(),
            ["local_time"] = local.ToString("HH:mm"),
            ["day_of_week"] = local.DayOfWeek.ToString(),
            ["minutes_since_condition_began"] =
                Math.Round((operationalEvent.LastSeen - operationalEvent.Since).TotalMinutes, 1),
        };

        var response = await jev.EvaluateAsync(
            new DecisionRequest(state).Choice(Question, question.Instructions, question.Options),
            cancellationToken);
        var answer = response.AsChoice(Question);

        return new Assessment(
            Verdict: answer.Choice,
            Description: question.Options[answer.Choice],
            Probability: answer.Probabilities.GetValueOrDefault(answer.Choice),
            Confidence: answer.Confidence,
            Options: answer.Probabilities
                .OrderByDescending(p => p.Value)
                .Select(p => new AssessmentOption(p.Key, question.Options.GetValueOrDefault(p.Key, p.Key), p.Value))
                .ToList(),
            Model: response.Model);
    }
}
