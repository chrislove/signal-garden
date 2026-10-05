import { HttpClient } from '@angular/common/http';
import { Signal, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, scan, switchMap, timer } from 'rxjs';

/** Matches the ingest worker's poll interval — asking more often gains nothing. */
export const POLL_INTERVAL_MS = 20_000;

export interface Polled<T> {
  data: T;
  /** When we last got a good answer from the API. */
  fetchedAt: Date | null;
  /** Set when the latest poll failed; the previous data is kept on screen. */
  error: string | null;
}

/**
 * GETs a JSON endpoint now and every `intervalMs`, as a signal.
 *
 * Polling (not WebSockets/SignalR) is deliberate: the data itself only changes
 * every 20 s, so a timer is the simplest thing that is honestly "live". Must be
 * called in an injection context (e.g. a service field initialiser).
 */
export function pollJson<T>(
  url: string,
  initial: T,
  intervalMs = POLL_INTERVAL_MS,
): Signal<Polled<T>> {
  const http = inject(HttpClient);
  const empty: Polled<T> = { data: initial, fetchedAt: null, error: null };

  return toSignal(
    timer(0, intervalMs).pipe(
      // switchMap drops a slow request if the next tick arrives first.
      switchMap(() =>
        http.get<T>(url).pipe(
          map((data): Partial<Polled<T>> => ({ data, fetchedAt: new Date(), error: null })),
          catchError((err) => of<Partial<Polled<T>>>({ error: err?.message ?? 'API unreachable' })),
        ),
      ),
      // Merge each result into the last one, so a failed poll keeps the previous
      // data instead of blanking the screen.
      scan((last, next) => ({ ...last, ...next }), empty),
    ),
    { initialValue: empty },
  );
}
