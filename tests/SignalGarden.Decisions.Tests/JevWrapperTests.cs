using System.Net;
using System.Text;
using System.Text.Json;
using SignalGarden.Decisions.Jev;

namespace SignalGarden.Decisions.Tests;

public class JevWrapperTests
{
    // A real Jev response captured from a live call, used to prove parsing.
    private const string SampleResponse = """
    {
      "model": "jev-1.13.0",
      "answers": {
        "operational_state": {"type":"choice","choice":"degraded","confidence":0.4,
          "probabilities":{"normal":0.0,"degraded":0.55,"disrupted":0.43,"severe":0.02}},
        "commuter_impact": {"type":"score","score":1.95,"confidence":0.92,
          "legend":{"0":"Barely noticeable","1":"Mild","2":"Serious","3":"Widespread"},
          "probabilities":{"0":0.0,"1":0.06,"2":0.92,"3":0.02}},
        "weather_driven": {"type":"noul","noul":0.69}
      },
      "usage": {"input_tokens":477,"output_tokens":88}
    }
    """;

    private static DecisionRequest SampleRequest() =>
        new DecisionRequest(new { active_vehicles = 320, weather = "heavy rain" })
            .Choice("operational_state", "Overall network state", new Dictionary<string, string>
            {
                ["normal"] = "Running to schedule",
                ["degraded"] = "Some delays",
                ["disrupted"] = "Significant delays",
                ["severe"] = "Emergency",
            })
            .Score("commuter_impact", "How badly commuters are affected",
                ["Barely noticeable", "Mild", "Serious", "Widespread"])
            .Noul("weather_driven", "The disruption is primarily weather");

    [Fact]
    public async Task Serialises_request_with_model_state_and_typed_questions()
    {
        var handler = new CapturingHandler(SampleResponse);
        var engine = new JevDecisionEngine(new HttpClient(handler), new JevOptions { ApiKey = "test" });

        await engine.EvaluateAsync(SampleRequest());

        using var doc = JsonDocument.Parse(handler.CapturedBody!);
        var root = doc.RootElement;

        Assert.Equal("jev-latest", root.GetProperty("model").GetString());
        Assert.Equal(320, root.GetProperty("state").GetProperty("active_vehicles").GetInt32());

        var questions = root.GetProperty("questions");
        // choice → criteria is an object keyed by option name
        var choice = questions.GetProperty("operational_state");
        Assert.Equal("choice", choice.GetProperty("type").GetString());
        Assert.Equal("Running to schedule", choice.GetProperty("criteria").GetProperty("normal").GetString());
        // score → criteria is an ordered array
        var score = questions.GetProperty("commuter_impact");
        Assert.Equal("score", score.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Array, score.GetProperty("criteria").ValueKind);
        // noul → criteria omitted entirely
        var noul = questions.GetProperty("weather_driven");
        Assert.Equal("noul", noul.GetProperty("type").GetString());
        Assert.False(noul.TryGetProperty("criteria", out _));
    }

    [Fact]
    public async Task Sends_bearer_auth_header()
    {
        var handler = new CapturingHandler(SampleResponse);
        var engine = new JevDecisionEngine(new HttpClient(handler), new JevOptions { ApiKey = "secret-key" });

        await engine.EvaluateAsync(SampleRequest());

        Assert.Equal("Bearer", handler.CapturedRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("secret-key", handler.CapturedRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Parses_choice_score_and_noul_answers()
    {
        var handler = new CapturingHandler(SampleResponse);
        var engine = new JevDecisionEngine(new HttpClient(handler), new JevOptions { ApiKey = "test" });

        var resp = await engine.EvaluateAsync(SampleRequest());

        Assert.Equal("jev-1.13.0", resp.Model);

        var choice = resp.AsChoice("operational_state");
        Assert.Equal("degraded", choice.Choice);
        Assert.Equal(0.4, choice.Confidence, 3);
        Assert.Equal(0.43, choice.Probabilities["disrupted"], 3);

        var score = resp.AsScore("commuter_impact");
        Assert.Equal(1.95, score.Score, 3);
        Assert.Equal("Serious", score.Legend["2"]);
        Assert.Equal(0.92, score.Probabilities["2"], 3);

        Assert.Equal(0.69, resp.AsNoul("weather_driven"), 3);

        Assert.Equal(477, resp.Usage!.InputTokens);
        Assert.Equal(88, resp.Usage.OutputTokens);
    }

    [Fact]
    public async Task Throws_JevException_on_error_status()
    {
        var handler = new CapturingHandler("{\"detail\":\"bad\"}", HttpStatusCode.UnprocessableEntity);
        var engine = new JevDecisionEngine(new HttpClient(handler), new JevOptions { ApiKey = "test" });

        var ex = await Assert.ThrowsAsync<JevException>(() => engine.EvaluateAsync(SampleRequest()));
        Assert.Contains("422", ex.Message);
    }

    private sealed class CapturingHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public string? CapturedBody { get; private set; }
        public HttpRequestMessage? CapturedRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CapturedRequest = request;
            CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
