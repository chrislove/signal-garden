/**
 * One vehicle as the API's `/api/vehicles/latest` returns it. Mirrors
 * `LiveVehicleDto` in services/core/Contracts — the browser only ever sees our
 * contract, never the GTFS-RT feed or ADX.
 */
export interface LiveVehicle {
  vehicleId: string;
  /** Rider-facing route, e.g. "66". */
  route: string | null;
  routeId: string | null;
  tripId: string | null;
  latitude: number;
  longitude: number;
  stopId: string | null;
  status: 'STOPPED_AT' | 'INCOMING_AT' | 'IN_TRANSIT_TO' | null;
  /** ISO 8601 time the vehicle reported this position. */
  reportedAt: string;
  /** Seconds old at the moment the API answered. */
  ageSeconds: number;
}
