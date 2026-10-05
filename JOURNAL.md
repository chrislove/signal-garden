# 🌱 Signal Garden — project journal

A day-by-day tick-tock of what we actually did. Newest entries at the bottom.
Learning project; entries are informal on purpose. `☐` = planned, `✅` = done.

---

## Mon 2026-09-15 — Kickoff & scaffold

- Took the ChatGPT brainstorm (Brisbane "operations pulse", inspired by
  `gods-eye-view`) and committed to the concept + the name **Signal Garden**.
- Picked the stack: **.NET 10** backend, **Angular 20** frontend, **ADX free
  cluster** for storage/KQL. Monorepo layout agreed.
- Scaffolded the monorepo: `core` (models + repo contracts), `api` (minimal API
  with `/health` + stubbed `/api/vehicles/latest`), `ingest` (polling worker
  skeleton), `infra/adx/schema.kql`, Angular dashboard shell (state cards +
  map/charts/timeline placeholders).
- Both stacks build green. Pushed to `github.com/chrislove/signal-garden`.

## Fri 2026-09-19 — Jev, the decision model

- Chris got access to **Jev** (TypeSafe AI's System-1 decision model). Read the
  docs: typed `state` + `choice`/`score`/`noul` questions → typed probabilistic
  verdicts, ~70–500 ms, ~$0.0004/call, no hallucinated values.
- **Fired a real call** against a synthetic Brisbane network state — worked
  (737 ms). Confirmed the API shape (`Bearer` auth, `model` field required).
- Designed the **"operations brain"** idea: Jev powers the dashboard's state
  cards (network pulse) and timeline (route-triage leaderboard); its verdicts
  become their own time series in ADX. Landed a fuller idea menu (ghost-bus,
  bunching, alert triager, "leave now").

## Sun 2026-09-27 — Ground rules & multi-agent setup

- Added `CLAUDE.md` (canonical project guidance) + a git-ignored `.notes/`
  personal scratch folder.
- Chris brought **Codex** in for ideas, reviews, and code help. Set up shared
  agent infrastructure:
  - `AGENTS.md` → points all agents at `CLAUDE.md` as the single source of truth.
  - `.notes/shared/context.md` + `memory.md` → shared, git-ignored working
    context + durable memory, readable by Claude and Codex.
  - Jev key moved into git-ignored `.env` as `TYPESAFE_API_KEY`.
  - Started this journal.
- Chris signing up for the **ADX free cluster** (need cluster URI + `signalgarden` DB).
- Also set up an automated **iCloud backup** of `.notes/` (hourly launchd job via a
  scoped, FDA-granted runner) so shared notes can't be lost.

## Tue 2026-09-30 — ADX cluster is up

- Created the **ADX free cluster** `signalgarden` in **Australia East** (100 GB /
  4 vCPU, free for a year, no card). Connection URIs saved to the git-ignored
  shared notes (not committed).
- Next: confirm the database name, run `infra/adx/schema.kql`, then wire the
  query/ingestion URIs into git-ignored config.

## Sat 2026-10-03 — The feed is live

- Lost the last session to a crash: `claude --resume` died on macOS's default
  256 open-files limit. Raised it in the shell profile (`ulimit -n 61440`).
  (The Jev decision-engine wrapper from that stretch was safe: it landed on
  `main` as PR #9 — a typed C# `IDecisionEngine` for choice/score/noul, with tests.)
- **Wired the feed.** The ingest worker now fetches Translink VehiclePositions
  every 20 s, decodes the protobuf, and normalises each entity into a
  `VehicleObservation`.
  - Swapped the stale `GtfsRealtimeBindings` NuGet package (built on an old
    protobuf-net) for the official `gtfs-realtime.proto`, compiled at build time
    with `Google.Protobuf` + `Grpc.Tools`.
  - The decoder is a pure bytes-in, records-out function, and uses proto2 `Has*`
    flags so "not reported" stays `null` rather than a fake zero.
- First real read, Saturday ~7:17 am: **407 vehicles on 181 routes**, ~65 KB per
  poll, reports fresh to within seconds.
- **First ADX write.** Queued ingestion (`Kusto.Ingest`, CSV in column order,
  Azure CLI auth) with a 30 s batching policy — rows queryable ~40 s after a
  poll. 8:30 am: 636 vehicles on 276 routes.
  - Auth detour: the CLI signs in as an Entra guest of my own tenant, not as a
    personal Microsoft account, so the grant had to be `aaduser=<oid>;<tid>`,
    not `msauser=`. Decoding the token's claims is what gave it away.
  - First profile of the data, already full of questions:
    - **Speed and bearing: 0 of 1,271 rows.** Translink doesn't send them. The
      decoder keeps them `null`; a naive one would say every bus is parked.
    - **~25% duplicates** — a vehicle that hasn't reported since the last poll
      comes back with the same timestamp. Dedupe at write or at query time?
    - **Stale positions** — a few vehicles' last report is hours old. When is
      a vehicle "in service"?
- **Always on.** Moved the worker off the laptop onto a home server in Docker
  (`compose.yaml`, `restart: unless-stopped`, rotated logs). It signs in as a
  service principal that can only ingest into one database. The code switched to
  `DefaultAzureCredential`, so the same binary uses `az login` on the laptop,
  the service principal on the server, and a managed identity later in Azure.
  Afternoon peak so far: ~740 vehicles on ~300 routes.
- Learned that KQL is just Unix pipes for tables. Very happy about this.
- Next: a KQL timechart of the day, then a Council map layer for PAE-001.

---

## Mon 2026-10-05 — Buses on the map

- Overnight check: the home server ran 23 h without a gap (~2.7 M rows, 16
  feed timeouts, all retried).
- Now capturing `stop_id` and `current_status` (`STOPPED_AT` / `IN_TRANSIT_TO`),
  which the feed sent all along — appended as the last table columns, because
  the worker uploads CSV in column order.
- **The whole path works end to end for the first time:**
  Translink → home server → ADX → .NET API → Angular map.
  - **API:** `AdxVehicleReadRepository` behind `IVehicleReadRepository` (Kusto
    stays inside it), KQL with declared query parameters (no string-built
    queries), and a `LiveVehicleDto` contract that's deliberately not the
    storage model: no always-null speed/bearing, plus a ready-made `AgeSeconds`
    freshness value. `/api/vehicles/latest` and `/api/vehicles/{id}/history`.
  - **Angular:** Leaflet map (canvas renderer, darkened OSM tiles) fed by a
    polling `VehicleService` signal. Buses coloured by stopped vs moving, stale
    reports faded, and a failed poll keeps the last known positions on screen
    (tested). Dev server proxies `/api` to the API.
  - The dark CARTO basemap I reached for first now needs an API key —
    swapped for OSM tiles with a CSS invert filter.
- First live read on the map, 7:04 am Monday: 268 vehicles, 97 stopped at a
  stop. The Gold Coast trams turn up too (route `L1`).
- **Agents became GitHub Apps.** GitHub flagged the two bot machine accounts,
  so Claude and Codex now act as `claude-chrislove[bot]` / `codex-chrislove[bot]`
  with 1-hour, repo-scoped tokens (PR #10 was the end-to-end test: Claude's app
  opened it, Codex's app reviewed, Claude's fixed and resolved). Then rewrote
  history so every Claude co-author line points at the app: same code, new
  commit IDs.
- **Events: coffee and the river.** The dashboard now raises operational
  events, with the evidence that produced them:
  - **RFE-001 Possible Refreshment Event:** stationary 5+ min, *not* at a stop,
    within 50 m of a café. First real batch: Route 348, 18.5 min, 34 m from
    Cook & Co.; Route 412 near Frankie and George; Route 375, 25 m from Ant Bowl.
  - **PAE-001 Possible Aquatic Transfer Event:** reported position inside a
    river polygon — unless it's a ferry route (15 CityCats, "operating as
    intended") or within 30 m of a bridge.
  - All spatial work happens in KQL: S2-cell join for cafés, point-in-polygon
    for water, point-to-line distance for bridges. ~0.2 s per scan.
  - Reference layers from OpenStreetMap (1,809 cafés, 95 waterways, 189
    bridges) via a re-runnable loader (`infra/adx/reference/`).
  - Evidence is labelled OBSERVED / DERIVED / INFERRED (inferred = Jev, later).
  - A staged PAE-001 (Route 199, mid-river at New Farm) for demos, always
    labelled SYNTHETIC, dev only.
- **The first real PAE-001 was a false positive, and the evidence trail found
  it.** 1:19 pm: a train on the `BRBD` line was reported at -27.41094,
  153.06288, near Toombul. That's inside OSM way 597039868, Kedron Brook
  mapped as `water=canal`, and the evidence said "nearest vehicle bridge:
  Abbotsford Road, 3.2 km". A train doesn't swim 3 km from a bridge, so the
  reference data had to be wrong. It was: the bridge layer only took bridges
  over `water=river`, so the Airtrain bridge over the brook was never loaded.
  Fixed the loader to use the same water filter for bridges (180 → 189); the
  spot is now 1.9 m from the Airtrain rail bridge, which counts as a bridge
  crossing, not an aquatic transfer.
  - Why it matters: the bare verdict ("train in water") was wrong, but the
    *derived* evidence next to it made the mistake obvious within a minute.
    That's the case for showing operators the evidence, not just the alert.
- KQL gotcha: `latest` is a reserved word. `let latest = …` fails with nothing
  but "Request is invalid".
- **Public, behind a login: https://signalgarden.christopherlove.au**
  - The API now also serves the built Angular app, so one container on the home
    server is the whole dashboard (multi-stage Docker build: Node → .NET).
  - A Cloudflare Tunnel (`cloudflared` in compose) dials *out* to Cloudflare's
    Brisbane and Sydney edges. Nothing is open on the home router and the home
    IP stays hidden.
  - Cloudflare Access sits in front: anyone can enter an email and get a
    one-time code, and the logs show who looked. Set up *before* the tunnel, so
    the site was never reachable without a login. Every path, `/api` included,
    redirects to the login page.
  - The dashboard reads ADX with its own read-only service principal
    (`signalgarden-api`, Viewer). The ingest one can only write.
  - First container build was a blank page: routing ran before static files,
    so `main.js` came back as `index.html`. Fixed, with in-memory hosting tests
    that fail on the old code.
- **Jev fills in the INFERRED layer.** Each event goes to Jev once, with its
  raw facts plus local time, as a `choice` between explanations it can't add to:
  refreshment / layover / traffic / breakdown / data anomaly for RFE-001, and
  genuinely in the water / GPS error / bridge missing from the map / unrecognised
  ferry for PAE-001. The dashboard shows the pick, the model's confidence, and
  every option as a probability bar.
  - Cached per event (one call each, shared by every viewer); a request waits at
    most 2.5 s, so slow answers arrive on the next poll; failures never break
    the scan. Tests use a fake Jev, including a slow and a broken one.
  - First verdicts: real café stops come back "Refreshment ~80%". The staged bus
    in the river: **GPS anomaly 43% vs genuinely in the water 43%, confidence
    0.24**. The model is honestly torn, and the UI shows that rather than
    hiding it.

---

## Tue 2026-10-06 — Where is it on its own route?

- **Chris gave Claude permission to create tables** in the `signalgarden` ADX
  database: its `az` CLI identity now has the Database **User** role (create
  and load tables; it can't change permissions or touch other databases).
  It saves a "please paste this" round trip for each new reference table.
- **New facts from Translink's static timetable (GTFS).** The realtime feed
  only carries IDs; the timetable says where every stop is and which stops
  each route serves. A loader (`infra/adx/reference/load_gtfs_reference.py`)
  turns the 35 MB schedule into two small tables in 7 s: `Stops` (13k) and
  `RouteStops` (40k pairs, flagging each route's termini).
  - Every live route and stop ID matched the timetable; 93% of trips did. The
    rest are `UNPLANNED-…` extra services, which is now a fact of its own.
- Refreshment events now say where the vehicle is **relative to its own
  route**: at one of its stops or not (within 30 m, whatever the feed's status
  says), and how far it is from the route's nearest terminus. Jev gets the same
  facts, so it can finally tell a layover from a coffee.
- First look, 5 am: Route 19, still for 3 min, 9 m from its terminus at Rocklea
  (a textbook layover); Route 126, still for 12 min, **394 m from any of its
  stops**, near a Zarraffa's (the interesting one); a Nambour train standing
  at Landsborough platform 2.

---

## The week ahead (planned)

- ☑ **Stand up ADX** — free cluster created, `schema.kql` applied.
- ☑ **Wire the feed** — ingest polls Translink VehiclePositions, decodes protobuf
  (official `gtfs-realtime.proto`), normalises to `VehicleObservation`.
- ☐ **First ADX write** — batch observations into the `VehicleObservations` table.
- ☐ **The brain** — aggregate a network state summary, call Jev (network pulse +
  route triage), store the verdict.
- ☐ **KQL** — latest state, per-vehicle history, an anomaly/pulse timechart.
- ◐ **Angular** — map + live vehicle card connected to the API; charts and other cards still to come.
- ☐ **Second signal + alert** — add another feed or a basic alert rule.
- ☐ **Weekend** — deploy frontend + ingest to Azure (Static Web Apps, Functions,
  App Insights, Azure DevOps). Set a budget alert first.
