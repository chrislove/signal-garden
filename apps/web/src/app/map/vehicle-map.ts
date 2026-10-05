import {
  Component,
  ElementRef,
  OnDestroy,
  afterNextRender,
  effect,
  input,
  output,
  untracked,
  viewChild,
} from '@angular/core';
import * as L from 'leaflet';
import { OperationalEvent } from '../events/operational-event';
import { LiveVehicle } from '../vehicles/live-vehicle';

const BRISBANE: L.LatLngExpression = [-27.4698, 153.0251];

/** A report older than this is drawn faded: the vehicle may no longer be there. */
const STALE_AFTER_SECONDS = 120;

export const STATUS_COLOURS: Record<string, string> = {
  STOPPED_AT: '#f2b84b',
  INCOMING_AT: '#4ec9a5',
  IN_TRANSIT_TO: '#4ec9a5',
};
const UNKNOWN_COLOUR = '#8aa0ab';

export const EVENT_COLOURS: Record<string, string> = {
  'RFE-001': '#c9925e', // coffee
  'PAE-001': '#6ea8ff', // river
};

/**
 * Live vehicles and events on a Leaflet map. Leaflet isn't an Angular library,
 * so this component is the seam: Angular hands it signals, and it redraws
 * Leaflet's layers whenever they change.
 */
@Component({
  selector: 'sg-vehicle-map',
  template: '<div #map class="map"></div>',
  styles: ':host, .map { display: block; height: 100%; min-height: 420px; border-radius: 8px; }',
})
export class VehicleMap implements OnDestroy {
  readonly vehicles = input.required<LiveVehicle[]>();
  readonly events = input<OperationalEvent[]>([]);
  readonly selectedEventId = input<string | null>(null);
  readonly eventSelected = output<string>();

  private readonly mapElement = viewChild.required<ElementRef<HTMLElement>>('map');
  private map?: L.Map;
  private readonly markers = L.layerGroup();
  private readonly eventMarkers = L.layerGroup();

  constructor() {
    // Leaflet needs a real, laid-out DOM element, so create it after first render.
    afterNextRender(() => {
      // Canvas instead of SVG: ~800 dots redrawn every 20 s is cheaper as pixels.
      this.map = L.map(this.mapElement().nativeElement, { preferCanvas: true }).setView(
        BRISBANE,
        11,
      );
      // Standard OSM tiles (free, no key), darkened in styles.scss to suit the
      // dashboard. Fine for a dev sandbox; heavy use needs a tile provider.
      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap contributors',
        maxZoom: 19,
        className: 'sg-dark-tiles',
      }).addTo(this.map);
      this.markers.addTo(this.map);
      this.eventMarkers.addTo(this.map); // added last, so events sit on top
      this.draw(this.vehicles());
      this.drawEvents(this.events());
    });

    // Re-run whenever the signals they read change (i.e. every poll).
    effect(() => this.draw(this.vehicles()));
    effect(() => this.drawEvents(this.events()));

    // Fly to an event when it's selected — but not again on every poll, so the
    // event list is read untracked: only a new selection moves the map.
    effect(() => {
      const id = this.selectedEventId();
      const event = untracked(() => this.events()).find((e) => e.id === id);
      if (event && this.map) this.map.flyTo([event.latitude, event.longitude], 15, { duration: 1 });
    });
  }

  private draw(vehicles: LiveVehicle[]): void {
    if (!this.map) return; // not created yet; afterNextRender will draw
    this.markers.clearLayers();
    for (const v of vehicles) {
      const stale = v.ageSeconds > STALE_AFTER_SECONDS;
      L.circleMarker([v.latitude, v.longitude], {
        radius: 4,
        weight: 1,
        color: STATUS_COLOURS[v.status ?? ''] ?? UNKNOWN_COLOUR,
        fillOpacity: stale ? 0.15 : 0.8,
        opacity: stale ? 0.3 : 1,
      })
        .bindPopup(() => popupFor(v))
        .addTo(this.markers);
    }
  }

  private drawEvents(events: OperationalEvent[]): void {
    if (!this.map) return;
    this.eventMarkers.clearLayers();
    for (const e of events) {
      L.circleMarker([e.latitude, e.longitude], {
        radius: 12,
        weight: 3,
        color: EVENT_COLOURS[e.code] ?? '#f26b5b',
        fillOpacity: 0.2,
        dashArray: e.synthetic ? '4 4' : undefined,
      })
        .bindTooltip(`${e.code}${e.synthetic ? ' (synthetic)' : ''}`, { direction: 'top' })
        .on('click', () => this.eventSelected.emit(e.id))
        .addTo(this.eventMarkers);
    }
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }
}

/** Built with textContent, not an HTML string, so feed values can't inject markup. */
function popupFor(v: LiveVehicle): HTMLElement {
  const el = document.createElement('div');
  const lines = [
    `Route ${v.route ?? '?'}`,
    `${v.status ?? 'status unknown'}${v.stopId ? ` · stop ${v.stopId}` : ''}`,
    `Reported ${v.ageSeconds}s ago`,
    `Vehicle ${v.vehicleId}`,
  ];
  for (const text of lines) {
    const line = document.createElement('div');
    line.textContent = text;
    el.appendChild(line);
  }
  el.firstElementChild?.setAttribute('style', 'font-weight: 700');
  return el;
}
