namespace SignalGarden.Ingest;

/// <summary>
/// Polls a GTFS-Realtime feed on a fixed interval, decodes it to
/// <c>VehicleObservation</c> records, and writes them to ADX.
///
/// Fetch and decode are wired; the ADX write is still a TODO, so for now each
/// poll just logs a summary of what it saw.
/// </summary>
public class Worker(ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    // One long-lived client is fine for a single polling loop; no factory needed yet.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

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
                if (string.IsNullOrWhiteSpace(feedUrl)) continue;

                var payload = await Http.GetByteArrayAsync(feedUrl, stoppingToken);
                var observations = VehiclePositionDecoder.Decode(payload, DateTimeOffset.UtcNow);

                // TODO(ingest→adx): batch-write the observations to the ADX
                //                   VehicleObservations table (Kusto.Ingest).
                logger.LogInformation(
                    "Polled {Bytes:N0} bytes → {Vehicles} vehicles on {Routes} routes, newest report {Newest:O}",
                    payload.Length,
                    observations.Count,
                    observations.Select(o => o.RouteId).Distinct().Count(),
                    observations.Count > 0 ? observations.Max(o => o.EventTime) : null);
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
