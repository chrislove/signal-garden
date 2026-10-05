/** Mirrors `EventsResponseDto` / `EventDto` in services/core/Contracts. */
export interface EventsResponse {
  events: OperationalEvent[];
  /** Ferries in the river — expected, so not raised as events. */
  vesselsInWater: number;
  /** Vehicles over the river on a bridge — also not raised. */
  vehiclesOnBridges: number;
  scannedAt: string;
  /** Whether a model is judging events (so "not assessed" can mean "assessing…"). */
  assessmentEnabled: boolean;
}

export interface OperationalEvent {
  id: string;
  /** e.g. "PAE-001" (aquatic transfer) or "RFE-001" (refreshment). */
  code: string;
  title: string;
  vehicleId: string;
  route: string | null;
  latitude: number;
  longitude: number;
  since: string;
  lastSeen: string;
  durationSeconds: number;
  recommendedAction: string;
  /** Staged demo scenario — always shown as SYNTHETIC. */
  synthetic: boolean;
  evidence: Evidence[];
  /** The model's judgement — the INFERRED layer. Null until assessed. */
  assessment: Assessment | null;
}

/** A model's pick, with the full probability spread over every option it was offered. */
export interface Assessment {
  verdict: string;
  description: string;
  probability: number;
  /** The model's own confidence in its pick. Low means torn. */
  confidence: number;
  model: string;
  options: { name: string; description: string; probability: number }[];
}

export interface Evidence {
  /** Straight from the feed, calculated by us, or a model's judgement. */
  layer: 'OBSERVED' | 'DERIVED' | 'INFERRED';
  label: string;
  value: string;
}
