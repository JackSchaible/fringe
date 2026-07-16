import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { ScheduleItem, Show } from '../../../models';
import { ProposalScheduleItemComponent } from './proposal-schedule-item';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const show1: Show = {
    fee: '$1',
    lengthInMinutes: 60,
    price: '$10',
    showId: 1,
    showTimes: ['2025-08-01T19:00:00Z'],
    title: 'Show One',
  },
  item1: ScheduleItem = {
    groupScore: 10,
    show: show1,
    showTime: '2025-08-01T19:00:00Z',
  },
  build = async (
    isNew: boolean,
  ): Promise<ComponentFixture<ProposalScheduleItemComponent>> => {
    TestBed.configureTestingModule({
      imports: [ProposalScheduleItemComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(ProposalScheduleItemComponent);
    fixture.componentRef.setInput('item', item1);
    fixture.componentRef.setInput('isNew', isNew);
    fixture.detectChanges();
    return fixture;
  };

describe('ProposalScheduleItemComponent', () => {
  it('renders the show title', async () => {
    const fixture = await build(false);
    expect(
      getNativeElement(fixture).querySelector('.proposal-title')?.textContent,
    ).toContain('Show One');
  });

  it('shows a new badge when isNew is true', async () => {
    const fixture = await build(true);
    expect(
      getNativeElement(fixture).querySelector('.new-badge'),
    ).not.toBeNull();
  });

  it('hides the new badge when isNew is false', async () => {
    const fixture = await build(false);
    expect(getNativeElement(fixture).querySelector('.new-badge')).toBeNull();
  });

  it('applies the is-new class when isNew is true', async () => {
    const fixture = await build(true);
    expect(
      getNativeElement(fixture)
        .querySelector('.proposal-item')
        ?.classList.contains('is-new'),
    ).toBeTrue();
  });
});
