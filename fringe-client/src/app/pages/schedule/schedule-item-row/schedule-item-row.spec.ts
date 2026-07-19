import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { ScheduleItemRowComponent } from './schedule-item-row';
import type { Show } from '../../../models';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const FRINGE_URL_TEST_ID = 42,
  show1: Show = {
    fee: '$1',
    lengthInMinutes: 60,
    price: '$10',
    showId: FRINGE_URL_TEST_ID,
    showTimes: ['2025-08-01T19:00:00Z'],
    title: 'Show One',
  },
  build = async (
    show: Readonly<Show> = show1,
  ): Promise<ComponentFixture<ScheduleItemRowComponent>> => {
    TestBed.configureTestingModule({
      imports: [ScheduleItemRowComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(ScheduleItemRowComponent);
    fixture.componentRef.setInput('item', {
      groupScore: 10,
      show,
      showTime: '2025-08-01T19:00:00Z',
    });
    fixture.detectChanges();
    return fixture;
  };

describe('ScheduleItemRowComponent', () => {
  it('renders the show title', async () => {
    const fixture = await build();
    expect(
      getNativeElement(fixture).querySelector('h2')?.textContent,
    ).toContain('Show One');
  });

  it('builds a ticket link that includes the showId', async () => {
    const fixture = await build();
    const link =
      getNativeElement(fixture).querySelector<HTMLAnchorElement>(
        '.ticket-link',
      );
    expect(link?.getAttribute('href')).toBe(
      `https://tickets.fringetheatre.ca/event/601:${FRINGE_URL_TEST_ID}`,
    );
  });

  it('renders the group score', async () => {
    const fixture = await build();
    expect(
      getNativeElement(fixture).querySelector('.score')?.textContent,
    ).toContain('10');
  });

  it('falls back to the address as the venue name when the name is "Unknown"', async () => {
    const showWithUnknownVenueName: Show = {
        ...show1,
        venue: { address: '10330 84 Ave NW', name: 'Unknown', phone: '' },
      },
      fixture = await build(showWithUnknownVenueName);
    expect(
      getNativeElement(fixture).querySelector('.venue')?.textContent,
    ).toContain('10330 84 Ave NW');
  });

  it('omits the venue line when there is no usable name or address', async () => {
    const showWithNoUsableVenue: Show = {
        ...show1,
        venue: { address: '', name: 'Unknown', phone: '' },
      },
      fixture = await build(showWithNoUsableVenue);
    expect(getNativeElement(fixture).querySelector('.venue')).toBeNull();
  });
});
