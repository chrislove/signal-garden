import { DatePipe, LowerCasePipe } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { OperationalEvent } from './operational-event';

/**
 * Current events in calm operations-centre language. Selecting one expands its
 * evidence trail, grouped by where each fact came from (observed / derived /
 * inferred), and tells the map to fly to it.
 */
@Component({
  selector: 'sg-event-timeline',
  imports: [DatePipe, LowerCasePipe],
  templateUrl: './event-timeline.html',
  styleUrl: './event-timeline.scss',
})
export class EventTimeline {
  readonly events = input.required<OperationalEvent[]>();
  readonly selectedId = input<string | null>(null);
  readonly vesselsInWater = input(0);
  readonly vehiclesOnBridges = input(0);
  readonly loaded = input(false);

  readonly selected = output<string | null>();

  protected toggle(id: string): void {
    this.selected.emit(id === this.selectedId() ? null : id);
  }

  protected minutes(e: OperationalEvent): string {
    return (e.durationSeconds / 60).toFixed(0);
  }
}
