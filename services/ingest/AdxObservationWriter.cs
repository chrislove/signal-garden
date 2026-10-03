using System.Globalization;
using System.Text;
using Azure.Identity;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Ingest;
using SignalGarden.Core.Models;

namespace SignalGarden.Ingest;

/// <summary>
/// Writes a poll's worth of observations to the ADX <c>VehicleObservations</c>
/// table using queued ingestion.
/// </summary>
/// <remarks>
/// Queued ingestion hands the batch to ADX and returns immediately; ADX commits
/// it later, in batches governed by the table's ingestion batching policy (see
/// <c>infra/adx/schema.kql</c>). That trade — a short delay for cheap, robust
/// writes — is the normal ADX shape for telemetry.
///
/// Rows go up as CSV in the table's column order, so no ingestion mapping is
/// needed. Auth is <see cref="DefaultAzureCredential"/>, so the same code signs
/// in differently depending on where it runs: a service principal from
/// <c>AZURE_CLIENT_ID</c>/<c>AZURE_TENANT_ID</c>/<c>AZURE_CLIENT_SECRET</c> in the
/// home-server container, a managed identity once it moves to Azure, and the
/// developer's <c>az login</c> on a laptop.
/// </remarks>
public sealed class AdxObservationWriter : IDisposable
{
    private readonly IKustoQueuedIngestClient _client;
    private readonly KustoQueuedIngestionProperties _properties;

    public AdxObservationWriter(string ingestUri, string database, string table)
    {
        var connection = new KustoConnectionStringBuilder(ingestUri)
            .WithAadAzureTokenCredentialsAuthentication(new DefaultAzureCredential());
        _client = KustoIngestFactory.CreateQueuedIngestClient(connection);
        _properties = new KustoQueuedIngestionProperties(database, table)
        {
            Format = DataSourceFormat.csv,
        };
    }

    public async Task WriteAsync(IReadOnlyList<VehicleObservation> observations)
    {
        if (observations.Count == 0) return;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ToCsv(observations)));
        await _client.IngestFromStreamAsync(stream, _properties);
    }

    /// <summary>
    /// One CSV line per observation, columns in <c>VehicleObservations</c> order.
    /// Empty fields become nulls in ADX. Invariant culture so a non-English
    /// locale can't turn 153.02 into "153,02".
    /// </summary>
    internal static string ToCsv(IEnumerable<VehicleObservation> observations)
    {
        var csv = new StringBuilder();
        foreach (var o in observations)
        {
            csv.AppendJoin(',',
                o.EventTime.UtcDateTime.ToString("O"),
                o.IngestionTime.UtcDateTime.ToString("O"),
                Quote(o.VehicleId),
                Quote(o.TripId),
                Quote(o.RouteId),
                o.Latitude.ToString("R", CultureInfo.InvariantCulture),
                o.Longitude.ToString("R", CultureInfo.InvariantCulture),
                o.Bearing?.ToString("R", CultureInfo.InvariantCulture),
                o.Speed?.ToString("R", CultureInfo.InvariantCulture),
                o.CurrentStopSequence?.ToString(CultureInfo.InvariantCulture),
                Quote(o.Source));
            csv.Append('\n');
        }
        return csv.ToString();
    }

    // IDs are plain today, but quote anyway so a comma in an ID can't shift columns.
    private static string Quote(string? value) =>
        value is null ? "" : $"\"{value.Replace("\"", "\"\"")}\"";

    public void Dispose() => _client.Dispose();
}
