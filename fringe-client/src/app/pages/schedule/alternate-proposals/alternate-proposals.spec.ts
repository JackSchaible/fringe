import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { ScheduleItem, Show } from '../../../models';
import { AlternateProposalsComponent } from './alternate-proposals';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

type ReadonlyProposal = Readonly<{
  description: string;
  excludedMemberName: string;
  items: ReadonlyArray<Readonly<ScheduleItem>>;
}>;

const FIRST_PROPOSAL = 0,
  ONE_CARD = 1,
  ONE_NEW_ITEM = 1,
  show1: Show = {
    fee: '$1',
    lengthInMinutes: 60,
    price: '$10',
    showId: 1,
    showTimes: ['2025-08-01T19:00:00Z'],
    title: 'Show One',
  },
  show2: Show = {
    fee: '$1',
    lengthInMinutes: 90,
    price: '$12',
    showId: 2,
    showTimes: ['2025-08-01T21:00:00Z'],
    title: 'Show Two',
  },
  item1: ScheduleItem = {
    groupScore: 10,
    show: show1,
    showTime: '2025-08-01T19:00:00Z',
  },
  item2: ScheduleItem = {
    groupScore: 8,
    show: show2,
    showTime: '2025-08-01T21:00:00Z',
  },
  proposal1: ReadonlyProposal = {
    description: 'Alt without Bob',
    excludedMemberName: 'Bob',
    items: [item1, item2],
  },
  build = async (
    proposals: ReadonlyArray<ReadonlyProposal>,
    schedule: ReadonlyArray<Readonly<ScheduleItem>>,
  ): Promise<ComponentFixture<AlternateProposalsComponent>> => {
    TestBed.configureTestingModule({
      imports: [AlternateProposalsComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(AlternateProposalsComponent);
    fixture.componentRef.setInput('proposals', proposals);
    fixture.componentRef.setInput('schedule', schedule);
    fixture.componentRef.setInput('subtitle', 'Some subtitle');
    fixture.detectChanges();
    return fixture;
  };

describe('AlternateProposalsComponent rendering', () => {
  it('renders nothing when there are no proposals', async () => {
    const fixture = await build([], [item1]);
    expect(
      getNativeElement(fixture).querySelector('.proposals-section'),
    ).toBeNull();
  });

  it('renders a card per proposal', async () => {
    const fixture = await build([proposal1], [item1]);
    expect(
      getNativeElement(fixture).querySelectorAll('.proposal-card').length,
    ).toBe(ONE_CARD);
  });

  it('expands a proposal when its header is clicked', async () => {
    const fixture = await build([proposal1], [item1]);
    getNativeElement(fixture)
      .querySelector<HTMLElement>('.proposal-header')
      ?.click();
    fixture.detectChanges();
    expect(
      getNativeElement(fixture).querySelector('.proposal-body'),
    ).not.toBeNull();
  });

  it('collapses an expanded proposal when clicked again', async () => {
    const fixture = await build([proposal1], [item1]);
    const header =
      getNativeElement(fixture).querySelector<HTMLElement>('.proposal-header');
    header?.click();
    fixture.detectChanges();
    header?.click();
    fixture.detectChanges();
    expect(
      getNativeElement(fixture).querySelector('.proposal-body'),
    ).toBeNull();
  });
});

describe('AlternateProposalsComponent expanded proposal', () => {
  it('shows shows blocked relative to the main schedule as needing a change', async () => {
    const fixture = await build([proposal1], [item1]);
    getNativeElement(fixture)
      .querySelector<HTMLElement>('.proposal-header')
      ?.click();
    fixture.detectChanges();
    expect(
      getNativeElement(fixture).querySelector('.needs-change'),
    ).not.toBeNull();
  });

  it('marks items not in the main schedule as new', async () => {
    const fixture = await build([proposal1], [item1]);
    getNativeElement(fixture)
      .querySelector<HTMLElement>('.proposal-header')
      ?.click();
    fixture.detectChanges();
    expect(
      getNativeElement(fixture).querySelectorAll('.proposal-item.is-new')
        .length,
    ).toBe(ONE_NEW_ITEM);
  });

  it('emits accept with the proposal index when accepted', async () => {
    const fixture = await build([proposal1], [item1]);
    getNativeElement(fixture)
      .querySelector<HTMLElement>('.proposal-header')
      ?.click();
    fixture.detectChanges();
    const emitted: Array<number> = [];
    fixture.componentInstance.accept.subscribe((index: number) => {
      emitted.push(index);
    });
    getNativeElement(fixture)
      .querySelector<HTMLButtonElement>('.btn-accept')
      ?.click();
    expect(emitted).toEqual([FIRST_PROPOSAL]);
  });
});
