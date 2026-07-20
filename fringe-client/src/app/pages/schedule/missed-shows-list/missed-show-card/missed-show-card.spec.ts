import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { Show, TransferConflict } from '../../../../models';
import { MissedShowCardComponent } from './missed-show-card';
import { getNativeElement } from '../../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const TWO_BLOCKERS = 2,
  FIRST = 0,
  SECOND = 1,
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
    availableGapMinutes: 10,
    destinationShowTitle: 'Sketch Bites',
    destinationVenueName: 'Northern Arts Collective',
    originShowTitle: 'Improv Chaos',
    originVenueName: 'Roxy Theatre',
    requiredGapMinutes: 32,
    travelMode: 'walking',
  },
  build = async (
    props: Readonly<{
      conflictsWithScheduled: boolean;
      blockedByMembers: ReadonlyArray<string>;
      transferConflict: Readonly<TransferConflict> | null;
    }>,
  ): Promise<ComponentFixture<MissedShowCardComponent>> => {
    TestBed.configureTestingModule({
      imports: [MissedShowCardComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(MissedShowCardComponent);
    fixture.componentRef.setInput('show', show1);
    fixture.componentRef.setInput(
      'conflictsWithScheduled',
      props.conflictsWithScheduled,
    );
    fixture.componentRef.setInput('blockedByMembers', props.blockedByMembers);
    fixture.componentRef.setInput('transferConflict', props.transferConflict);
    fixture.detectChanges();
    return fixture;
  };

describe('MissedShowCardComponent conflict/blocked reasons', () => {
  it('outlines the card for a timing conflict and explains it', async () => {
    const fixture = await build({
      blockedByMembers: [],
      conflictsWithScheduled: true,
      transferConflict: null,
    });
    const native = getNativeElement(fixture);
    expect(native.querySelector('.missed-card.has-reason')).not.toBeNull();
    expect(native.querySelector('.missed-line')?.textContent).toContain(
      'Overlaps with a show already in your schedule',
    );
  });

  it('outlines the card for an availability conflict and names each blocker', async () => {
    const fixture = await build({
      blockedByMembers: ['Bob', 'Alice'],
      conflictsWithScheduled: false,
      transferConflict: null,
    });
    const native = getNativeElement(fixture);
    expect(native.querySelector('.missed-card.has-reason')).not.toBeNull();
    const lines = native.querySelectorAll('.missed-line');
    expect(lines.length).toBe(TWO_BLOCKERS);
    expect(lines[FIRST].textContent).toContain('Bob');
    expect(lines[SECOND].textContent).toContain('Alice');
  });

  it('renders no outline class or detail block when there is no reason', async () => {
    const fixture = await build({
      blockedByMembers: [],
      conflictsWithScheduled: false,
      transferConflict: null,
    });
    const native = getNativeElement(fixture);
    expect(native.querySelector('.has-reason')).toBeNull();
    expect(native.querySelector('.missed-details')).toBeNull();
  });
});

describe('MissedShowCardComponent transfer reason', () => {
  it('outlines the card for a transfer conflict and reuses the detail component', async () => {
    const fixture = await build({
      blockedByMembers: [],
      conflictsWithScheduled: false,
      transferConflict: namedConflict,
    });
    const native = getNativeElement(fixture);
    expect(native.querySelector('.missed-card.has-reason')).not.toBeNull();
    expect(native.querySelector('.missed-detail')?.textContent).toContain(
      'Roxy Theatre',
    );
  });
});
