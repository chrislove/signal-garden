# 🌱 Signal Garden

A small, live-data playground. First slice: a **Brisbane / South-East Queensland
"operations pulse"** dashboard fed by real-time Translink transit data, stored
and analysed in **Azure Data Explorer (ADX)**, and surfaced through an **Angular**
UI. The name is deliberately broad — the garden can grow other feeds (weather,
traffic, quakes) later.

> Status: **scaffold**. Structure, contracts, and the ADX schema exist; the live
> pipeline (fetch → decode → ingest) is stubbed and not yet wired.

## Architecture

The browser never talks to Translink directly — GTFS-Realtime is binary protobuf
served without CORS. So the data path is always:

```
Translink GTFS-RT ──poll──▶ Ingest worker ──write──▶ ADX ──KQL──▶ API ──JSON──▶ Angular
 (protobuf, no key)         (.NET, decode)          (KQL store)   (.NET)        (map/charts)
```

## Repository layout

```
signal-garden/
├── apps/web/            Angular 20 dashboard (map, timeline, charts, state cards)
├── services/
│   ├── core/            SignalGarden.Core — domain models, DTOs, repository contracts
│   ├── api/             SignalGarden.Api  — thin read API (ASP.NET minimal API)
│   └── ingest/          SignalGarden.Ingest — polling worker (GTFS-RT → ADX)
├── infra/adx/           ADX table schema + KQL query functions
├── data/               Static GTFS reference notes (large files git-ignored)
└── SignalGarden.sln
```

## Prerequisites

- **.NET 10 SDK** (8.0 LTS also works if you retarget)
- **Node 24** + npm (Angular CLI pinned to v20 via `npx`)
- An **ADX free cluster** — https://dataexplorer.azure.com — no Azure
  subscription or credit card required (~100 GB, up to 10 DBs, 1 year free)

## Quickstart (local, once wired)

```bash
# API
dotnet run --project services/api          # https://localhost:xxxx/health

# Ingest worker (currently logs poll ticks; pipeline stubbed)
dotnet run --project services/ingest

# Angular dev server
cd apps/web && npm start                    # http://localhost:4200
```

Apply the ADX schema once, in the Data Explorer web UI against a `signalgarden`
database:

```bash
infra/adx/schema.kql   # paste/run top-to-bottom
```

## Roadmap (first-week experiment)

- [x] Scaffold monorepo, contracts, ADX schema
- [ ] **Day 2** — ingest one feed locally, write first ADX table
- [ ] **Day 3** — KQL for latest state, history, anomalies
- [ ] **Day 4** — connect Angular to the API, render map + charts
- [ ] **Day 5** — add a second feed + a basic alert rule
- [ ] **Weekend** — deploy frontend + ingest to Azure (Static Web Apps, Functions,
      App Insights, Azure DevOps)

Guardrail: set an **Azure budget alert** before creating any paid resource, and
stay on the ADX free cluster until it hits its limits.

## Data & attribution

Transit data © **Translink** (Queensland), licensed **CC BY 4.0**. Realtime feeds
require no API key. Public-facing use must attribute Translink; logo/branding use
needs their approval. See [`data/README.md`](data/README.md).

Concept nods to [`bilawalsidhu/gods-eye-view`](https://github.com/bilawalsidhu/gods-eye-view)
(CesiumJS live-data globe) — borrowing feed/visual ideas, not forking.
