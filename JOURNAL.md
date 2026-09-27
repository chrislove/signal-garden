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

---

## The week ahead (planned)

- ☐ **Stand up ADX** — create the free cluster + `signalgarden` DB, run `schema.kql`.
- ☐ **Wire the feed** — ingest polls Translink VehiclePositions, decodes protobuf
  (`gtfs-realtime-bindings`), normalises to `VehicleObservation`.
- ☐ **First ADX write** — batch observations into the `VehicleObservations` table.
- ☐ **The brain** — aggregate a network state summary, call Jev (network pulse +
  route triage), store the verdict.
- ☐ **KQL** — latest state, per-vehicle history, an anomaly/pulse timechart.
- ☐ **Angular** — connect to the API, render the map + charts + live state cards.
- ☐ **Second signal + alert** — add another feed or a basic alert rule.
- ☐ **Weekend** — deploy frontend + ingest to Azure (Static Web Apps, Functions,
  App Insights, Azure DevOps). Set a budget alert first.
