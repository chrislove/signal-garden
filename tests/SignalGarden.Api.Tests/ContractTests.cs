using SignalGarden.Api.Demo;
using SignalGarden.Core.Contracts;
using SignalGarden.Core.Models;

namespace SignalGarden.Api.Tests;

/// <summary>
/// The API contracts are what the dashboard depends on, so pin down the values
/// they derive rather than just copy.
/// </summary>
public class ContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 1, 0, 0, TimeSpan.Zero);

    private static OperationalEvent Event(DateTimeOffset since, DateTimeOffset lastSeen) => new()
    {
        Id = "RFE-001:v1",
        Code = "RFE-001",
        Title = "POSSIBLE REFRESHMENT EVENT",
        VehicleId = "v1",
        RouteId = "740-5146",
        Latitude = -27.47,
        Longitude = 153.02,
        Since = since,
        LastSeen = lastSeen,
        RecommendedAction = "Monitor until departure",
        Evidence = [new(EvidenceLayer.Derived, "Nearest café", "Espresso Bonsai (42 m)")],
    };

    [Fact]
    public void Event_route_is_the_rider_facing_part_of_the_feed_route_id()
    {
        Assert.Equal("740", EventDto.From(Event(Now, Now)).Route);
    }

    [Fact]
    public void Event_duration_is_since_to_last_seen()
    {
        Assert.Equal(780, EventDto.From(Event(Now, Now.AddMinutes(13))).DurationSeconds);
    }

    [Fact]
    public void Evidence_layer_is_an_upper_case_string_for_the_ui()
    {
        Assert.Equal("DERIVED", EventDto.From(Event(Now, Now)).Evidence.Single().Layer);
    }

    [Fact]
    public void Incident_reference_is_short_and_stable_for_the_same_event()
    {
        var reference = EventDto.From(Event(Now, Now)).Reference;
        Assert.Matches("^[0-9A-F]{4}$", reference);
        Assert.Equal(reference, EventDto.From(Event(Now, Now.AddMinutes(5))).Reference); // next poll
        Assert.NotEqual(reference, EventDto.ReferenceFor("RFE-001:another-vehicle"));
    }

    [Fact]
    public void Staged_aquatic_event_is_always_marked_synthetic()
    {
        var staged = EventDto.From(SyntheticEvents.AquaticTransfer(Now));
        Assert.True(staged.Synthetic);
        Assert.Equal("PAE-001", staged.Code);
        Assert.StartsWith("SYNTHETIC", staged.VehicleId);
    }

    [Fact]
    public void Vehicle_age_never_goes_negative_when_clocks_disagree()
    {
        var reportFromTheFuture = new VehicleObservation
        {
            EventTime = Now.AddSeconds(5),
            IngestionTime = Now,
            VehicleId = "v1",
            Latitude = -27.47,
            Longitude = 153.02,
            Source = "test",
        };
        Assert.Equal(0, LiveVehicleDto.From(reportFromTheFuture, Now).AgeSeconds);
    }
}
