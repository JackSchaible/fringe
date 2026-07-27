import { Component, input, output } from '@angular/core';

const DATETIME_LOCAL_LENGTH = 16,
  JS_MONTH_OFFSET = 1,
  PAD_WIDTH = 2;

const pad = (value: number): string => String(value).padStart(PAD_WIDTH, '0');

const toLocalInputValue = (iso: string): string => {
  const date = new Date(iso);
  return `${date.getFullYear()}-${pad(date.getMonth() + JS_MONTH_OFFSET)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

const earlierIso = (first: string, second: string): string => {
  if (first < second) {
    return first;
  }
  return second;
};

const laterIso = (first: string, second: string): string => {
  if (first > second) {
    return first;
  }
  return second;
};

@Component({
  imports: [],
  selector: 'fg-datetime-range',
  styleUrl: './datetime-range.scss',
  templateUrl: './datetime-range.html',
})
export class DatetimeRangeComponent {
  public readonly label = input('');
  public readonly min = input.required<string>();
  public readonly max = input.required<string>();
  public readonly value = input.required<readonly [string, string]>();

  public readonly valueChange = output<readonly [string, string]>();

  protected readonly toLocalInputValue = toLocalInputValue;

  protected onMinInput(rawValue: string): void {
    if (rawValue.length < DATETIME_LOCAL_LENGTH) {
      return;
    }
    const [, currentMax] = this.value(),
      candidate = new Date(rawValue).toISOString(),
      nextMin = earlierIso(candidate, currentMax);
    this.valueChange.emit([nextMin, currentMax]);
  }

  protected onMaxInput(rawValue: string): void {
    if (rawValue.length < DATETIME_LOCAL_LENGTH) {
      return;
    }
    const [currentMin] = this.value(),
      candidate = new Date(rawValue).toISOString(),
      nextMax = laterIso(candidate, currentMin);
    this.valueChange.emit([currentMin, nextMax]);
  }
}
