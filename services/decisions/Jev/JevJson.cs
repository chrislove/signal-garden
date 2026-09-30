using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalGarden.Decisions.Jev;

/// <summary>
/// Shared JSON options for talking to Jev. snake_case matches the API (e.g.
/// <c>input_tokens</c>); dictionary keys (question names, choice options, state
/// keys) are passed through untouched, and null <c>criteria</c> is omitted.
/// </summary>
internal static class JevJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
