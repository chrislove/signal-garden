import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { EventService } from './events/event.service';
import { EventTimeline } from './events/event-timeline';
import { VehicleMap } from './map/vehicle-map';
import { VehicleService } from './vehicles/vehicle.service';

@Component({
  selector: 'app-root',
  imports: [VehicleMap, EventTimeline, DatePipe],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = signal('Signal Garden');
  protected readonly subtitle = signal('Brisbane / SEQ operations pulse');

  private readonly snapshot = inject(VehicleService).snapshot;
  protected readonly vehicles = computed(() => this.snapshot().data);
  protected readonly fetchedAt = computed(() => this.snapshot().fetchedAt);
  protected readonly error = computed(() => this.snapshot().error);

  protected readonly stoppedCount = computed(
    () => this.vehicles().filter((v) => v.status === 'STOPPED_AT').length,
  );

  private readonly eventSnapshot = inject(EventService).snapshot;
  protected readonly eventScan = computed(() => this.eventSnapshot().data);
  protected readonly events = computed(() => this.eventScan().events);
  protected readonly eventsLoaded = computed(() => this.eventSnapshot().fetchedAt !== null);
  protected readonly syntheticCount = computed(
    () => this.events().filter((e) => e.synthetic).length,
  );

  /** Shared by the map and the timeline: selecting in either highlights both. */
  protected readonly selectedEventId = signal<string | null>(null);

  /** live = good data; offline = the last poll failed; waiting = no answer yet. */
  protected readonly feedState = computed(() =>
    this.error() ? 'offline' : this.fetchedAt() ? 'live' : 'waiting',
  );
}
