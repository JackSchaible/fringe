import type { CalendarCell, CalendarMonth } from '../calendar-utils';
import { Component, input, output } from '@angular/core';

@Component({
  imports: [],
  selector: 'fg-calendar-month',
  styleUrl: './calendar-month.scss',
  templateUrl: './calendar-month.html',
})
export class CalendarMonthComponent {
  public readonly month = input.required<Readonly<CalendarMonth>>();
  public readonly dayNames = input.required<ReadonlyArray<string>>();
  public readonly selectedDate = input.required<string | null>();
  public readonly daySelect = output<Readonly<CalendarCell>>();
}
