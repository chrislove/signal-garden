import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, discardPeriodicTasks, fakeAsync, tick } from '@angular/core/testing';
import { LiveVehicle } from './live-vehicle';
import { POLL_INTERVAL_MS, VehicleService } from './vehicle.service';

const BUS: LiveVehicle = {
  vehicleId: 'v1',
  route: '66',
  routeId: '66-4838',
  tripId: 't1',
  latitude: -27.47,
  longitude: 153.02,
  stopId: '1285',
  status: 'STOPPED_AT',
  reportedAt: '2026-10-05T00:00:00Z',
  ageSeconds: 12,
};

describe('VehicleService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  it('polls immediately and then every interval', fakeAsync(() => {
    const service = TestBed.inject(VehicleService);
    service.snapshot(); // reading the signal subscribes
    tick(0);
    http.expectOne('/api/vehicles/latest').flush([BUS]);
    expect(service.snapshot().vehicles).toEqual([BUS]);

    tick(POLL_INTERVAL_MS);
    http.expectOne('/api/vehicles/latest').flush([]);
    expect(service.snapshot().vehicles).toEqual([]);
    discardPeriodicTasks();
  }));

  it('keeps the last vehicles on screen when a poll fails', fakeAsync(() => {
    const service = TestBed.inject(VehicleService);
    service.snapshot();
    tick(0);
    http.expectOne('/api/vehicles/latest').flush([BUS]);

    tick(POLL_INTERVAL_MS);
    http
      .expectOne('/api/vehicles/latest')
      .flush('down', { status: 503, statusText: 'Service Unavailable' });

    expect(service.snapshot().vehicles).toEqual([BUS]);
    expect(service.snapshot().error).toBeTruthy();
    discardPeriodicTasks();
  }));
});
