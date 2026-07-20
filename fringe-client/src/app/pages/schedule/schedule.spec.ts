import {
  ActivatedRoute,
  Router,
  convertToParamMap,
  provideRouter,
} from '@angular/router';
import type {
  AlternateProposal,
  MissedShow,
  ScheduleItem,
  ScheduleResponse,
  Show,
} from '../../models';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { SchedulePage } from './schedule';
import { provideZonelessChangeDetection } from '@angular/core';

// ── Fixtures ──────────────────────────────────────────────────────────────────

const show1: Show = {
    fee: '$1',
    lengthInMinutes: 60,
    price: '$10',
    showId: 1,
    showTimes: ['2025-08-01T19:00:00Z'],
    title: 'Show One',
  },
  show2: Show = {
    fee: '$1',
    lengthInMinutes: 90,
    price: '$12',
    showId: 2,
    showTimes: ['2025-08-01T21:00:00Z'],
    title: 'Show Two',
  },
  item1: ScheduleItem = {
    groupScore: 10,
    show: show1,
    showTime: '2025-08-01T19:00:00Z',
  },
  item2: ScheduleItem = {
    groupScore: 8,
    show: show2,
    showTime: '2025-08-01T21:00:00Z',
  },
  proposal1: AlternateProposal = {
    description: 'Alt without Bob',
    excludedMemberName: 'Bob',
    items: [item2],
  },
  missedShow1: MissedShow = {
    blockedByMembers: ['Bob'],
    conflictsWithScheduled: true,
    show: show2,
  },
  FIRST_PROPOSAL = 0,
  OUT_OF_BOUNDS_INDEX = 99,
  makeApiSpy = (
    scheduleResponse: ScheduleResponse = {
      alternateProposals: [proposal1],
      hasVotes: true,
      items: [item1],
      missedShows: [missedShow1],
      travelMode: 'walking',
    },
  ): jasmine.SpyObj<ApiService> => {
    const spy = jasmine.createSpyObj<ApiService>('ApiService', ['getSchedule']);
    spy.getSchedule.and.returnValue(of(scheduleResponse));
    return spy;
  },
  navigateSpies = new WeakMap<ComponentFixture<SchedulePage>, jasmine.Spy>(),
  buildComponent = async (
    api: jasmine.SpyObj<ApiService>,
    queryParams: Readonly<Record<string, string>> = {},
  ): Promise<ComponentFixture<SchedulePage>> => {
    TestBed.configureTestingModule({
      imports: [SchedulePage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: ApiService, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap(queryParams) },
          },
        },
      ],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(SchedulePage),
      navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.returnValue(
        Promise.resolve(true),
      );
    navigateSpies.set(fixture, navigateSpy);
    fixture.detectChanges();
    return fixture;
  };

describe('SchedulePage ngOnInit success', () => {
  it('calls getSchedule on init', async () => {
    const apiSpy = makeApiSpy();
    await buildComponent(apiSpy);
    expect(apiSpy.getSchedule).toHaveBeenCalled();
  });

  it('sets hasVotes signal', async () => {
    const { componentInstance: component } = await buildComponent(makeApiSpy());
    expect(component.hasVotes()).toBeTrue();
  });

  it('sets schedule signal', async () => {
    const { componentInstance: component } = await buildComponent(makeApiSpy());
    expect(component.schedule()).toEqual([item1]);
  });

  it('sets proposals signal', async () => {
    const { componentInstance: component } = await buildComponent(makeApiSpy());
    expect(component.proposals()).toEqual([proposal1]);
  });

  it('sets missedShows signal', async () => {
    const { componentInstance: component } = await buildComponent(makeApiSpy());
    expect(component.missedShows()).toEqual([missedShow1]);
  });

  it('sets loading to false after response', async () => {
    const { componentInstance: component } = await buildComponent(makeApiSpy());
    expect(component.loading()).toBeFalse();
  });
});

describe('SchedulePage ngOnInit 400 error', () => {
  const build = async (): Promise<ComponentFixture<SchedulePage>> => {
    const apiSpy = jasmine.createSpyObj<ApiService>('ApiService', [
      'getSchedule',
    ]);
    apiSpy.getSchedule.and.returnValue(throwError(() => ({ status: 400 })));
    return buildComponent(apiSpy);
  };

  it('sets noGroup to true', async () => {
    const { componentInstance: component } = await build();
    expect(component.noGroup()).toBeTrue();
  });

  it('sets loading to false', async () => {
    const { componentInstance: component } = await build();
    expect(component.loading()).toBeFalse();
  });
});

