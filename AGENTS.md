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

## Identities & attribution

Claude and Codex each have their own GitHub bot account (`claude-bot-chrislove`,
`codex-bot-chrislove`), both Write collaborators. Full policy + the exact git/gh
recipe are in `CLAUDE.md` ("Commit & PR attribution") and `.notes/shared/memory.md`.
Short version: **pairing** work → author as Chris Love + `Co-Authored-By:` the
agent's bot email; **autonomous** work on an issue → commit/PR/comment as the
bot's own identity, escalate to Chris only when a human decision is needed.

## Secrets

- API keys and bot PATs live in the git-ignored `.env` (`TYPESAFE_API_KEY`,
  `GH_TOKEN_CLAUDE`, `GH_TOKEN_CODEX`). Never commit secrets or paste them into
  tracked files; push with an ephemeral token URL, never persist it in git config.
