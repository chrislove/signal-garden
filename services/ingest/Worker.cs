using SignalGarden.Core.Models;
using SignalGarden.Ingest.Gtfs;
using TransitRealtime;

namespace SignalGarden.Ingest;

/// <summary>
/// Polls a GTFS-Realtime VehiclePositions feed on a fixed interval, decodes the
/// protobuf, normalises it to <see cref="VehicleObservation"/> records, and logs a
/// sample. Writing to ADX is a separate step (see TODO in <see cref="PollOnceAsync"/>).
/// </summary>
public class Worker(
    ILogger<Worker> logger,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : BackgroundService
{
    private const string Source = "translink-seq/vehicle-positions";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(configuration.GetValue("Ingest:PollSeconds", 20));
        var feedUrl = configuration["Ingest:VehiclePositionsUrl"];

        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            logger.LogError("Ingest:VehiclePositionsUrl is not configured; nothing to poll.");
            return;
        }

        logger.LogInformation(
            "Signal Garden ingest starting. Interval={Interval}s, Feed={Feed}",
            interval.TotalSeconds, feedUrl);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                var observations = await PollOnceAsync(feedUrl, stoppingToken);
                LogSample(observations);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // graceful shutdown
            }
            catch (Exception ex)
            {
                // Never let one bad poll kill the worker.
                logger.LogError(ex, "Ingest poll failed; will retry next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<IReadOnlyList<VehicleObservation>> PollOnceAsync(string feedUrl, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient("gtfs-rt");
        await using var stream = await http.GetStreamAsync(feedUrl, ct);
        var feed = FeedMessage.Parser.ParseFrom(stream);

        // TODO(ingest→adx): batch-write these observations to the ADX
        // VehicleObservations table. For now we decode and log only.
        return VehiclePositionsDecoder.ToObservations(feed, Source);
    }

    private void LogSample(IReadOnlyList<VehicleObservation> observations)
    {
        logger.LogInformation("Decoded {Count} vehicle positions.", observations.Count);
        foreach (var o in observations.Take(5))
        {
            logger.LogInformation(
                "  {VehicleId} route={RouteId} trip={TripId} ({Lat:F5},{Lon:F5}) " +
                "bearing={Bearing} speed={Speed} seq={Seq} @ {EventTime:u}",
                o.VehicleId, o.RouteId ?? "-", o.TripId ?? "-", o.Latitude, o.Longitude,
                o.Bearing, o.Speed, o.CurrentStopSequence, o.EventTime);
        }
    }
}
