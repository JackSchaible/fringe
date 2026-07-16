import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { Show } from '../../../models';
import { ShowDetailStatsComponent } from './show-detail-stats';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const SECOND_STAT_INDEX = 1,
  showWithVenue: Show = {
    fee: '$1',
    lengthInMinutes: 60,
    price: '$10',
    showId: 1,
    showTimes: [],
    title: 'Hamlet',
    venue: { address: '1 Main St', name: 'Venue A', phone: '555-0100' },
  },
  showWithoutVenue: Show = {
    fee: '$1',
    lengthInMinutes: 0,
    price: '$10',
    showId: 2,
    showTimes: [],
    title: 'Comedy Night',
  },
  build = async (
    show: Readonly<Show>,
  ): Promise<ComponentFixture<ShowDetailStatsComponent>> => {
    TestBed.configureTestingModule({
      imports: [ShowDetailStatsComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(ShowDetailStatsComponent);
    fixture.componentRef.setInput('show', show);
    fixture.detectChanges();
    return fixture;
  };

describe('ShowDetailStatsComponent', () => {
  it('renders the show length when present', async () => {
    const fixture = await build(showWithVenue);
    expect(
      getNativeElement(fixture).querySelector('.stat')?.textContent,
    ).toContain('60 min');
  });

  it('omits the length stat when lengthInMinutes is zero', async () => {
    const fixture = await build(showWithoutVenue);
    expect(getNativeElement(fixture).textContent).not.toContain('min');
  });

  it('renders the venue name when present', async () => {
    const fixture = await build(showWithVenue),
      stats = getNativeElement(fixture).querySelectorAll('.stat');
    expect(stats[SECOND_STAT_INDEX].textContent).toContain('Venue A');
  });

  it('renders the venue address when present', async () => {
    const fixture = await build(showWithVenue);
    expect(
      getNativeElement(fixture).querySelector('.detail-address')?.textContent,
    ).toContain('1 Main St');
  });

  it('omits venue stats when venue is absent', async () => {
    const fixture = await build(showWithoutVenue);
    expect(
      getNativeElement(fixture).querySelector('.detail-address'),
    ).toBeNull();
  });
});
