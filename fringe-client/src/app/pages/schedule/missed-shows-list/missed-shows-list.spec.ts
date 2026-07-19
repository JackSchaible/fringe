import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { MissedShowsListComponent } from './missed-shows-list';
import type { Show } from '../../../models';
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
  build = async (
    missedShows: ReadonlyArray<
      Readonly<{
        show: Show;
        conflictsWithScheduled: boolean;
        blockedByMembers: ReadonlyArray<string>;
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
      { blockedByMembers: [], conflictsWithScheduled: false, show: show1 },
    ]);
    expect(
      getNativeElement(fixture).querySelectorAll('.missed-item').length,
    ).toBe(ONE_ITEM);
  });

  it('shows a conflict tag when conflictsWithScheduled is true', async () => {
    const fixture = await build([
      { blockedByMembers: [], conflictsWithScheduled: true, show: show1 },
    ]);
    expect(
      getNativeElement(fixture).querySelector('.missed-tag.conflict'),
    ).not.toBeNull();
  });

  it('shows a blocked tag per blocking member', async () => {
    const fixture = await build([
      { blockedByMembers: ['Bob'], conflictsWithScheduled: false, show: show1 },
    ]);
    expect(
      getNativeElement(fixture).querySelectorAll('.missed-tag.blocked').length,
    ).toBe(ONE_TAG);
  });
});
