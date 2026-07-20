import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { Show, TransferConflict } from '../../../models';
import { MissedShowsListComponent } from './missed-shows-list';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const ONE_ITEM = 1,
  ONE_TAG = 1,
  show1: Show = {
    fee: '$1',
    lengthInMinutes: 60,
    price: '$10',
    showId: 1,
    showTimes: ['2025-08-01T19:00:00Z'],
    title: 'Show One',
  },
  namedConflict: TransferConflict = {
    appliedRule: 'matrix',
    availableGapMinutes: 15,
    destinationVenueName: 'Venue Twenty',
    originVenueName: 'Venue Ten',
    requiredGapMinutes: 45,
    travelMode: 'walking',
  },
  build = async (
    missedShows: ReadonlyArray<
      Readonly<{
        show: Show;
        conflictsWithScheduled: boolean;
        blockedByMembers: ReadonlyArray<string>;
        transferConflict: Readonly<TransferConflict> | null;
      }>
    >,
  ): Promise<ComponentFixture<MissedShowsListComponent>> => {
    TestBed.configureTestingModule({
      imports: [MissedShowsListComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(MissedShowsListComponent);
    fixture.componentRef.setInput('missedShows', missedShows);
    fixture.detectChanges();
    return fixture;
  };

describe('MissedShowsListComponent', () => {
  it('renders nothing when there are no missed shows', async () => {
    const fixture = await build([]);
    expect(
      getNativeElement(fixture).querySelector('.missed-section'),
    ).toBeNull();
  });

  it('renders an item per missed show', async () => {
    const fixture = await build([
      {
        blockedByMembers: [],
        conflictsWithScheduled: false,
        show: show1,
        transferConflict: null,
      },
    ]);
    expect(
      getNativeElement(fixture).querySelectorAll('.missed-item').length,
    ).toBe(ONE_ITEM);
  });

  it('shows a conflict tag when conflictsWithScheduled is true', async () => {
    const fixture = await build([
      {
        blockedByMembers: [],
        conflictsWithScheduled: true,
        show: show1,
        transferConflict: null,
      },
    ]);
    expect(
      getNativeElement(fixture).querySelector('.missed-tag.conflict'),
    ).not.toBeNull();
  });

  it('shows a blocked tag per blocking member', async () => {
    const fixture = await build([
      {
        blockedByMembers: ['Bob'],
        conflictsWithScheduled: false,
        show: show1,
        transferConflict: null,
      },
    ]);
    expect(
      getNativeElement(fixture).querySelectorAll('.missed-tag.blocked').length,
    ).toBe(ONE_TAG);
  });
});

describe('MissedShowsListComponent transfer conflict tag', () => {
  it('does not render a transfer tag when transferConflict is null', async () => {
    const fixture = await build([
      {
        blockedByMembers: [],
        conflictsWithScheduled: false,
        show: show1,
        transferConflict: null,
      },
    ]);
    expect(
      getNativeElement(fixture).querySelector('.missed-tag.transfer'),
    ).toBeNull();
  });

  it('names both venues when both are known', async () => {
    const fixture = await build([
      {
        blockedByMembers: [],
        conflictsWithScheduled: false,
        show: show1,
        transferConflict: namedConflict,
      },
    ]);
    const tag = getNativeElement(fixture).querySelector('.missed-tag.transfer');
    expect(tag?.textContent).toContain('Venue Ten');
    expect(tag?.textContent).toContain('Venue Twenty');
  });

  it('falls back to a generic message when a venue name is unknown', async () => {
    const fixture = await build([
      {
        blockedByMembers: [],
        conflictsWithScheduled: false,
        show: show1,
        transferConflict: { ...namedConflict, originVenueName: null },
      },
    ]);
    const tag = getNativeElement(fixture).querySelector('.missed-tag.transfer');
    expect(tag?.textContent).toContain('next venue');
  });
});
