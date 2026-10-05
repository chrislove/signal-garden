using System.Data;
using Azure.Identity;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;

namespace SignalGarden.Api.Adx;

/// <summary>
/// Runs parameterised KQL against one ADX database. Shared by the API's
/// ADX-backed classes so the Kusto plumbing (auth, parameters, reading rows)
/// lives in one place.
/// </summary>
/// <remarks>
/// Values only ever reach KQL as declared query parameters, never by string
/// concatenation — the KQL equivalent of SQL injection protection.
/// </remarks>
public sealed class AdxQueryClient : IDisposable
{
    private readonly ICslQueryProvider _client;
    private readonly string _database;

    public AdxQueryClient(string queryUri, string database)
    {
        // DefaultAzureCredential: az login on a laptop, a service principal or
        // managed identity when deployed — same code everywhere.
        var connection = new KustoConnectionStringBuilder(queryUri)
            .WithAadAzureTokenCredentialsAuthentication(new DefaultAzureCredential());
        _client = KustoClientFactory.CreateCslQueryProvider(connection);
        _database = database;
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string query,
        IReadOnlyDictionary<string, object> parameters,
        Func<IDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var properties = new ClientRequestProperties();
        foreach (var (name, value) in parameters)
        {
            switch (value)
            {
                case string s: properties.SetParameter(name, s); break;
                case TimeSpan t: properties.SetParameter(name, t); break;
                case double d: properties.SetParameter(name, d); break;
                default: throw new ArgumentException($"Unsupported parameter type for '{name}'.");
            }
        }

        using var reader = await _client.ExecuteQueryAsync(_database, query, properties, cancellationToken);
        var results = new List<T>();
        while (reader.Read()) results.Add(map(reader));
        return results;
    }

    public void Dispose() => _client.Dispose();
}

/// <summary>Null-aware column readers. ADX strings are never null — empty means "no value".</summary>
internal static class AdxReaderExtensions
{
    public static string? StringOrNull(this IDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        if (r.IsDBNull(i)) return null;
        var value = r.GetString(i);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static double? DoubleOrNull(this IDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetDouble(i);
    }

    public static int? IntOrNull(this IDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetInt32(i);
    }

    public static DateTimeOffset Utc(this IDataReader r, string column) =>
        new(DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(column)), DateTimeKind.Utc));

    public static TimeSpan Span(this IDataReader r, string column) =>
        (TimeSpan)r.GetValue(r.GetOrdinal(column));
}
