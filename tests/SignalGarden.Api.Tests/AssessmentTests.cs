using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SignalGarden.Api.Inference;
using SignalGarden.Api.Demo;
using SignalGarden.Core.Abstractions;
using SignalGarden.Core.Models;
using SignalGarden.Decisions.Jev;

namespace SignalGarden.Api.Tests;

public class AssessmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 4, 0, 0, TimeSpan.Zero);

    /// <summary>A Jev stand-in that records what it was asked and answers "gps_anomaly".</summary>
    private sealed class FakeJev : IDecisionEngine
    {
        public DecisionRequest? Asked { get; private set; }

        public Task<DecisionResponse> EvaluateAsync(DecisionRequest request, CancellationToken ct = default)
        {
            Asked = request;
            var answer = JsonDocument.Parse("""
                {"type":"choice","choice":"gps_anomaly","confidence":0.24,
                 "probabilities":{"aquatic_transfer":0.43,"gps_anomaly":0.44,"unrecognised_ferry":0.09,"missing_bridge":0.04}}
                """).RootElement;
            return Task.FromResult(new DecisionResponse
            {
                Model = "jev-test",
                Answers = new Dictionary<string, JsonElement> { ["explanation"] = answer },
            });
        }
    }

    [Fact]
    public async Task Jev_sees_the_event_facts_plus_local_time_and_returns_a_sorted_spread()
    {
        var jev = new FakeJev();
        var assessment = await new JevEventAssessor(jev).AssessAsync(SyntheticEvents.AquaticTransfer(Now));

        Assert.NotNull(assessment);
        Assert.Equal("gps_anomaly", assessment.Verdict);
        Assert.Equal(0.44, assessment.Probability, 3);
        Assert.Equal("jev-test", assessment.Model);
        Assert.Equal(["gps_anomaly", "aquatic_transfer", "unrecognised_ferry", "missing_bridge"],
            assessment.Options.Select(o => o.Name));

        var state = Assert.IsType<Dictionary<string, object?>>(jev.Asked!.State);
        Assert.Equal("Brisbane River", state["inside_water_body"]);
        Assert.Equal("14:00", state["local_time"]); // 04:00 UTC = 2 pm in Brisbane
    }

    [Fact]
    public async Task Event_types_without_a_question_are_not_assessed()
    {
        var unknown = SyntheticEvents.AquaticTransfer(Now) with { Code = "XYZ-999" };
        Assert.Null(await new JevEventAssessor(new FakeJev()).AssessAsync(unknown));
    }

    private sealed class SlowAssessor(TimeSpan delay) : IEventAssessor
    {
        public int Calls;

        public async Task<Assessment?> AssessAsync(OperationalEvent e, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Calls);
            await Task.Delay(delay, ct);
            return new Assessment("layover", "Layover", 0.6, 0.5, [], "slow");
        }
    }

    [Fact]
    public async Task A_slow_model_never_holds_up_the_dashboard_and_answers_on_the_next_poll()
    {
        var assessor = new SlowAssessor(EventAssessments.Budget + TimeSpan.FromSeconds(0.5));
        var assessments = new EventAssessments(assessor, NullLogger<EventAssessments>.Instance);
        var events = new[] { SyntheticEvents.AquaticTransfer(Now) };

        var first = await assessments.AttachAsync(events);
        Assert.Null(first.Single().Assessment); // budget ran out; still in flight

        await Task.Delay(TimeSpan.FromSeconds(0.7));
        var second = await assessments.AttachAsync(events);
        Assert.Equal("layover", second.Single().Assessment?.Verdict);
        Assert.Equal(1, assessor.Calls); // the in-flight call was reused, not repeated
    }

    private sealed class BrokenAssessor : IEventAssessor
    {
        public Task<Assessment?> AssessAsync(OperationalEvent e, CancellationToken ct = default) =>
            throw new JevException("Jev is down");
    }

    [Fact]
    public async Task A_failing_model_leaves_events_unassessed_instead_of_failing_the_scan()
    {
        var assessments = new EventAssessments(new BrokenAssessor(), NullLogger<EventAssessments>.Instance);
        var result = await assessments.AttachAsync([SyntheticEvents.AquaticTransfer(Now)]);
        Assert.Null(result.Single().Assessment);
    }
}
