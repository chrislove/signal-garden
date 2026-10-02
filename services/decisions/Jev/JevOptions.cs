namespace SignalGarden.Decisions.Jev;

/// <summary>Configuration for the Jev decision engine.</summary>
public sealed class JevOptions
{
    /// <summary>System-One endpoint.</summary>
    public string Endpoint { get; set; } = "https://api.typesafe.ai/v1/systemone";

    /// <summary>Model to request. <c>jev-latest</c> resolves to the current version.</summary>
    public string Model { get; set; } = "jev-latest";

    /// <summary>API key (from <c>TYPESAFE_API_KEY</c>). Never commit this.</summary>
    public string ApiKey { get; set; } = "";
}
