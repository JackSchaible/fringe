import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { BrowseShowCardComponent } from './browse-show-card';
import type { Show } from '../../../models';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const FRINGE_URL_TEST_ID = 42,
  show1: Show = {
    fee: '1.00',
    lengthInMinutes: 60,
    price: '10.00',
    showId: FRINGE_URL_TEST_ID,
    showTimes: [],
    tag: 'Drama',
    title: 'Hamlet',
  },
  freeShow: Show = {
    fee: '0.00',
    lengthInMinutes: 60,
    price: '0.00',
    showId: FRINGE_URL_TEST_ID,
    showTimes: [],
    title: 'Free Show',
  },
  build = async (
    show: Readonly<Show>,
  ): Promise<ComponentFixture<BrowseShowCardComponent>> => {
    TestBed.configureTestingModule({
      imports: [BrowseShowCardComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(BrowseShowCardComponent);
    fixture.componentRef.setInput('show', show);
    fixture.detectChanges();
    return fixture;
  };

describe('BrowseShowCardComponent', () => {
  it('renders the show title', async () => {
    const fixture = await build(show1);
    expect(
      getNativeElement(fixture).querySelector('.card-title')?.textContent,
    ).toContain('Hamlet');
  });

  it('renders the price', async () => {
    const fixture = await build(show1);
    expect(
      getNativeElement(fixture).querySelector('.price')?.textContent,
    ).toContain('$10.00');
  });

  it('renders "Free" when price is zero', async () => {
    const fixture = await build(freeShow);
    expect(
      getNativeElement(fixture).querySelector('.price')?.textContent,
    ).toContain('Free');
  });

  it('builds a ticket link that includes the showId', async () => {
    const fixture = await build(show1),
      link =
        getNativeElement(fixture).querySelector<HTMLAnchorElement>('.price');
    expect(link?.getAttribute('href')).toBe(
      `https://tickets.fringetheatre.ca/event/601:${FRINGE_URL_TEST_ID}`,
    );
  });
});

describe('BrowseShowCardComponent events', () => {
  it('emits add with the show when the card is clicked', async () => {
    const fixture = await build(show1),
      emitted: Array<Show> = [];
    fixture.componentInstance.add.subscribe((show) => {
      emitted.push(show);
    });
    getNativeElement(fixture).querySelector<HTMLElement>('.show-card')?.click();
    expect(emitted).toEqual([show1]);
  });

  it('emits viewDetails with the show when the details button is clicked', async () => {
    const fixture = await build(show1),
      emitted: Array<Show> = [];
    fixture.componentInstance.viewDetails.subscribe((show) => {
      emitted.push(show);
    });
    getNativeElement(fixture)
      .querySelector<HTMLButtonElement>('.card-action-btn')
      ?.click();
    expect(emitted).toEqual([show1]);
  });
});
