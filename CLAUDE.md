# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

Signal Garden is a **personal learning project** — a sandbox for experimenting
with four things together:

- **Time series** — high-frequency observations, windowing, aggregation,
  anomaly detection
- **GIS / geospatial data** — positions, routes, map rendering
- **Azure** — Azure Data Explorer (ADX/KQL) first, then hosting, Functions,
  App Insights, Azure DevOps
- **Angular** — the dashboard front end (Angular 20, standalone components)

It is not a product and has no users. Optimise for learning and clarity over
robustness, abstraction, or premature scale. Keep it cheap: ADX free cluster,
and nothing paid without an explicit budget alert first.

The current slice uses public real-time Translink (SEQ) transit data as a
convenient live feed. The feed is a means to an end — swapping in weather,
traffic, or seismic data later is expected.

## Layout

```
apps/web/          Angular 20 dashboard (map, timeline, charts)
services/core/     SignalGarden.Core — domain models, DTOs, repository contracts
services/api/      SignalGarden.Api  — thin read API (ASP.NET minimal API)
services/ingest/   SignalGarden.Ingest — polling worker (GTFS-RT → ADX)
infra/adx/         ADX table schema + KQL functions
data/              Static GTFS reference notes (large files git-ignored)
.notes/            Chris's personal notes — git-ignored, don't read or write unless asked
```

## Data flow

The browser never talks to the upstream feed directly (binary protobuf, no
CORS). The path is always:

```
GTFS-RT feed ──poll──▶ Ingest worker ──write──▶ ADX ──KQL──▶ API ──JSON──▶ Angular
```

## Commands

```bash
dotnet build                                # whole solution
dotnet run --project services/api           # API, /health
dotnet run --project services/ingest        # polling worker
cd apps/web && npm start                    # Angular dev server, :4200
cd apps/web && npm test                     # Karma/Jasmine
```

ADX schema (`infra/adx/schema.kql`) is applied by hand in the Data Explorer web
UI against a `signalgarden` database — there's no migration tooling.

## Conventions

- **.NET 10**, nullable + implicit usings enabled. `sealed record` for domain
  models, `required` for non-optional members.
- The API depends on **interfaces in `services/core/Abstractions`**, never on
  ADX types directly. Keep the Kusto dependency inside the implementation.
- Pipeline work that isn't wired yet is marked with tagged TODOs —
  `TODO(fetch)`, `TODO(decode)`, `TODO(ingest)`, `TODO(ingest→adx)`. Follow
  that convention for new stubs.
- XML doc comments on public types explain *why*, not just *what* — match that
  density.
- Angular: standalone components, SCSS, Prettier at 100 cols with single
  quotes (config lives in `apps/web/package.json`).
- KQL functions are `.create-or-alter` so the schema file stays re-runnable.

## Notes

- Transit data is CC BY 4.0 and needs plain-text attribution if anything goes
  public.
- Static GTFS files and any `.env` / `*.local.json` are git-ignored on purpose;
  don't commit credentials or cluster URIs.
