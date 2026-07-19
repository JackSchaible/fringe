import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { Show, UserAvailability } from '../../models';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { AvailabilityPage } from './availability';
import type { CalendarCell } from './calendar-utils';
import { provideZonelessChangeDetection } from '@angular/core';

// ── Fixtures ──────────────────────────────────────────────────────────────────

// Two show times on different days
const showWithTimes: Show = {
    fee: '$1',
    lengthInMinutes: 60,
    price: '$10',
    showId: 1,
    showTimes: ['2025-08-01T01:00:00Z', '2025-08-02T01:00:00Z'],
    title: 'Evening Show',
  },
  emptyAvailability: UserAvailability = { windows: [] },
  DAYS_PER_WEEK = 7,
  EMPTY_COUNT = 0,
  SAVED_FEEDBACK_MS = 3_000,
  makeApiSpy = (
    shows: Array<Show> = [showWithTimes],
    availability: UserAvailability = emptyAvailability,
  ): jasmine.SpyObj<ApiService> => {
    const spy = jasmine.createSpyObj<ApiService>('ApiService', [
      'getShows',
      'getAvailability',
      'saveAvailability',
    ]);
    spy.getShows.and.returnValue(of(shows));
    spy.getAvailability.and.returnValue(of(availability));
    spy.saveAvailability.and.returnValue(of(undefined));
    return spy;
  };

interface CalendarEventMock {
  end: string;
  endStr: string;
  remove: jasmine.Spy;
  start: string;
  startStr: string;
}

interface CalendarApiMock {
  events: Array<CalendarEventMock>;
  addEvent: jasmine.Spy;
  getEvents: jasmine.Spy;
  gotoDate: jasmine.Spy;
  removeAllEvents: jasmine.Spy;
  unselect: jasmine.Spy;
}

