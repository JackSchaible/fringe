import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { Show } from '../../../models';
import { ShowsFiltersComponent } from './shows-filters';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const ZERO_COUNT = 0,
  ONE_COUNT = 1,
  PRICE_MIN = 0,
  PRICE_MAX = 20,
  DURATION_MIN = 60,
  DURATION_MAX = 120;

const show1: Show = {
    contentRating: { code: 'PG', name: 'Parental Guidance' },
    fee: '1.00',
    lengthInMinutes: DURATION_MIN,
    price: `${PRICE_MIN}`,
    showId: 1,
    showTimes: [],
    tag: 'Drama',
    title: 'Hamlet',
    venue: { address: '1 Main St', name: 'Venue A', phone: '555-0100' },
  },
  show2: Show = {
    contentRating: { code: '14A', name: 'Restricted' },
    fee: '1.00',
    lengthInMinutes: DURATION_MAX,
    price: `${PRICE_MAX}`,
    showId: 2,
    showTimes: [],
    tag: 'Comedy',
    title: 'Cats',
    venue: { address: '2 Side St', name: 'Venue B', phone: '555-0200' },
  },
  noVenueShow: Show = {
    contentRating: show1.contentRating,
    fee: show1.fee,
    lengthInMinutes: show1.lengthInMinutes,
    price: show1.price,
    showId: show1.showId,
    showTimes: show1.showTimes,
    tag: show1.tag,
    title: show1.title,
  };

interface BuildOptions {
  readonly shows?: ReadonlyArray<Show>;
  readonly overrides?: Readonly<Record<string, unknown>>;
}

const setAllInputs = (
  fixture: ComponentFixture<ShowsFiltersComponent>,
  inputs: Readonly<Record<string, unknown>>,
): void => {
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
};

const buildCollapsed = async (
  options: BuildOptions = {},
): Promise<ComponentFixture<ShowsFiltersComponent>> => {
  const { shows = [show1, show2], overrides = {} } = options;
  TestBed.configureTestingModule({
    imports: [ShowsFiltersComponent],
    providers: [provideZonelessChangeDetection()],
  });
  await TestBed.compileComponents();
  const fixture = TestBed.createComponent(ShowsFiltersComponent);
  setAllInputs(fixture, {
    durationBounds: [DURATION_MIN, DURATION_MAX],
    durationRange: [DURATION_MIN, DURATION_MAX],
    priceBounds: [PRICE_MIN, PRICE_MAX],
    priceRange: [PRICE_MIN, PRICE_MAX],
    selectedGenres: new Set<string>(),
    selectedLocations: new Set<string>(),
    selectedRatings: new Set<string>(),
    shows,
    ...overrides,
  });
  fixture.detectChanges();
  return fixture;
};

const build = async (
  options: BuildOptions = {},
): Promise<ComponentFixture<ShowsFiltersComponent>> => {
  const fixture = await buildCollapsed(options);
  getNativeElement(fixture)
    .querySelector<HTMLElement>('.filters-header')
    ?.click();
  fixture.detectChanges();
  return fixture;
};

describe('ShowsFiltersComponent collapse behavior', () => {
  it('renders the "Filters" header title', async () => {
    const fixture = await buildCollapsed();
    expect(
      getNativeElement(fixture).querySelector('.filters-title')?.textContent,
    ).toContain('Filters');
  });

  it('is collapsed by default, hiding the pill groups', async () => {
    const fixture = await buildCollapsed();
    expect(getNativeElement(fixture).querySelector('fg-pill-group')).toBeNull();
  });

  it('expands to show the pill groups when the header is clicked', async () => {
    const fixture = await buildCollapsed();
    getNativeElement(fixture)
      .querySelector<HTMLElement>('.filters-header')
      ?.click();
    fixture.detectChanges();
    expect(
      getNativeElement(fixture).querySelector('fg-pill-group'),
    ).not.toBeNull();
  });

  it('collapses again when the header is clicked twice', async () => {
    const fixture = await buildCollapsed(),
      header =
        getNativeElement(fixture).querySelector<HTMLElement>('.filters-header');
    header?.click();
    fixture.detectChanges();
    header?.click();
    fixture.detectChanges();
    expect(getNativeElement(fixture).querySelector('fg-pill-group')).toBeNull();
  });
});

