import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { ScheduleItem, Show } from '../../../models';
import { NeedsChangeComponent } from './needs-change';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

const MS_PER_MINUTE = 60_000,
  ONE_WINDOW = 1,
  show1: Show = {
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
    newShows: ReadonlyArray<Readonly<ScheduleItem>>,
  ): Promise<ComponentFixture<NeedsChangeComponent>> => {
    TestBed.configureTestingModule({
      imports: [NeedsChangeComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(NeedsChangeComponent);
    fixture.componentRef.setInput('newShows', newShows);
    fixture.componentRef.setInput('excludedMemberName', 'Bob');
    fixture.detectChanges();
    return fixture;
  };

describe('NeedsChangeComponent', () => {
  it('renders nothing when there are no new shows', async () => {
    const fixture = await build([]);
    expect(getNativeElement(fixture).querySelector('.needs-change')).toBeNull();
  });

  it('renders a window per new show', async () => {
    const fixture = await build([item1]);
    expect(
      getNativeElement(fixture).querySelectorAll('.needs-window').length,
    ).toBe(ONE_WINDOW);
  });

  it('renders the excluded member name in the heading', async () => {
    const fixture = await build([item1]);
    expect(
      getNativeElement(fixture).querySelector('.needs-change-heading')
        ?.textContent,
    ).toContain('Bob');
  });

  it('computes end time as showTime plus lengthInMinutes', async () => {
    const fixture = await build([item1]),
      expected = new Date(
        new Date('2025-08-01T19:00:00Z').getTime() +
          show1.lengthInMinutes * MS_PER_MINUTE,
      );
    expect(fixture.componentInstance.showEndTime(item1).getTime()).toBe(
      expected.getTime(),
    );
  });
});
