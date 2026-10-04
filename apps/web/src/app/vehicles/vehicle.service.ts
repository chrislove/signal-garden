import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, scan, switchMap, timer } from 'rxjs';
import { LiveVehicle } from './live-vehicle';

/** Matches the ingest worker's poll interval — asking more often gains nothing. */
export const POLL_INTERVAL_MS = 20_000;

export interface VehicleSnapshot {
  vehicles: LiveVehicle[];
  /** When we last got a good answer from the API. */
  fetchedAt: Date | null;
  /** Set when the latest poll failed; the previous vehicles are kept on screen. */
  error: string | null;
}

/**
 * Polls the API for the latest vehicle positions and exposes them as a signal.
 *
 * Polling (not WebSockets/SignalR) is deliberate for now: the data itself only
 * changes every 20 s, so a timer is the simplest thing that is honestly "live".
 */
@Injectable({ providedIn: 'root' })
export class VehicleService {
  private readonly http = inject(HttpClient);

  readonly snapshot = toSignal(
    timer(0, POLL_INTERVAL_MS).pipe(
      // switchMap drops a slow request if the next tick arrives first.
      switchMap(() =>
        this.http.get<LiveVehicle[]>('/api/vehicles/latest').pipe(
          map((vehicles): Partial<VehicleSnapshot> => ({
            vehicles,
            fetchedAt: new Date(),
            error: null,
          })),
          catchError((err) =>
            of<Partial<VehicleSnapshot>>({ error: err?.message ?? 'API unreachable' }),
          ),
        ),
      ),
      // Merge each result into the last snapshot, so a failed poll keeps the
      // previous vehicles instead of blanking the map.
      scan((last, next) => ({ ...last, ...next }), EMPTY_SNAPSHOT),
    ),
    { initialValue: EMPTY_SNAPSHOT },
  );
}

const EMPTY_SNAPSHOT: VehicleSnapshot = { vehicles: [], fetchedAt: null, error: null };
