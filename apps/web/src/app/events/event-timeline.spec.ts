import { TestBed } from '@angular/core/testing';
import { EventTimeline } from './event-timeline';
import { OperationalEvent } from './operational-event';

const STAGED: OperationalEvent = {
  id: 'PAE-001:SYNTHETIC-1847',
  code: 'PAE-001',
  title: 'POSSIBLE AQUATIC TRANSFER EVENT',
  vehicleId: 'SYNTHETIC-1847',
  route: '199',
  latitude: -27.47,
  longitude: 153.04,
  since: '2026-10-05T01:00:00Z',
  lastSeen: '2026-10-05T01:06:00Z',
  durationSeconds: 360,
  recommendedAction: 'Further investigation',
  synthetic: true,
  evidence: [{ layer: 'DERIVED', label: 'Waterway relationship', value: 'Inside Brisbane River' }],
};

describe('EventTimeline', () => {
  function render(selectedId: string | null = null) {
    const fixture = TestBed.createComponent(EventTimeline);
    fixture.componentRef.setInput('events', [STAGED]);
    fixture.componentRef.setInput('selectedId', selectedId);
    fixture.componentRef.setInput('loaded', true);
    fixture.detectChanges();
    return fixture;
  }

  it('always labels a synthetic event as synthetic', () => {
    const el: HTMLElement = render().nativeElement;
    expect(el.querySelector('.synthetic')?.textContent).toContain('Synthetic');
  });

  it('shows the evidence trail only for the selected event', () => {
    expect(render().nativeElement.querySelector('.evidence')).toBeNull();
    const el: HTMLElement = render(STAGED.id).nativeElement;
    expect(el.querySelector('.evidence')?.textContent).toContain('Inside Brisbane River');
  });

  it('selects on click, and deselects when clicked again', () => {
    const fixture = render(STAGED.id);
    const emitted: (string | null)[] = [];
    fixture.componentInstance.selected.subscribe((id) => emitted.push(id));
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    expect(emitted).toEqual([null]);
  });
});
