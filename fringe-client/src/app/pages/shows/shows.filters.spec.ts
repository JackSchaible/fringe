import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { Show, Vote } from '../../models';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { ShowsPage } from './shows';
import { provideZonelessChangeDetection } from '@angular/core';

// ── Fixtures ──────────────────────────────────────────────────────────────────

const FILTER_PRICE_LOW = 10,
  FILTER_PRICE_HIGH = 30,
  FILTER_DURATION_SHORT = 60,
  FILTER_DURATION_LONG = 120,
  NARROW_PRICE_MAX = 15,
  NARROW_DURATION_MIN = 100,
  NARROW_DURATION_MAX = 130,
  ZERO_SIZE = 0,
  ONE_SHOW = 1,
  FILTER_SHOW_ONE_ID = 11,
  FILTER_SHOW_TWO_ID = 12;

const filterShow1: Show = {
    contentRating: { code: 'PG', name: 'Parental Guidance' },
    fee: '1.00',
    lengthInMinutes: FILTER_DURATION_SHORT,
    price: `${FILTER_PRICE_LOW}.00`,
    showId: FILTER_SHOW_ONE_ID,
    showTimes: [],
    tag: 'Drama',
    title: 'Filter Show One',
    venue: { address: '1 Main St', name: 'Venue A', phone: '555-0100' },
  },
  filterShow2: Show = {
    contentRating: { code: '14A', name: 'Restricted' },
    fee: '1.00',
    lengthInMinutes: FILTER_DURATION_LONG,
    price: `${FILTER_PRICE_HIGH}.00`,
    showId: FILTER_SHOW_TWO_ID,
    showTimes: [],
    tag: 'Comedy',
    title: 'Filter Show Two',
    venue: { address: '2 Side St', name: 'Venue B', phone: '555-0200' },
  };

const makeApiSpy = (
    shows: Array<Show> = [filterShow1, filterShow2],
    votesResult: Array<Vote> | 'error' = [],
  ): jasmine.SpyObj<ApiService> => {
    const spy = jasmine.createSpyObj<ApiService>('ApiService', [
      'getShows',
      'getVotes',
      'saveVotes',
    ]);
    spy.getShows.and.returnValue(of(shows));
    if (votesResult === 'error') {
      spy.getVotes.and.returnValue(throwError(() => new Error('votes error')));
    } else {
      spy.getVotes.and.returnValue(of(votesResult));
    }
    // eslint-disable-next-line no-undefined
    spy.saveVotes.and.returnValue(of(undefined));
    return spy;
  },
  buildComponent = async (
    api: jasmine.SpyObj<ApiService>,
  ): Promise<{
    component: ShowsPage;
    fixture: ComponentFixture<ShowsPage>;
  }> => {
    TestBed.configureTestingModule({
      imports: [ShowsPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: ApiService, useValue: api },
      ],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(ShowsPage),
      component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    return { component, fixture };
  };

describe('ShowsPage genre filter', () => {
  it('narrows filteredUnranked to the selected genre', async () => {
    const { component } = await buildComponent(makeApiSpy());
    component.toggleGenre('Comedy');
    expect(component.filteredUnranked().map((show) => show.showId)).toEqual([
      filterShow2.showId,
    ]);
  });

  it('clears the filter when the same genre is toggled again', async () => {
    const { component } = await buildComponent(makeApiSpy());
    component.toggleGenre('Comedy');
    component.toggleGenre('Comedy');
    expect(component.filteredUnranked().length).toBeGreaterThan(ONE_SHOW);
  });
});

describe('ShowsPage rating filter', () => {
  it('narrows filteredUnranked to the selected rating code', async () => {
    const { component } = await buildComponent(makeApiSpy());
    component.toggleRating('14A');
    expect(component.filteredUnranked().map((show) => show.showId)).toEqual([
      filterShow2.showId,
    ]);
  });
});

describe('ShowsPage location filter', () => {
  it('narrows filteredUnranked to the selected venue', async () => {
    const { component } = await buildComponent(makeApiSpy());
    component.toggleLocation('Venue A');
    expect(component.filteredUnranked().map((show) => show.showId)).toEqual([
      filterShow1.showId,
    ]);
  });
});

describe('ShowsPage price range', () => {
  it('computes bounds from the loaded shows', async () => {
    const { component } = await buildComponent(makeApiSpy());
    expect(component.priceBounds()).toEqual([
      FILTER_PRICE_LOW,
      FILTER_PRICE_HIGH,
    ]);
  });

  it('narrows filteredUnranked when the range is set', async () => {
    const { component } = await buildComponent(makeApiSpy());
    component.setPriceRange([ZERO_SIZE, NARROW_PRICE_MAX]);
    expect(component.filteredUnranked().map((show) => show.showId)).toEqual([
      filterShow1.showId,
    ]);
  });
});

describe('ShowsPage duration range', () => {
  it('computes bounds from the loaded shows', async () => {
    const { component } = await buildComponent(makeApiSpy());
    expect(component.durationBounds()).toEqual([
      FILTER_DURATION_SHORT,
      FILTER_DURATION_LONG,
    ]);
  });

  it('narrows filteredUnranked when the range is set', async () => {
    const { component } = await buildComponent(makeApiSpy());
    component.setDurationRange([NARROW_DURATION_MIN, NARROW_DURATION_MAX]);
    expect(component.filteredUnranked().map((show) => show.showId)).toEqual([
      filterShow2.showId,
    ]);
  });
});

describe('ShowsPage clearFilters', () => {
  it('resets pill filters back to empty', async () => {
    const { component } = await buildComponent(makeApiSpy());
    component.toggleGenre('Comedy');
    component.toggleRating('14A');
    component.toggleLocation('Venue B');

    component.clearFilters();

    expect(component.selectedGenres().size).toBe(ZERO_SIZE);
    expect(component.selectedRatings().size).toBe(ZERO_SIZE);
    expect(component.selectedLocations().size).toBe(ZERO_SIZE);
  });

  it('resets range filters back to the full bounds', async () => {
    const { component } = await buildComponent(makeApiSpy());
    component.setPriceRange([ZERO_SIZE, NARROW_PRICE_MAX]);
    component.setDurationRange([FILTER_DURATION_SHORT, NARROW_DURATION_MIN]);

    component.clearFilters();

    expect(component.priceRange()).toEqual(component.priceBounds());
    expect(component.durationRange()).toEqual(component.durationBounds());
  });
});
