import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ApiService } from './api.service';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { provideHttpClient } from '@angular/common/http';
import { provideZonelessChangeDetection } from '@angular/core';

const BASE = environment.apiUrl,
  setup = (): { httpMock: HttpTestingController; service: ApiService } => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    return {
      httpMock: TestBed.inject(HttpTestingController),
      service: TestBed.inject(ApiService),
    };
  };

describe('ApiService getSchedule mode query param', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('omits the mode query param when no mode is given', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe();
    const req = httpMock.expectOne(`${BASE}/api/schedule`);
    expect(req.request.params.has('mode')).toBeFalse();
    req.flush({
      alternateProposals: [],
      hasVotes: false,
      items: [],
      missedShows: [],
    });
  });

  it('sends the mode query param when a mode is given', () => {
    const { httpMock, service } = setup();
    service.getSchedule('cycling').subscribe();
    const req = httpMock.expectOne(
      (candidate) =>
        candidate.url === `${BASE}/api/schedule` &&
        candidate.params.get('mode') === 'cycling',
    );
    expect(req.request.params.get('mode')).toBe('cycling');
    req.flush({
      alternateProposals: [],
      hasVotes: false,
      items: [],
      missedShows: [],
      travelMode: 'cycling',
    });
  });
});

describe('ApiService getSchedule travelMode field parsing', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('parses travelMode from the response', () => {
    const { httpMock, service } = setup();
    service.getSchedule('driving').subscribe((result) => {
      expect(result.travelMode).toBe('driving');
    });
    httpMock.expectOne(`${BASE}/api/schedule?mode=driving`).flush({
      alternateProposals: [],
      hasVotes: false,
      items: [],
      missedShows: [],
      travelMode: 'driving',
    });
  });

  it('defaults travelMode to walking when the field is missing', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe((result) => {
      expect(result.travelMode).toBe('walking');
    });
    httpMock.expectOne(`${BASE}/api/schedule`).flush({
      alternateProposals: [],
      hasVotes: false,
      items: [],
      missedShows: [],
    });
  });

  it('defaults travelMode to walking when the field is an unrecognized value', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe((result) => {
      expect(result.travelMode).toBe('walking');
    });
    httpMock.expectOne(`${BASE}/api/schedule`).flush({
      alternateProposals: [],
      hasVotes: false,
      items: [],
      missedShows: [],
      travelMode: 'teleporting',
    });
  });
});
