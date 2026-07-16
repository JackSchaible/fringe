import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { Show, Vote } from '../../models';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { ShowsPage } from './shows';
import { provideZonelessChangeDetection } from '@angular/core';

// ── Fixtures ──────────────────────────────────────────────────────────────────

const show1: Show = {
    fee: '$1',
    lengthInMinutes: 90,
    price: '$10',
    showId: 1,
    showTimes: [],
    tag: 'Drama',
    title: 'Hamlet',
    venue: { address: '1 Main St', name: 'Venue A', phone: '555-0100' },
  },
  show2: Show = {
    fee: '$1',
    lengthInMinutes: 120,
    price: '$12',
    showId: 2,
    showTimes: [],
    tag: 'Musical',
    title: 'Cats',
    venue: { address: '2 Side St', name: 'Venue B', phone: '555-0200' },
  },
  show3: Show = {
    fee: '$1',
    lengthInMinutes: 60,
    price: '$8',
    showId: 3,
    showTimes: [],
    tag: 'Comedy',
    title: 'Comedy Night',
  },
  votes: Array<Vote> = [
    { rank: 1, showId: 2 },
    { rank: 2, showId: 1 },
  ],
  FIRST_INDEX = 0,
  FIXTURE_SHOW_COUNT = 3,
  SECOND_INDEX = 1;

