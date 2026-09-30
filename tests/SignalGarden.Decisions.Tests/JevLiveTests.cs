using SignalGarden.Decisions.Jev;

namespace SignalGarden.Decisions.Tests;

/// <summary>
/// Real calls to Jev. Skipped unless <c>TYPESAFE_API_KEY</c> is set, so the normal
/// unit run stays hermetic. Run locally with the key exported to exercise the API.
/// </summary>
public class JevLiveTests
{
    private static string? ApiKey => Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");

    [SkippableFact]
    public async Task Evaluates_a_brisbane_network_state()
    {
        Skip.If(string.IsNullOrWhiteSpace(ApiKey), "TYPESAFE_API_KEY not set.");

        var engine = new JevDecisionEngine(new HttpClient(), new JevOptions { ApiKey = ApiKey! });

        var request = new DecisionRequest(new
            {
                active_vehicles = 320,
                typical_active_for_time = 500,
                avg_delay_min = 8.5,
                service_alerts = 4,
                weather = "heavy rain, wind",
            })
            .Choice("operational_state", "Overall operational state of the transit network right now",
                new Dictionary<string, string>
                {
                    ["normal"] = "Running to schedule",
                    ["degraded"] = "Some delays but manageable",
                    ["disrupted"] = "Significant delays affecting many riders",
                    ["severe"] = "Major breakdown or weather emergency",
                })
            .Score("commuter_impact", "How badly are peak commuters affected",
                ["Barely noticeable", "Mild inconvenience", "Serious disruption", "Widespread failure"])
            .Noul("weather_driven", "The disruption is primarily caused by weather");

        var resp = await engine.EvaluateAsync(request);

        // We assert shape/ranges, not exact values (the model can move).
        var state = resp.AsChoice("operational_state");
        Assert.Contains(state.Choice, new[] { "normal", "degraded", "disrupted", "severe" });
        Assert.InRange(state.Confidence, 0.0, 1.0);
        Assert.Equal(1.0, state.Probabilities.Values.Sum(), 1);

        var impact = resp.AsScore("commuter_impact");
        Assert.InRange(impact.Score, 0.0, 3.0);

        Assert.InRange(resp.AsNoul("weather_driven"), 0.0, 1.0);
    }
}
