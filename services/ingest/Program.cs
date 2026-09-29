using SignalGarden.Ingest;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("gtfs-rt");
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