describe('SchedulePage ngOnInit 500 error', () => {
  const build = async (): Promise<ComponentFixture<SchedulePage>> => {
    const apiSpy = jasmine.createSpyObj<ApiService>('ApiService', [
      'getSchedule',
    ]);
    apiSpy.getSchedule.and.returnValue(throwError(() => ({ status: 500 })));
    return buildComponent(apiSpy);
  };

  it('does NOT set noGroup', async () => {
    const { componentInstance: component } = await build();
    expect(component.noGroup()).toBeFalse();
  });

  it('sets loading to false', async () => {
    const { componentInstance: component } = await build();
    expect(component.loading()).toBeFalse();
  });
});

describe('SchedulePage activeSchedule', () => {
  const build = async (): Promise<ComponentFixture<SchedulePage>> =>
    buildComponent(
      makeApiSpy({
        alternateProposals: [proposal1],
        hasVotes: true,
        items: [item1],
        missedShows: [],
        travelMode: 'walking',
      }),
    );

  it('returns main schedule when no proposal is active', async () => {
    const { componentInstance: component } = await build();
    expect(component.activeSchedule()).toEqual([item1]);
  });

  it('returns proposal items when a proposal index is active', async () => {
    const { componentInstance: component } = await build();
    component.acceptProposal(FIRST_PROPOSAL);
    expect(component.activeSchedule()).toEqual(proposal1.items);
  });

  it('falls back to main schedule if proposal index is out of bounds', async () => {
    const { componentInstance: component } = await build();
    component.activeProposalIndex.set(OUT_OF_BOUNDS_INDEX);
    expect(component.activeSchedule()).toEqual([item1]);
  });
});

describe('SchedulePage acceptProposal', () => {
  it('sets activeProposalIndex', async () => {
    const { componentInstance: component } = await buildComponent(makeApiSpy());
    component.acceptProposal(FIRST_PROPOSAL);
    expect(component.activeProposalIndex()).toBe(FIRST_PROPOSAL);
  });
});

describe('SchedulePage resetToMain', () => {
  it('sets activeProposalIndex to null', async () => {
    const { componentInstance: component } = await buildComponent(makeApiSpy());
    component.acceptProposal(FIRST_PROPOSAL);
    component.resetToMain();
    expect(component.activeProposalIndex()).toBeNull();
  });
});

describe('SchedulePage initial travel mode', () => {
  it('defaults to walking when no mode query param is present', async () => {
    const apiSpy = makeApiSpy();
    await buildComponent(apiSpy);
    expect(apiSpy.getSchedule).toHaveBeenCalledWith('walking');
  });

  it('reads the mode from the query params when present', async () => {
    const apiSpy = makeApiSpy({
      alternateProposals: [],
      hasVotes: true,
      items: [item1],
      missedShows: [],
      travelMode: 'cycling',
    });
    await buildComponent(apiSpy, { mode: 'cycling' });
    expect(apiSpy.getSchedule).toHaveBeenCalledWith('cycling');
  });

  it('falls back to walking for an unrecognized mode query param', async () => {
    const apiSpy = makeApiSpy();
    await buildComponent(apiSpy, { mode: 'teleporting' });
    expect(apiSpy.getSchedule).toHaveBeenCalledWith('walking');
  });

  it('reflects the mode confirmed by the response', async () => {
    const apiSpy = makeApiSpy({
      alternateProposals: [],
      hasVotes: true,
      items: [item1],
      missedShows: [],
      travelMode: 'cycling',
    });
    const { componentInstance: component } = await buildComponent(apiSpy, {
      mode: 'cycling',
    });
    expect(component.travelMode()).toBe('cycling');
  });
});

describe('SchedulePage setTravelMode', () => {
  it('refetches the schedule with the new mode', async () => {
    const apiSpy = makeApiSpy();
    const { componentInstance: component } = await buildComponent(apiSpy);
    component.setTravelMode('driving');
    expect(apiSpy.getSchedule).toHaveBeenCalledWith('driving');
  });

  it('updates the router query params', async () => {
    const apiSpy = makeApiSpy(),
      fixture = await buildComponent(apiSpy),
      navigateSpy = navigateSpies.get(fixture);
    navigateSpy?.calls.reset();
    fixture.componentInstance.setTravelMode('driving');
    expect(navigateSpy).toHaveBeenCalled();
  });

  it('does nothing when the requested mode already matches the current mode', async () => {
    const apiSpy = makeApiSpy();
    const { componentInstance: component } = await buildComponent(apiSpy);
    apiSpy.getSchedule.calls.reset();
    component.setTravelMode('walking');
    expect(apiSpy.getSchedule).not.toHaveBeenCalled();
  });
});
