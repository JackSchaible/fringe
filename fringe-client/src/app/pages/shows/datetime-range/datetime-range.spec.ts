import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { DatetimeRangeComponent } from './datetime-range';
import { getNativeElement } from '../../../../testing/native-element';
import { provideZonelessChangeDetection } from '@angular/core';

// Moments are derived from local Date components, not hardcoded UTC
// ISO literals, so they stay correct under any test-runner timezone.
const YEAR = 2026,
  MONTH_AUGUST = 8,
  JS_MONTH_OFFSET = 1,
  DAY_RANGE_START = 1,
  DAY_NARROWED_START = 3,
  DAY_NARROWED_END = 7,
  DAY_RANGE_END = 10,
  HOUR_START = 14,
  HOUR_END = 22,
  MINUTE_ZERO = 0,
  PAD_WIDTH = 2;

interface Moment {
  readonly iso: string;
  readonly inputValue: string;
}

const pad = (value: number): string => String(value).padStart(PAD_WIDTH, '0'),
  localMoment = (day: number, hour: number): Moment => ({
    inputValue: `${YEAR}-${pad(MONTH_AUGUST)}-${pad(day)}T${pad(hour)}:${pad(MINUTE_ZERO)}`,
    iso: new Date(
      YEAR,
      MONTH_AUGUST - JS_MONTH_OFFSET,
      day,
      hour,
      MINUTE_ZERO,
    ).toISOString(),
  });

const rangeStart = localMoment(DAY_RANGE_START, HOUR_START),
  rangeEnd = localMoment(DAY_RANGE_END, HOUR_END),
  narrowedStart = localMoment(DAY_NARROWED_START, HOUR_START),
  narrowedEnd = localMoment(DAY_NARROWED_END, HOUR_END);

const build = async (
    value: readonly [string, string] = [rangeStart.iso, rangeEnd.iso],
  ): Promise<ComponentFixture<DatetimeRangeComponent>> => {
    TestBed.configureTestingModule({
      imports: [DatetimeRangeComponent],
      providers: [provideZonelessChangeDetection()],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(DatetimeRangeComponent);
    fixture.componentRef.setInput('min', rangeStart.iso);
    fixture.componentRef.setInput('max', rangeEnd.iso);
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
    return fixture;
  },
  inputsOf = (
    fixture: ComponentFixture<DatetimeRangeComponent>,
  ): readonly [HTMLInputElement, HTMLInputElement] => {
    const [startInput, endInput] = getNativeElement(
      fixture,
    ).querySelectorAll<HTMLInputElement>('.datetime-range-input');
    return [startInput, endInput];
  },
  captureEmitted = (
    fixture: ComponentFixture<DatetimeRangeComponent>,
  ): Array<readonly [string, string]> => {
    const emitted: Array<readonly [string, string]> = [];
    fixture.componentInstance.valueChange.subscribe((value) => {
      emitted.push(value);
    });
    return emitted;
  };

describe('DatetimeRangeComponent rendering', () => {
  it('applies the label when provided', async () => {
    const fixture = await build();
    fixture.componentRef.setInput('label', 'Showtime');
    fixture.detectChanges();
    expect(
      getNativeElement(fixture).querySelector('.datetime-range-label')
        ?.textContent,
    ).toContain('Showtime');
  });

  it('omits the label element when none is provided', async () => {
    const fixture = await build();
    expect(
      getNativeElement(fixture).querySelector('.datetime-range-label'),
    ).toBeNull();
  });

  it('renders the current start and end values as local datetime inputs', async () => {
    const fixture = await build(),
      [startInput, endInput] = inputsOf(fixture);
    expect(startInput.value).toBe(rangeStart.inputValue);
    expect(endInput.value).toBe(rangeEnd.inputValue);
  });
});

describe('DatetimeRangeComponent start input events', () => {
  it('emits an updated start time clamped below the current end', async () => {
    const fixture = await build([rangeStart.iso, narrowedEnd.iso]),
      emitted = captureEmitted(fixture),
      [startInput] = inputsOf(fixture);
    startInput.value = narrowedStart.inputValue;
    startInput.dispatchEvent(new Event('input'));
    expect(emitted).toEqual([[narrowedStart.iso, narrowedEnd.iso]]);
  });

  it('clamps a start-time change so it cannot exceed the current end', async () => {
    const fixture = await build([rangeStart.iso, narrowedStart.iso]),
      emitted = captureEmitted(fixture),
      [startInput] = inputsOf(fixture);
    startInput.value = narrowedEnd.inputValue;
    startInput.dispatchEvent(new Event('input'));
    expect(emitted).toEqual([[narrowedStart.iso, narrowedStart.iso]]);
  });

  it('ignores an incomplete (cleared) datetime-local value', async () => {
    const fixture = await build(),
      emitted = captureEmitted(fixture),
      [startInput] = inputsOf(fixture);
    startInput.value = '';
    startInput.dispatchEvent(new Event('input'));
    expect(emitted).toEqual([]);
  });
});

describe('DatetimeRangeComponent end input events', () => {
  it('emits an updated end time clamped above the current start', async () => {
    const fixture = await build([narrowedStart.iso, rangeEnd.iso]),
      emitted = captureEmitted(fixture),
      [, endInput] = inputsOf(fixture);
    endInput.value = narrowedEnd.inputValue;
    endInput.dispatchEvent(new Event('input'));
    expect(emitted).toEqual([[narrowedStart.iso, narrowedEnd.iso]]);
  });

  it('clamps an end-time change so it cannot go below the current start', async () => {
    const fixture = await build([narrowedEnd.iso, rangeEnd.iso]),
      emitted = captureEmitted(fixture),
      [, endInput] = inputsOf(fixture);
    endInput.value = narrowedStart.inputValue;
    endInput.dispatchEvent(new Event('input'));
    expect(emitted).toEqual([[narrowedEnd.iso, narrowedEnd.iso]]);
  });

  it('ignores an incomplete (cleared) datetime-local value', async () => {
    const fixture = await build(),
      emitted = captureEmitted(fixture),
      [, endInput] = inputsOf(fixture);
    endInput.value = '';
    endInput.dispatchEvent(new Event('input'));
    expect(emitted).toEqual([]);
  });
});
