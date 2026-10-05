import { Injectable } from '@angular/core';
import { pollJson } from '../shared/poll';
import { LiveVehicle } from './live-vehicle';

/** Latest vehicle positions from the API, refreshed every poll. */
@Injectable({ providedIn: 'root' })
export class VehicleService {
  readonly snapshot = pollJson<LiveVehicle[]>('/api/vehicles/latest', []);
}
