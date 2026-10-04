using SignalGarden.Api.Adx;
using SignalGarden.Core.Abstractions;
using SignalGarden.Core.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Allow the Angular dev server to call the API during local development.
// (`npm start` also proxies /api to this port, so the browser rarely needs it.)
const string DevCorsPolicy = "signal-garden-dev";
builder.Services.AddCors(options =>
    options.AddPolicy(DevCorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

// The ADX query URI lives in user secrets (`Api:Adx:QueryUri`), never in the
// repo. Without it the API still starts — /health works and the data endpoints
// answer 503 — so the front end can be worked on without a cluster.
var queryUri = builder.Configuration["Api:Adx:QueryUri"];
if (!string.IsNullOrWhiteSpace(queryUri))
{
    builder.Services.AddSingleton<IVehicleReadRepository>(_ => new AdxVehicleReadRepository(
        queryUri,
        builder.Configuration["Api:Adx:Database"] ?? "signalgarden",
        TimeSpan.FromMinutes(builder.Configuration.GetValue("Api:LatestWindowMinutes", 10))));
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevCorsPolicy);
}

app.UseHttpsRedirection();

// Liveness/readiness probe — always safe to call.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "signal-garden-api" }))
    .WithName("Health");

// Latest recent position per vehicle, optionally for one route ("66-4838").
app.MapGet("/api/vehicles/latest", async (
    string? routeId,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    if (services.GetService<IVehicleReadRepository>() is not { } repository) return NotConfigured();

    var now = DateTimeOffset.UtcNow;
    var observations = await repository.GetLatestPositionsAsync(routeId, cancellationToken);
    return Results.Ok(observations.Select(o => LiveVehicleDto.From(o, now)));
})
.WithName("GetLatestVehiclePositions");

// One vehicle's trail, oldest first — the "evidence trail" behind a map dot.
app.MapGet("/api/vehicles/{vehicleId}/history", async (
    string vehicleId,
    int? minutes,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    if (services.GetService<IVehicleReadRepository>() is not { } repository) return NotConfigured();

    var window = TimeSpan.FromMinutes(Math.Clamp(minutes ?? 60, 1, 24 * 60));
    var now = DateTimeOffset.UtcNow;
    var observations = await repository.GetVehicleHistoryAsync(vehicleId, window, cancellationToken);
    return Results.Ok(observations.Select(o => LiveVehicleDto.From(o, now)));
})
.WithName("GetVehicleHistory");

app.Run();

static IResult NotConfigured() => Results.Problem(
    "ADX is not configured. Set Api:Adx:QueryUri in user secrets.",
    statusCode: StatusCodes.Status503ServiceUnavailable);