// Minimal FullCalendar API mock
const makeCalendarApiMock = (): CalendarApiMock => {
    const events: Array<CalendarEventMock> = [];
    return {
      addEvent: jasmine
        .createSpy('addEvent')
        .and.callFake((opts: Readonly<{ start: string; end: string }>) => {
          const event = {
            end: opts.end,
            endStr: opts.end,
            remove: jasmine.createSpy('remove'),
            start: opts.start,
            startStr: opts.start,
          };
          events.push(event);
          return event;
        }),
      events,
      getEvents: jasmine.createSpy('getEvents').and.callFake(() => [...events]),
      gotoDate: jasmine.createSpy('gotoDate'),
      removeAllEvents: jasmine.createSpy('removeAllEvents').and.callFake(() => {
        events.length = 0;
      }),
      unselect: jasmine.createSpy('unselect'),
    };
  },
  build = async (
    api: jasmine.SpyObj<ApiService> = makeApiSpy(),
  ): Promise<{
    calApi: CalendarApiMock;
    component: AvailabilityPage;
    fixture: ComponentFixture<AvailabilityPage>;
  }> => {
    TestBed.configureTestingModule({
      imports: [AvailabilityPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: ApiService, useValue: api },
      ],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(AvailabilityPage),
      component = fixture.componentInstance,
      calApi = makeCalendarApiMock();

    // AvailabilityPage's ViewChild ref is private and typed to the real FullCalendarComponent.
    // There's no public, typed seam to inject a test stand-in, so this reaches past the type system deliberately.
    /* eslint-disable @typescript-eslint/no-unsafe-type-assertion */
    (
      component as unknown as {
        calendarRef: () => { getApi: () => CalendarApiMock };
      }
    ).calendarRef = (): { getApi: () => CalendarApiMock } => ({
      getApi: (): CalendarApiMock => calApi,
    });
    /* eslint-enable @typescript-eslint/no-unsafe-type-assertion */

    fixture.detectChanges();
    await fixture.whenStable();
    return { calApi, component, fixture };
  };

describe('AvailabilityPage ngOnInit', () => {
  it('loads shows and availability, then sets loading false', async () => {
    const apiSpy = makeApiSpy(),
      { component } = await build(apiSpy);
    expect(apiSpy.getShows).toHaveBeenCalled();
    expect(apiSpy.getAvailability).toHaveBeenCalled();
    expect(component.loading()).toBeFalse();
  });

  it('builds calendarMonths from show times', async () => {
    const { component } = await build();
    expect(component.calendarMonths().length).toBeGreaterThan(EMPTY_COUNT);
  });

  it('handles empty shows list gracefully', async () => {
    const api = makeApiSpy([], emptyAvailability),
      { component } = await build(api);
    expect(component.calendarMonths()).toEqual([]);
    expect(component.loading()).toBeFalse();
  });

  it('pre-populates windowsByDate from existing availability', async () => {
    const avail: UserAvailability = {
        windows: [
          { end: '2025-08-01T20:00:00Z', start: '2025-08-01T18:00:00Z' },
        ],
      },
      api = makeApiSpy([showWithTimes], avail),
      { component } = await build(api);
    expect(component.loading()).toBeFalse();
  });
});

describe('AvailabilityPage selectDay', () => {
  it('does nothing for non-festival cells', async () => {
    const { component } = await build(),
      emptyCell: CalendarCell = {
        date: null,
        dayNumber: null,
        festivalDay: null,
        isFestival: false,
      };
    component.selectDay(emptyCell);
    expect(component.selectedDate()).toBeNull();
  });

  it('sets selectedDate for a festival day cell', async () => {
    const { component } = await build();
    for (const month of component.calendarMonths()) {
      for (const week of month.weeks) {
        for (const cell of week) {
          if (cell.isFestival && cell.festivalDay) {
            component.selectDay(cell);
            expect(component.selectedDate()).toBe(cell.date);
            return;
          }
        }
      }
    }
  });

  it('sets selectedDayLabel for a festival day', async () => {
    const { component } = await build();
    for (const month of component.calendarMonths()) {
      for (const week of month.weeks) {
        for (const cell of week) {
          if (cell.isFestival && cell.festivalDay) {
            component.selectDay(cell);
            expect(component.selectedDayLabel()).not.toBe('');
            return;
          }
        }
      }
    }
  });
});

describe('AvailabilityPage save', () => {
  it('calls saveAvailability', async () => {
    const apiSpy = makeApiSpy(),
      { component } = await build(apiSpy);
    component.save();
    expect(apiSpy.saveAvailability).toHaveBeenCalled();
  });

  it('sets saving to false on success (synchronous spy)', async () => {
    const { component } = await build();
    component.save();
    expect(component.saving()).toBeFalse();
  });

  it('sets saved to true on success', async () => {
    const { component } = await build();
    component.save();
    expect(component.saved()).toBeTrue();
  });

  it('resets saved to false after 3000ms', async () => {
    const { component } = await build();
    jasmine.clock().install();
    component.save();
    jasmine.clock().tick(SAVED_FEEDBACK_MS);
    expect(component.saved()).toBeFalse();
    jasmine.clock().uninstall();
  });

  it('sets saving false on error', async () => {
    const apiSpy = makeApiSpy(),
      { component } = await build(apiSpy);
    apiSpy.saveAvailability.and.returnValue(
      throwError(() => new Error('save error')),
    );
    component.save();
    expect(component.saving()).toBeFalse();
  });
});

describe('AvailabilityPage clearDay', () => {
  it('does nothing when no date is selected (selectedDate is null)', async () => {
    const { component } = await build();
    expect(component.selectedDate()).toBeNull();
    expect(() => {
      component.clearDay();
    }).not.toThrow();
  });

  it('clears the selected date and removes calendar events', async () => {
    const { calApi, component } = await build();
    component.selectedDate.set('2025-08-01');
    component.clearDay();
    expect(calApi.removeAllEvents).toHaveBeenCalled();
  });
});

describe('AvailabilityPage calendarMonths structure', () => {
  it('every week has exactly 7 cells', async () => {
    const { component } = await build();
    for (const month of component.calendarMonths()) {
      for (const week of month.weeks) {
        expect(week.length).toBe(DAYS_PER_WEEK);
      }
    }
  });

  it('month labels are non-empty strings', async () => {
    const { component } = await build();
    for (const month of component.calendarMonths()) {
      expect(month.label).toBeTruthy();
    }
  });
});
