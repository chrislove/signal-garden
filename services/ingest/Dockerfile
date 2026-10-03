# Ingest worker image. Build from the repo root (the worker references core):
#   docker compose up -d --build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY services/core/ services/core/
COPY services/ingest/ services/ingest/
RUN dotnet publish services/ingest -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
# The base image ships a non-root "app" user; nothing here needs root.
USER app
ENTRYPOINT ["dotnet", "SignalGarden.Ingest.dll"]
