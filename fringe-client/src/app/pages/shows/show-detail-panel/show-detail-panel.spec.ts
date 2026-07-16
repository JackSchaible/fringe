import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { Show } from '../../../models';
import { ShowDetailPanelComponent } from './show-detail-panel';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const FRINGE_URL_TEST_ID = 42,
  show1: Show = {
    contentRating: { code: 'PG', name: 'Parental Guidance' },
    description: 'A gripping drama.',
    fee: '1.00',
    lengthInMinutes: 90,
    plainTextDescription: 'A gripping drama.',
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
  ): Promise<ComponentFixture<ShowDetailPanelComponent>> => {
    TestBed.configureTestingModule({
      imports: [ShowDetailPanelComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(ShowDetailPanelComponent);
    fixture.componentRef.setInput('show', show);
    fixture.detectChanges();
    return fixture;
  };

describe('ShowDetailPanelComponent', () => {
  it('renders the show title', async () => {
    const fixture = await build(show1);
    expect(
      getNativeElement(fixture).querySelector('.detail-title')?.textContent,
    ).toContain('Hamlet');
  });

  it('renders the tag chip', async () => {
    const fixture = await build(show1);
    expect(
      getNativeElement(fixture).querySelector('.chip')?.textContent,
    ).toContain('Drama');
  });

  it('renders the content rating chip', async () => {
    const fixture = await build(show1);
    expect(
      getNativeElement(fixture).querySelector('.chip-rating')?.textContent,
    ).toContain('Parental Guidance');
  });
});

describe('ShowDetailPanelComponent pricing', () => {
  it('renders the price chip', async () => {
    const fixture = await build(show1);
    expect(
      getNativeElement(fixture).querySelector('.chip-price')?.textContent,
    ).toContain('$10.00');
  });

  it('renders "Free" when price is zero', async () => {
    const fixture = await build(freeShow);
    expect(
      getNativeElement(fixture).querySelector('.chip-price')?.textContent,
    ).toContain('Free');
  });

  it('renders the description', async () => {
    const fixture = await build(show1);
    expect(
      getNativeElement(fixture).querySelector('.detail-desc')?.textContent,
    ).toContain('A gripping drama.');
  });

  it('builds a ticket link that includes the showId', async () => {
    const fixture = await build(show1),
      link =
        getNativeElement(fixture).querySelector<HTMLAnchorElement>('.book-btn');
    expect(link?.getAttribute('href')).toBe(
      `https://tickets.fringetheatre.ca/event/601:${FRINGE_URL_TEST_ID}`,
    );
  });
});

describe('ShowDetailPanelComponent dismiss', () => {
  it('emits dismiss when the close button is clicked', async () => {
    const fixture = await build(show1);
    let dismissed = false;
    fixture.componentInstance.dismiss.subscribe(() => {
      dismissed = true;
    });
    getNativeElement(fixture)
      .querySelector<HTMLButtonElement>('.close-btn')
      ?.click();
    expect(dismissed).toBeTrue();
  });

  it('emits dismiss when the backdrop is clicked', async () => {
    const fixture = await build(show1);
    let dismissed = false;
    fixture.componentInstance.dismiss.subscribe(() => {
      dismissed = true;
    });
    getNativeElement(fixture)
      .querySelector<HTMLElement>('.detail-backdrop')
      ?.click();
    expect(dismissed).toBeTrue();
  });
});
