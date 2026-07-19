import type { Group, Show, UserAvailability, Vote } from '../models';
import {
  type HttpErrorResponse,
  provideHttpClient,
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ApiService } from './api.service';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { provideZonelessChangeDetection } from '@angular/core';

const BASE = environment.apiUrl,
  mockShow: Show = {
    fee: '$1',
    lengthInMinutes: 60,
    price: '$10',
    showId: 1,
    showTimes: ['2025-08-01T19:00:00Z'],
    title: 'Test Show',
  },
  mockVote: Vote = { rank: 1, showId: 1 },
  mockGroup: Group = {
    groupId: 'g1',
    inviteCode: 'ABC123',
    members: [],
    name: 'My Group',
  },
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

describe('ApiService getShows', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('returns shows array on success', () => {
    const { httpMock, service } = setup();
    service.getShows().subscribe((shows) => {
      expect(shows).toEqual([mockShow]);
    });
    const req = httpMock.expectOne(`${BASE}/api/shows`);
    expect(req.request.method).toBe('GET');
    req.flush([mockShow]);
  });

  it('returns empty array when backend returns []', () => {
    const { httpMock, service } = setup();
    service.getShows().subscribe((shows) => {
      expect(shows).toEqual([]);
    });
    httpMock.expectOne(`${BASE}/api/shows`).flush([]);
  });

  it('propagates HTTP errors', () => {
    const { httpMock, service } = setup();
    service.getShows().subscribe({
      error: (error: HttpErrorResponse) => {
        expect(error).toBeTruthy();
      },
    });
    httpMock.expectOne(`${BASE}/api/shows`).flush('Server Error', {
      status: 500,
      statusText: 'Internal Server Error',
    });
  });
});

describe('ApiService getVotes', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('returns votes array', () => {
    const { httpMock, service } = setup();
    service.getVotes().subscribe((votes) => {
      expect(votes).toEqual([mockVote]);
    });
    httpMock.expectOne(`${BASE}/api/votes`).flush([mockVote]);
  });

  it('propagates 401 errors', () => {
    const { httpMock, service } = setup();
    service.getVotes().subscribe({
      error: (error: HttpErrorResponse) => {
        expect(error).toBeTruthy();
      },
    });
    httpMock
      .expectOne(`${BASE}/api/votes`)
      .flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
  });
});

describe('ApiService saveVotes', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('sends PUT with votes payload', () => {
    const { httpMock, service } = setup();
    let completed = false;
    service.saveVotes([mockVote]).subscribe({
      complete: () => {
        completed = true;
      },
    });
    const req = httpMock.expectOne(`${BASE}/api/votes`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual([mockVote]);
    req.flush(null);
    expect(completed).toBeTrue();
  });

  it('sends empty array when no votes', () => {
    const { httpMock, service } = setup();
    service.saveVotes([]).subscribe();
    const req = httpMock.expectOne(`${BASE}/api/votes`);
    expect(req.request.body).toEqual([]);
    req.flush(null);
  });
});

describe('ApiService getMyGroup', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('returns group', () => {
    const { httpMock, service } = setup();
    service.getMyGroup().subscribe((group) => {
      expect(group).toEqual(mockGroup);
    });
    httpMock.expectOne(`${BASE}/api/groups/me`).flush(mockGroup);
  });

  it('propagates 400 errors', () => {
    const { httpMock, service } = setup();
    service.getMyGroup().subscribe({
      error: (error: HttpErrorResponse) => {
        expect(error).toBeTruthy();
      },
    });
    httpMock
      .expectOne(`${BASE}/api/groups/me`)
      .flush('No group', { status: 400, statusText: 'Bad Request' });
  });
});

describe('ApiService createGroup', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('sends POST with name and returns group', () => {
    const { httpMock, service } = setup();
    service.createGroup('My Group').subscribe((group) => {
      expect(group).toEqual(mockGroup);
    });
    const req = httpMock.expectOne(`${BASE}/api/groups`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'My Group' });
    req.flush(mockGroup);
  });
});

describe('ApiService joinGroup', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('sends POST with inviteCode', () => {
    const { httpMock, service } = setup();
    service.joinGroup('ABC123').subscribe((group) => {
      expect(group).toEqual(mockGroup);
    });
    const req = httpMock.expectOne(`${BASE}/api/groups/join`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ inviteCode: 'ABC123' });
    req.flush(mockGroup);
  });

  it('propagates error on invalid invite code', () => {
    const { httpMock, service } = setup();
    service.joinGroup('INVALID').subscribe({
      error: (error: HttpErrorResponse) => {
        expect(error).toBeTruthy();
      },
    });
    httpMock
      .expectOne(`${BASE}/api/groups/join`)
      .flush('Invalid', { status: 400, statusText: 'Bad Request' });
  });
});

describe('ApiService getAvailability', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('returns availability', () => {
    const { httpMock, service } = setup(),
      avail: UserAvailability = {
        windows: [
          { end: '2025-08-01T14:00:00Z', start: '2025-08-01T10:00:00Z' },
        ],
      };
    service.getAvailability().subscribe((result) => {
      expect(result).toEqual(avail);
    });
    httpMock.expectOne(`${BASE}/api/availability`).flush(avail);
  });
});

describe('ApiService saveAvailability', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('sends PUT with availability payload', () => {
    const { httpMock, service } = setup(),
      avail: UserAvailability = { windows: [] };
    let completed = false;
    service.saveAvailability(avail).subscribe({
      complete: () => {
        completed = true;
      },
    });
    const req = httpMock.expectOne(`${BASE}/api/availability`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(avail);
    req.flush(null);
    expect(completed).toBeTrue();
  });
});