describe('ShowsFiltersComponent option lists', () => {
  it('lists distinct genre pills sorted alphabetically', async () => {
    const fixture = await build(),
      pills = getNativeElement(fixture).querySelectorAll(
        'fg-pill-group:nth-of-type(1) .pill',
      );
    expect([...pills].map((pill) => pill.textContent.trim())).toEqual([
      'Comedy',
      'Drama',
    ]);
  });

  it('lists distinct rating pills by code', async () => {
    const fixture = await build(),
      pills = getNativeElement(fixture).querySelectorAll(
        'fg-pill-group:nth-of-type(2) .pill',
      );
    expect([...pills].map((pill) => pill.textContent.trim())).toEqual([
      '14A',
      'PG',
    ]);
  });

  it('lists distinct location pills sorted alphabetically', async () => {
    const fixture = await build(),
      pills = getNativeElement(fixture).querySelectorAll(
        'fg-pill-group:nth-of-type(3) .pill',
      );
    expect([...pills].map((pill) => pill.textContent.trim())).toEqual([
      'Venue A',
      'Venue B',
    ]);
  });

  it('omits shows without a venue from location pills', async () => {
    const fixture = await build({ shows: [noVenueShow, show2] }),
      pills = getNativeElement(fixture).querySelectorAll(
        'fg-pill-group:nth-of-type(3) .pill',
      );
    expect([...pills].map((pill) => pill.textContent.trim())).toEqual([
      'Venue B',
    ]);
  });
});

describe('ShowsFiltersComponent location pill fallback', () => {
  it('falls back to the address for a location pill when the venue name is "Unknown"', async () => {
    const showWithUnknownVenueName: Show = {
        ...show1,
        venue: { address: '10330 84 Ave NW', name: 'Unknown', phone: '' },
      },
      fixture = await build({ shows: [showWithUnknownVenueName, show2] }),
      pills = getNativeElement(fixture).querySelectorAll(
        'fg-pill-group:nth-of-type(3) .pill',
      );
    expect([...pills].map((pill) => pill.textContent.trim())).toEqual([
      '10330 84 Ave NW',
      'Venue B',
    ]);
  });
});

describe('ShowsFiltersComponent pill events', () => {
  it('emits toggleGenre when a genre pill is clicked', async () => {
    const fixture = await build(),
      emitted: Array<string> = [];
    fixture.componentInstance.toggleGenre.subscribe((genre) => {
      emitted.push(genre);
    });
    getNativeElement(fixture)
      .querySelector<HTMLButtonElement>('fg-pill-group:nth-of-type(1) .pill')
      ?.click();
    expect(emitted).toEqual(['Comedy']);
  });

  it('emits toggleRating when a rating pill is clicked', async () => {
    const fixture = await build(),
      emitted: Array<string> = [];
    fixture.componentInstance.toggleRating.subscribe((rating) => {
      emitted.push(rating);
    });
    getNativeElement(fixture)
      .querySelector<HTMLButtonElement>('fg-pill-group:nth-of-type(2) .pill')
      ?.click();
    expect(emitted).toEqual(['14A']);
  });

  it('emits toggleLocation when a location pill is clicked', async () => {
    const fixture = await build(),
      emitted: Array<string> = [];
    fixture.componentInstance.toggleLocation.subscribe((location) => {
      emitted.push(location);
    });
    getNativeElement(fixture)
      .querySelector<HTMLButtonElement>('fg-pill-group:nth-of-type(3) .pill')
      ?.click();
    expect(emitted).toEqual(['Venue A']);
  });

  it('marks a selected genre pill as active', async () => {
    const fixture = await build({
      overrides: { selectedGenres: new Set(['Comedy']) },
    });
    expect(
      getNativeElement(fixture).querySelector(
        'fg-pill-group:nth-of-type(1) .pill',
      )?.className,
    ).toContain('active');
  });
});

describe('ShowsFiltersComponent clear button', () => {
  it('is hidden when no filters are active', async () => {
    const fixture = await build();
    expect(getNativeElement(fixture).querySelector('.clear-btn')).toBeNull();
  });

  it('appears once a genre filter is active and emits clear when clicked', async () => {
    const fixture = await build({
        overrides: { selectedGenres: new Set(['Comedy']) },
      }),
      emittedCount = { value: ZERO_COUNT };
    fixture.componentInstance.clear.subscribe(() => {
      emittedCount.value += ONE_COUNT;
    });
    getNativeElement(fixture)
      .querySelector<HTMLButtonElement>('.clear-btn')
      ?.click();
    expect(emittedCount.value).toBe(ONE_COUNT);
  });

  it('appears once the price range narrows below its bounds', async () => {
    const fixture = await build({
      overrides: { priceRange: [PRICE_MIN, PRICE_MAX - ONE_COUNT] },
    });
    expect(
      getNativeElement(fixture).querySelector('.clear-btn'),
    ).not.toBeNull();
  });
});
