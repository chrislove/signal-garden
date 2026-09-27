# Static GTFS reference (Translink SEQ)

The realtime feed carries only IDs (`route_id`, `trip_id`, `stop_id`). To make
the dashboard readable — route short names, headsigns, stop names, scheduled
times — we join against the **static GTFS schedule** published by Translink.

- **Source:** Translink open data (South-East Queensland), CC BY 4.0. No API key.
- **Format:** a ZIP of CSV files (`routes.txt`, `trips.txt`, `stops.txt`,
  `stop_times.txt`, `calendar.txt`, …).
- **Cadence:** the static schedule changes infrequently (roughly weekly); the
  realtime feeds change every few seconds. Refresh the static data on a slow
  timer, not on every poll.

## Not committed

The GTFS ZIP and any extracted CSVs are **git-ignored** — they're large and
re-downloadable. Drop them here locally (e.g. `data/gtfs-seq/`) or, better, load
the lookup tables into ADX so the join happens in KQL.

## Attribution

Public-facing use must attribute Translink and follow CC BY 4.0. Using the
Translink logo/branding requires their approval; plain-text attribution is fine
for a private learning project.
