import { Injectable } from '@angular/core';
import { pollJson } from '../shared/poll';
import { EventsResponse } from './operational-event';

const NO_EVENTS: EventsResponse = {
  events: [],
  vesselsInWater: 0,
  vehiclesOnBridges: 0,
  scannedAt: '',
};

/** Current operational events from the API, refreshed every poll. */
@Injectable({ providedIn: 'root' })
export class EventService {
  readonly snapshot = pollJson<EventsResponse>('/api/events', NO_EVENTS);
}
