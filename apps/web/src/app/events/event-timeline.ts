import { DatePipe, DecimalPipe, LowerCasePipe, PercentPipe } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { OperationalEvent } from './operational-event';

/**
 * Current events in calm operations-centre language. Selecting one expands its
 * evidence trail, grouped by where each fact came from (observed / derived /
 * inferred), and tells the map to fly to it.
 */
@Component({
  selector: 'sg-event-timeline',
  imports: [DatePipe, DecimalPipe, LowerCasePipe, PercentPipe],
  templateUrl: './event-timeline.html',
  styleUrl: './event-timeline.scss',
})
export class EventTimeline {
  readonly events = input.required<OperationalEvent[]>();
  readonly selectedId = input<string | null>(null);
  readonly vesselsInWater = input(0);
  readonly vehiclesOnBridges = input(0);
  readonly loaded = input(false);
  readonly assessmentEnabled = input(false);

  readonly selected = output<string | null>();

  protected toggle(id: string): void {
    this.selected.emit(id === this.selectedId() ? null : id);
  }

  /** "data_anomaly" → "Data anomaly". */
  protected label(option: string): string {
    const words = option.replace(/_/g, ' ');
    return words.charAt(0).toUpperCase() + words.slice(1);
  }

  protected minutes(e: OperationalEvent): string {
    return (e.durationSeconds / 60).toFixed(0);
  }
}
