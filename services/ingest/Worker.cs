namespace SignalGarden.Ingest;

/// <summary>
/// Polls a GTFS-Realtime feed on a fixed interval, decodes it to
/// <c>VehicleObservation</c> records, and writes them to ADX.
///
/// Currently a skeleton: the loop and timing are real, but the fetch → decode →
/// ingest steps are stubbed out (see TODOs) until we wire the pipeline.
/// </summary>
public class Worker(ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(configuration.GetValue("Ingest:PollSeconds", 20));
        var feedUrl = configuration["Ingest:VehiclePositionsUrl"];

        logger.LogInformation(
            "Signal Garden ingest starting. Interval={Interval}s, Feed={Feed}",
            interval.TotalSeconds,
            string.IsNullOrWhiteSpace(feedUrl) ? "(not configured)" : feedUrl);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                // TODO(fetch):  download the GTFS-RT protobuf from feedUrl (HttpClient).
                // TODO(decode): parse with gtfs-realtime-bindings → FeedMessage,
                //               map each VehiclePosition entity to a VehicleObservation.
                // TODO(ingest): batch-write the observations to the ADX
                //               VehicleObservations table (Kusto.Ingest).
                logger.LogInformation("Poll tick at {Time:O} — pipeline not yet wired.", DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                // Never let one bad poll kill the worker.
                logger.LogError(ex, "Ingest poll failed; will retry next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
