using SignalGarden.Core.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Allow the Angular dev server to call the API during local development.
const string DevCorsPolicy = "signal-garden-dev";
builder.Services.AddCors(options =>
    options.AddPolicy(DevCorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

// TODO(ingest→adx): register an IVehicleReadRepository backed by ADX/KQL, e.g.
//   builder.Services.AddSingleton<IVehicleReadRepository, AdxVehicleReadRepository>();
// It will read connection details from configuration (ADX cluster URI + database).

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

// Latest known position per vehicle. Stub: returns an empty set until the
// ADX-backed repository is wired in. Shape matches VehicleObservation so the
// UI can be built against it now.
app.MapGet("/api/vehicles/latest", (string? routeId) =>
{
    // TODO(ingest→adx): delegate to IVehicleReadRepository.GetLatestPositionsAsync(routeId).
    IReadOnlyList<VehicleObservation> observations = [];
    return Results.Ok(observations);
})
.WithName("GetLatestVehiclePositions");

app.Run();
