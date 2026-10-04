import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { VehicleMap } from './map/vehicle-map';
import { VehicleService } from './vehicles/vehicle.service';

@Component({
  selector: 'app-root',
  imports: [VehicleMap, DatePipe],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = signal('Signal Garden');
  protected readonly subtitle = signal('Brisbane / SEQ operations pulse');

  private readonly snapshot = inject(VehicleService).snapshot;
  protected readonly vehicles = computed(() => this.snapshot().vehicles);
  protected readonly fetchedAt = computed(() => this.snapshot().fetchedAt);
  protected readonly error = computed(() => this.snapshot().error);

  protected readonly stoppedCount = computed(
    () => this.vehicles().filter((v) => v.status === 'STOPPED_AT').length,
  );

  /** live = good data; offline = the last poll failed; waiting = no answer yet. */
  protected readonly feedState = computed(() =>
    this.error() ? 'offline' : this.fetchedAt() ? 'live' : 'waiting',
  );
}
