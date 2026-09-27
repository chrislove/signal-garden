# AGENTS.md

Signal Garden is worked on by more than one AI agent (Claude Code, Codex, …).
To keep everyone aligned, there is a **single source of truth**:

## 👉 Read [`CLAUDE.md`](CLAUDE.md) first

`CLAUDE.md` is canonical: what this project is, its layout, the data flow, and
all coding conventions. Everything below just points into it — don't duplicate
guidance here (it will drift). If you change how the project works, update
`CLAUDE.md`, not this file.

**One-line summary:** a personal learning sandbox for time series + GIS + Azure
(ADX/KQL) + Angular 20, currently fed by public Translink (SEQ) real-time transit
data, with Jev (TypeSafe AI) as an optional "decision brain".

## Shared working context & memory

- `.notes/shared/context.md` — the fuller "why / how we got here" and open decisions.
- `.notes/shared/memory.md` — durable facts to remember across sessions.

Both are **git-ignored** (local only). Read them at the start of a session and
keep them current as decisions land. `.notes/` outside `shared/` is Chris's
private scratch — don't read or write there unless asked.

## Worklog

- `JOURNAL.md` (committed) — day-by-day record of what we did.

## Secrets

- API keys live in the git-ignored `.env` (e.g. `TYPESAFE_API_KEY` for Jev).
  Never commit secrets or paste them into tracked files.
