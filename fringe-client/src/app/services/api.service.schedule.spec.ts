import {
  type HttpErrorResponse,
  provideHttpClient,
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import type { ScheduleItem, Show } from '../models';
import { ApiService } from './api.service';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { provideZonelessChangeDetection } from '@angular/core';

const BASE = environment.apiUrl,
  ONE_ITEM = 1,
  ONE_PROPOSAL = 1,
  ONE_MISSED_SHOW = 1,
  mockShow: Show = {
    fee: '$1',
    lengthInMinutes: 60,
    price: '$10',
    showId: 1,
    showTimes: ['2025-08-01T19:00:00Z'],
    title: 'Test Show',
  },
  mockScheduleItem: ScheduleItem = {
    groupScore: 5,
    show: mockShow,
    showTime: '2025-08-01T19:00:00Z',
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

describe('ApiService getSchedule new format', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('handles new object format with full fields', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe((result) => {
      expect(result.items).toEqual([mockScheduleItem]);
      expect(result.alternateProposals.length).toBe(ONE_PROPOSAL);
      expect(result.missedShows.length).toBe(ONE_MISSED_SHOW);
      expect(result.hasVotes).toBeTrue();
    });
    httpMock.expectOne(`${BASE}/api/schedule`).flush({
      alternateProposals: [
        { description: 'Alt', excludedMemberName: 'Alice', items: [] },
      ],
      hasVotes: true,
      items: [mockScheduleItem],
      missedShows: [
        { blockedByMembers: [], conflictsWithScheduled: true, show: mockShow },
      ],
    });
  });
});

describe('ApiService getSchedule legacy format', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('handles old array format (legacy backend)', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe((result) => {
      expect(result.items).toEqual([mockScheduleItem]);
      expect(result.alternateProposals).toEqual([]);
      expect(result.missedShows).toEqual([]);
      expect(result.hasVotes).toBeTrue();
    });
    httpMock.expectOne(`${BASE}/api/schedule`).flush([mockScheduleItem]);
  });

  it('sets hasVotes=false for old array format with no items', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe((result) => {
      expect(result.hasVotes).toBeFalse();
    });
    httpMock.expectOne(`${BASE}/api/schedule`).flush([]);
  });
});

describe('ApiService getSchedule invalid format', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('handles invalid format (null)', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe((result) => {
      expect(result.items).toEqual([]);
      expect(result.alternateProposals).toEqual([]);
      expect(result.missedShows).toEqual([]);
      expect(result.hasVotes).toBeFalse();
    });
    httpMock.expectOne(`${BASE}/api/schedule`).flush(null);
  });

  it('handles invalid format (string)', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe((result) => {
      expect(result.items).toEqual([]);
      expect(result.hasVotes).toBeFalse();
    });
    httpMock.expectOne(`${BASE}/api/schedule`).flush('bad');
  });
});

describe('ApiService getSchedule filtering', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('filters out invalid items from array format', () => {
    const { httpMock, service } = setup(),
      badItem = { notAShow: true };
    service.getSchedule().subscribe((result) => {
      expect(result.items.length).toBe(ONE_ITEM);
    });
    httpMock
      .expectOne(`${BASE}/api/schedule`)
      .flush([mockScheduleItem, badItem]);
  });

  it('filters out invalid items from object format', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe((result) => {
      expect(result.items.length).toBe(ONE_ITEM);
    });
    httpMock.expectOne(`${BASE}/api/schedule`).flush({
      alternateProposals: [],
      hasVotes: true,
      items: [mockScheduleItem, { garbage: true }],
      missedShows: [],
    });
  });

});

describe('ApiService getSchedule filtering alternateProposals and missedShows', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('filters out invalid alternateProposals', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe((result) => {
      expect(result.alternateProposals.length).toBe(ONE_PROPOSAL);
    });
    httpMock.expectOne(`${BASE}/api/schedule`).flush({
      alternateProposals: [
        { description: 'Alt', excludedMemberName: 'Alice', items: [] },
        { notAProposal: true },
      ],
      hasVotes: false,
      items: [],
      missedShows: [],
    });
  });

  it('filters out invalid missedShows', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe((result) => {
      expect(result.missedShows.length).toBe(ONE_MISSED_SHOW);
    });
    httpMock.expectOne(`${BASE}/api/schedule`).flush({
      alternateProposals: [],
      hasVotes: false,
      items: [],
      missedShows: [
        { blockedByMembers: [], conflictsWithScheduled: true, show: mockShow },
        { notAMissed: true },
      ],
    });
  });
});

describe('ApiService getSchedule hasVotes inference', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('infers hasVotes from items when hasVotes is missing', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe((result) => {
      expect(result.hasVotes).toBeTrue();
    });
    httpMock.expectOne(`${BASE}/api/schedule`).flush({
      alternateProposals: [],
      items: [mockScheduleItem],
      missedShows: [],
    });
  });

  it('uses hasVotes=false when items is empty and field is absent', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe((result) => {
      expect(result.hasVotes).toBeFalse();
    });
    httpMock
      .expectOne(`${BASE}/api/schedule`)
      .flush({ alternateProposals: [], items: [], missedShows: [] });
  });
});

describe('ApiService getSchedule errors', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('propagates HTTP errors', () => {
    const { httpMock, service } = setup();
    service.getSchedule().subscribe({
      error: (error: HttpErrorResponse) => {
        expect(error).toBeTruthy();
      },
    });
    httpMock
      .expectOne(`${BASE}/api/schedule`)
      .flush('Error', { status: 400, statusText: 'Bad Request' });
  });
});
