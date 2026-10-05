using SignalGarden.Api.Adx;
using SignalGarden.Api.Demo;
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
    builder.Services.AddSingleton(_ => new AdxQueryClient(
        queryUri, builder.Configuration["Api:Adx:Database"] ?? "signalgarden"));
    builder.Services.AddSingleton<IVehicleReadRepository>(sp => new AdxVehicleReadRepository(
        sp.GetRequiredService<AdxQueryClient>(),
        TimeSpan.FromMinutes(builder.Configuration.GetValue("Api:LatestWindowMinutes", 10))));
    builder.Services.AddSingleton(
        builder.Configuration.GetSection("Api:Events").Get<EventDetectionOptions>() ?? new EventDetectionOptions());
    builder.Services.AddSingleton<IEventDetector, AdxEventDetector>();
}
var syntheticEvents = builder.Configuration.GetValue("Api:Demo:SyntheticEvents", false);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevCorsPolicy);
}

// No HTTPS redirect: TLS ends before we're reached (Cloudflare in production,
// the Angular dev server locally), so the API itself only ever speaks HTTP.

// In the container the built Angular app sits in wwwroot, so one origin serves
// both the dashboard and /api — no CORS, no proxy. Locally wwwroot doesn't
// exist and these are no-ops; `npm start` serves the UI instead.
app.UseDefaultFiles();
app.UseStaticFiles();
// Route *after* static files: otherwise routing runs first, the fallback below
// claims every request, and main.js comes back as index.html.
app.UseRouting();

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

// Current operational events (refreshment, aquatic transfer) with their evidence.
app.MapGet("/api/events", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    if (services.GetService<IEventDetector>() is not { } detector) return NotConfigured();

    var scan = await detector.DetectAsync(cancellationToken);
    if (syntheticEvents)
        scan = scan with { Events = [SyntheticEvents.AquaticTransfer(scan.ScannedAt), .. scan.Events] };
    return Results.Ok(EventsResponseDto.From(scan));
})
.WithName("GetEvents");

// Angular routes are client-side, so any other path that isn't a file gets index.html.
// (/api/* stays out of it: an unknown API path should be a 404, not a web page.)
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api/).*$)}", "index.html");

app.Run();

static IResult NotConfigured() => Results.Problem(
    "ADX is not configured. Set Api:Adx:QueryUri in user secrets.",
    statusCode: StatusCodes.Status503ServiceUnavailable);

// Lets the test project start the real app in memory (WebApplicationFactory).
public partial class Program;