const makeApiSpy = (
    shows: Array<Show> = [show1, show2, show3],
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
    // Calling of() with no args emits nothing, silently skipping the subscriber's next callback under test.
    // An explicit undefined is required to match Observable<void>'s actual runtime emission.
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

describe('ShowsPage ngOnInit — no existing votes', () => {
  it('loads shows and sets loading false', async () => {
    const { component } = await buildComponent(
      makeApiSpy([show1, show2, show3], []),
    );
    expect(component.loading()).toBeFalse();
  });

  it('noShows is false when shows are loaded', async () => {
    const { component } = await buildComponent(
      makeApiSpy([show1, show2, show3], []),
    );
    expect(component.noShows()).toBeFalse();
  });

  it('rankedShows starts empty when no votes', async () => {
    const { component } = await buildComponent(
      makeApiSpy([show1, show2, show3], []),
    );
    expect(component.rankedShows()).toEqual([]);
  });

  it('filteredUnranked returns all shows when no query', async () => {
    const { component } = await buildComponent(
      makeApiSpy([show1, show2, show3], []),
    );
    expect(component.filteredUnranked().length).toBe(FIXTURE_SHOW_COUNT);
  });
});

describe('ShowsPage ngOnInit — with existing votes', () => {
  it('populates rankedShows in vote rank order', async () => {
    const { component } = await buildComponent(
      makeApiSpy([show1, show2, show3], votes),
    );
    expect(component.rankedShows()[FIRST_INDEX].showId).toBe(show2.showId);
    expect(component.rankedShows()[SECOND_INDEX].showId).toBe(show1.showId);
  });

  it('excludes ranked shows from filteredUnranked', async () => {
    const { component } = await buildComponent(
        makeApiSpy([show1, show2, show3], votes),
      ),
      unranked = component.filteredUnranked(),
      ids = unranked.map((show) => show.showId);
    expect(ids).not.toContain(show1.showId);
    expect(ids).not.toContain(show2.showId);
    expect(ids).toContain(show3.showId);
  });
});

describe('ShowsPage ngOnInit — empty shows', () => {
  it('noShows is true when shows array is empty', async () => {
    const { component } = await buildComponent(makeApiSpy([], []));
    expect(component.noShows()).toBeTrue();
  });
});

describe('ShowsPage filteredUnranked — search', () => {
  it('filters by title case-insensitively', async () => {
    const { component } = await buildComponent(
      makeApiSpy([show1, show2, show3], []),
    );
    component.searchQuery.set('hamlet');
    expect(component.filteredUnranked().map((show) => show.showId)).toEqual([
      show1.showId,
    ]);
  });

  it('filters by tag', async () => {
    const { component } = await buildComponent(
      makeApiSpy([show1, show2, show3], []),
    );
    component.searchQuery.set('musical');
    expect(component.filteredUnranked().map((show) => show.showId)).toEqual([
      show2.showId,
    ]);
  });

  it('filters by venue name', async () => {
    const { component } = await buildComponent(
      makeApiSpy([show1, show2, show3], []),
    );
    component.searchQuery.set('venue a');
    expect(component.filteredUnranked().map((show) => show.showId)).toEqual([
      show1.showId,
    ]);
  });
});

describe('ShowsPage filteredUnranked — search edge cases', () => {
  it('returns all shows when query is empty string', async () => {
    const { component } = await buildComponent(
      makeApiSpy([show1, show2, show3], []),
    );
    component.searchQuery.set('');
    expect(component.filteredUnranked().length).toBe(FIXTURE_SHOW_COUNT);
  });

  it('returns empty array when no shows match query', async () => {
    const { component } = await buildComponent(
      makeApiSpy([show1, show2, show3], []),
    );
    component.searchQuery.set('zzznomatch');
    expect(component.filteredUnranked()).toEqual([]);
  });

  it('handles shows without venue gracefully', async () => {
    const { component } = await buildComponent(
      makeApiSpy([show1, show2, show3], []),
    );
    component.searchQuery.set('comedy');
    expect(component.filteredUnranked().map((show) => show.showId)).toContain(
      show3.showId,
    );
  });
});

describe('ShowsPage selectedShow', () => {
  it('starts as null', async () => {
    const { component } = await buildComponent(makeApiSpy([show1, show2], []));
    expect(component.selectedShow()).toBeNull();
  });

  it('can be set directly via the signal', async () => {
    const { component } = await buildComponent(makeApiSpy([show1, show2], []));
    component.selectedShow.set(show1);
    expect(component.selectedShow()).toEqual(show1);
  });
});

describe('ShowsPage addToRanked', () => {
  it('adds show to rankedShows', async () => {
    const { component } = await buildComponent(makeApiSpy([show1, show2], []));
    component.addToRanked(show1);
    expect(component.rankedShows()).toContain(show1);
  });

  it('calls saveVotes after adding', async () => {
    const apiSpy = makeApiSpy([show1, show2], []),
      { component } = await buildComponent(apiSpy);
    component.addToRanked(show1);
    expect(apiSpy.saveVotes).toHaveBeenCalled();
  });

  it('removes show from filteredUnranked after adding', async () => {
    const { component } = await buildComponent(makeApiSpy([show1, show2], []));
    component.addToRanked(show1);
    expect(
      component.filteredUnranked().map((show) => show.showId),
    ).not.toContain(show1.showId);
  });

  it('saveVotes sends correct rank payload', async () => {
    const apiSpy = makeApiSpy([show1, show2], []),
      { component } = await buildComponent(apiSpy);
    component.addToRanked(show1);
    component.addToRanked(show2);
    const lastCall = apiSpy.saveVotes.calls.mostRecent().args[FIRST_INDEX];
    expect(lastCall[FIRST_INDEX]).toEqual({ rank: 1, showId: 1 });
    expect(lastCall[SECOND_INDEX]).toEqual({ rank: 2, showId: 2 });
  });
});

describe('ShowsPage removeFromRanked', () => {
  it('removes show from rankedShows', async () => {
    const { component } = await buildComponent(
      makeApiSpy([show1, show2], votes),
    );
    component.removeFromRanked(show2);
    expect(component.rankedShows().map((show) => show.showId)).not.toContain(
      show2.showId,
    );
  });

  it('calls saveVotes after removing', async () => {
    const apiSpy = makeApiSpy([show1, show2], votes),
      { component } = await buildComponent(apiSpy),
      countBefore = apiSpy.saveVotes.calls.count();
    component.removeFromRanked(show1);
    expect(apiSpy.saveVotes.calls.count()).toBeGreaterThan(countBefore);
  });
});

describe('ShowsPage saving signal', () => {
  it('resets to false after save completes', async () => {
    const apiSpy = makeApiSpy([show1], []),
      { component } = await buildComponent(apiSpy);
    component.addToRanked(show1);
    // SaveVotes observable completes synchronously in tests
    expect(component.saving()).toBeFalse();
  });
});
