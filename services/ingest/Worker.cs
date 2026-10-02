namespace SignalGarden.Ingest;

/// <summary>
/// Polls a GTFS-Realtime feed on a fixed interval, decodes it to
/// <c>VehicleObservation</c> records, and writes them to ADX.
///
/// If <c>Ingest:Adx:IngestUri</c> isn't configured (it lives in user secrets,
/// never in the repo), the worker still polls and logs — it just skips the write.
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

        var ingestUri = configuration["Ingest:Adx:IngestUri"];
        using var writer = string.IsNullOrWhiteSpace(ingestUri)
            ? null
            : new AdxObservationWriter(
                ingestUri,
                configuration["Ingest:Adx:Database"] ?? "signalgarden",
                configuration["Ingest:Adx:Table"] ?? "VehicleObservations");
        if (writer is null)
            logger.LogWarning("Ingest:Adx:IngestUri not set — polling without writing to ADX.");

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                if (string.IsNullOrWhiteSpace(feedUrl)) continue;

                var payload = await Http.GetByteArrayAsync(feedUrl, stoppingToken);
                var observations = VehiclePositionDecoder.Decode(payload, DateTimeOffset.UtcNow);

                if (writer is not null) await writer.WriteAsync(observations);

                logger.LogInformation(
                    "Polled {Bytes:N0} bytes → {Vehicles} vehicles on {Routes} routes, newest report {Newest:O}, ADX {Adx}",
                    payload.Length,
                    observations.Count,
                    observations.Select(o => o.RouteId).Distinct().Count(),
                    observations.Count > 0 ? observations.Max(o => o.EventTime) : null,
                    writer is null ? "off" : "queued");
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
