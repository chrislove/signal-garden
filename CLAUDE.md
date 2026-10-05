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
.notes/            Scratch + shared agent context (git-ignored). See "Agents & memory".
```

## Agents & memory

This repo is worked on by more than one AI agent (Claude Code, Codex, …).
**`CLAUDE.md` is the single source of truth** — `AGENTS.md` just points here so
Codex reads the same guidance. If you change how the project works, update
`CLAUDE.md`, not `AGENTS.md`.

Shared, git-ignored working state lives in `.notes/shared/` so all agents share it:

- `.notes/shared/context.md` — the fuller "why / how we got here" + open decisions.
- `.notes/shared/memory.md` — durable facts to remember across sessions.

**Read both at the start of a session, and keep them current as decisions land**
— that is where project memory belongs, not in an agent's private store.
`.notes/` outside `shared/` is Chris's private scratch — don't read or write there
unless asked. `JOURNAL.md` (committed) is the day-by-day worklog; add an entry
when we finish a chunk of work.

### Commit & PR attribution

Claude and Codex each act through their own **GitHub App**, installed on this
repo only: `claude-chrislove[bot]` and `codex-chrislove[bot]`. Private keys live
in git-ignored `.secrets/github-apps/`; `scripts/gh-app-token.sh claude|codex`
turns one into a 1-hour token. Policy:

| Agent | Commit identity (name / email) |
|-------|--------------------------------|
| Claude | `claude-chrislove[bot] <337864602+claude-chrislove[bot]@users.noreply.github.com>` |
| Codex | `codex-chrislove[bot] <337864652+codex-chrislove[bot]@users.noreply.github.com>` |

- **Pairing (Chris + agent):** author the commit as **Chris Love
  <chris@christopherlove.au>** and add a `Co-Authored-By:` trailer for the working
  agent using its **bot email above** (not `noreply@anthropic.com`) so its avatar
  shows. This overrides Claude Code's default co-author line.
- **Autonomous (agent solo on an issue):** author commits, open the PR, and
  comment/review/resolve **as the agent's app**. Escalate to Chris with an
  @mention only when a human decision is needed (apps can't be assigned as
  reviewers or receive notifications). Agents may review and resolve each
  other's PRs; an app can't approve its own.

```bash
export GH_TOKEN=$(scripts/gh-app-token.sh claude)      # gh now acts as the bot
git -c user.name="claude-chrislove[bot]" \
    -c user.email="337864602+claude-chrislove[bot]@users.noreply.github.com" commit -m "..."
git push "https://x-access-token:${GH_TOKEN}@github.com/chrislove/signal-garden.git" HEAD:refs/heads/<branch>
gh pr create ...
```

Never commit or print tokens or keys; push via an ephemeral URL like the one
above, never persist a token in `.git/config`.

## Data flow

The browser never talks to the upstream feed directly (binary protobuf, no
CORS). The path is always:

```
GTFS-RT feed ──poll──▶ Ingest worker ──write──▶ ADX ──KQL──▶ API ──JSON──▶ Angular
```

## Decision model (Jev)

Optional "operations brain": **Jev** (TypeSafe AI) is a System-1 decision model —
send a JSON `state` + typed questions (`choice` / `score` / `noul`), get back
typed probabilistic verdicts (~70–500 ms, no hallucinated values). Planned first
use: a **network pulse** (choice) for the dashboard's state cards and **route
triage** (score + noul) for the timeline; verdicts get stored in ADX as their own
time series. Don't use Jev for arithmetic — use it for judgment under ambiguity.
Full details, API shape, and the idea menu are in `.notes/shared/context.md`.

## Commands

```bash
dotnet build                                # whole solution
dotnet run --project services/api           # API, /health
dotnet run --project services/ingest        # polling worker
cd apps/web && npm start                    # Angular dev server, :4200
cd apps/web && npm test                     # Karma/Jasmine
```

Local config that never goes in the repo lives in .NET user secrets:
`Api:Adx:QueryUri` and `TYPESAFE_API_KEY` (API; the key lets Jev assess events)
and `Ingest:Adx:IngestUri` (ingest). Both authenticate
with `DefaultAzureCredential` — `az login` locally. The always-on ingest runs in
Docker via `compose.yaml` (config in a git-ignored `.env.ingest`).

OpenStreetMap reference layers (cafés, waterways, bridges) are loaded into ADX
with `python3 infra/adx/reference/load_osm_reference.py` (re-runnable; needs
`az login` and Node for `npx osmtogeojson`). The API's dev config stages one
SYNTHETIC event (`Api:Demo:SyntheticEvents`) — synthetic events must always be
labelled as such.

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
- API keys live in `.env` (git-ignored), e.g. `TYPESAFE_API_KEY` for Jev — the
  TypeSafe SDKs read that env var. Never paste a key into a tracked file.
