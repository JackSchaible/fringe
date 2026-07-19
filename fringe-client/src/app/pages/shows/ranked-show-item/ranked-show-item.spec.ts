import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { RankedShowItemComponent } from './ranked-show-item';
import type { Show } from '../../../models';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const RANK_ONE = 1,
  FRINGE_URL_TEST_ID = 42,
  show1: Show = {
    fee: '$1',
    lengthInMinutes: 60,
    price: '$10',
    showId: FRINGE_URL_TEST_ID,
    showTimes: [],
    tag: 'Drama',
    title: 'Hamlet',
  },
  build = async (): Promise<ComponentFixture<RankedShowItemComponent>> => {
    TestBed.configureTestingModule({
      imports: [RankedShowItemComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(RankedShowItemComponent);
    fixture.componentRef.setInput('show', show1);
    fixture.componentRef.setInput('rank', RANK_ONE);
    fixture.detectChanges();
    return fixture;
  };

describe('RankedShowItemComponent', () => {
  it('renders the show title', async () => {
    const fixture = await build();
    expect(
      getNativeElement(fixture).querySelector('.show-title')?.textContent,
    ).toContain('Hamlet');
  });

  it('renders the rank number', async () => {
    const fixture = await build();
    expect(
      getNativeElement(fixture).querySelector('.rank-num')?.textContent,
    ).toContain(`${RANK_ONE}`);
  });

  it('builds a ticket link that includes the showId', async () => {
    const fixture = await build(),
      link =
        getNativeElement(fixture).querySelector<HTMLAnchorElement>(
          '.ranked-action-btn',
        );
    expect(link?.getAttribute('href')).toBe(
      `https://tickets.fringetheatre.ca/event/601:${FRINGE_URL_TEST_ID}`,
    );
  });

  it('emits viewDetails with the show when the details button is clicked', async () => {
    const fixture = await build(),
      emitted: Array<Show> = [];
    fixture.componentInstance.viewDetails.subscribe((show) => {
      emitted.push(show);
    });
    getNativeElement(fixture)
      .querySelector<HTMLButtonElement>('button[title="Show details"]')
      ?.click();
    expect(emitted).toEqual([show1]);
  });

  it('emits remove with the show when the remove button is clicked', async () => {
    const fixture = await build(),
      emitted: Array<Show> = [];
    fixture.componentInstance.remove.subscribe((show) => {
      emitted.push(show);
    });
    getNativeElement(fixture)
      .querySelector<HTMLButtonElement>('.remove-btn')
      ?.click();
    expect(emitted).toEqual([show1]);
  });
});
