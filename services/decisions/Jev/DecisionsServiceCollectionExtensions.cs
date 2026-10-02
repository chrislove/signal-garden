using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SignalGarden.Decisions.Jev;

public static class DecisionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IDecisionEngine"/> (Jev). Binds the optional <c>Jev</c>
    /// config section for endpoint/model, and reads the API key from
    /// <c>TYPESAFE_API_KEY</c> (env var or config).
    /// </summary>
    public static IServiceCollection AddJevDecisionEngine(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new JevOptions();
        configuration.GetSection("Jev").Bind(options);
        options.ApiKey = configuration["TYPESAFE_API_KEY"] ?? options.ApiKey;

        services.AddSingleton(options);
        services.AddHttpClient<IDecisionEngine, JevDecisionEngine>();
        return services;
    }
}
