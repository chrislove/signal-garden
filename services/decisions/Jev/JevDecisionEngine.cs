using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SignalGarden.Decisions.Jev;

/// <summary>
/// <see cref="IDecisionEngine"/> backed by the Jev System-One API. Sends the
/// configured model plus the request's state and questions in a single POST and
/// returns the typed verdict.
/// </summary>
public sealed class JevDecisionEngine(HttpClient http, JevOptions options) : IDecisionEngine
{
    public async Task<DecisionResponse> EvaluateAsync(
        DecisionRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = options.Model,
            state = request.State,
            questions = request.Questions,
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
        {
            Content = JsonContent.Create(payload, options: JevJson.Options),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var response = await http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new JevException($"Jev request failed ({(int)response.StatusCode}): {body}");
        }

        return await response.Content.ReadFromJsonAsync<DecisionResponse>(JevJson.Options, cancellationToken)
            ?? throw new JevException("Jev returned an empty response.");
    }
}

/// <summary>Raised when a Jev request fails or returns something unusable.</summary>
public sealed class JevException(string message) : Exception(message);
