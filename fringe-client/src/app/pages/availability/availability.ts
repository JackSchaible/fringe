import {
  type AvailabilityWindow,
  type CalendarCell,
  type CalendarMonth,
  buildCalendarMonths,
  buildFestivalDays,
  utcToLocalDate,
} from './calendar-utils';
import type {
  CalendarOptions,
  DateSelectArg,
  EventClickArg,
} from '@fullcalendar/core';
import {
  Component,
  type OnInit,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import {
  type FullCalendarComponent,
  FullCalendarModule,
} from '@fullcalendar/angular';
import { ApiService } from '../../services/api.service';
import { CalendarMonthComponent } from './calendar-month/calendar-month';
import type { EventImpl } from '@fullcalendar/core/internal';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { faCheck } from '../../vendor/fontawesome-icons/solid';
import { firstValueFrom } from 'rxjs';
import interactionPlugin from '@fullcalendar/interaction';
import timeGridPlugin from '@fullcalendar/timegrid';

const CALENDAR_HEIGHT_PX = 380,
  DAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
  EMPTY_COUNT = 0,
  SAVED_FEEDBACK_MS = 3_000;

@Component({
  imports: [FaIconComponent, FullCalendarModule, CalendarMonthComponent],
  selector: 'fg-availability',
  styleUrl: './availability.scss',
  templateUrl: './availability.html',
})
export class AvailabilityPage implements OnInit {
  public readonly loading = signal(true);
  public readonly saving = signal(false);
  public readonly saved = signal(false);
  public readonly calendarMonths = signal<ReadonlyArray<CalendarMonth>>([]);
  public readonly selectedDate = signal<string | null>(null);
  public readonly selectedDayLabel = signal<string>('');

  public calendarOptions: CalendarOptions = {
    allDaySlot: false,
    eventBorderColor: 'transparent',
    eventClick: (info: EventClickArg) => {
      info.event.remove();
      this.syncWindowsFromCalendar();
    },
    eventColor: '#b5563a',
    eventTextColor: '#f5e1d6',
    events: [],
    expandRows: false,
    headerToolbar: false,
    height: CALENDAR_HEIGHT_PX,
    initialView: 'timeGridDay',
    plugins: [timeGridPlugin, interactionPlugin],
    select: (info: DateSelectArg) => {
      const api = this.calendarRef().getApi();
      api.addEvent({ end: info.endStr, start: info.startStr });
      api.unselect();
      this.syncWindowsFromCalendar();
    },
    selectLongPressDelay: 0,
    selectMirror: true,
    selectable: true,
    slotDuration: '00:30:00',
    slotLabelFormat: {
      hour: 'numeric',
      meridiem: 'short',
      omitZeroMinute: true,
    },
    slotLabelInterval: '01:00:00',
    slotMaxTime: '24:00:00',
    slotMinTime: '10:00:00',
    unselectAuto: false,
  };

  protected readonly faCheck = faCheck;
  protected readonly dayNames = DAY_NAMES;

  private readonly calendarRef =
    viewChild.required<FullCalendarComponent>('calendar');
  private readonly api = inject(ApiService);

  private windowsByDate: Partial<Record<string, Array<AvailabilityWindow>>> =
    {};

  public ngOnInit(): void {
    void this.loadAvailability();
  }

  public selectDay(cell: Readonly<CalendarCell>): void {
    if (
      !cell.isFestival ||
      cell.festivalDay === null ||
      cell.date === null ||
      cell.date.trim().length === EMPTY_COUNT
    ) {
      return;
    }
    const day = cell.festivalDay,
      selectedDate = cell.date;
    this.selectedDate.set(selectedDate);
    this.selectedDayLabel.set(
      new Date(`${selectedDate}T12:00:00`).toLocaleDateString('en-CA', {
        day: 'numeric',
        month: 'long',
        weekday: 'long',
      }),
    );

    // Rebuild options by reference so Angular's [options] binding triggers FullCalendar re-render
    this.calendarOptions = {
      ...this.calendarOptions,
      slotMaxTime: day.latest,
      slotMinTime: day.earliest,
    };

    // GotoDate + load saved windows after the new options have rendered
    setTimeout(() => {
      const api = this.calendarRef().getApi();
      api.gotoDate(selectedDate);
      api.removeAllEvents();
      for (const window of this.windowsByDate[selectedDate] ?? []) {
        api.addEvent({ end: window.end, start: window.start });
      }
    });
  }

  public save(): void {
    this.syncWindowsFromCalendar();
    this.saving.set(true);
    this.saved.set(false);
    const windows = Object.values(this.windowsByDate).flatMap(
      (entry) => entry ?? [],
    );
    this.api.saveAvailability({ windows }).subscribe({
      error: () => {
        this.saving.set(false);
      },
      next: () => {
        this.saving.set(false);
        this.saved.set(true);
        this.rebuildHighlights();
        setTimeout(() => {
          this.saved.set(false);
        }, SAVED_FEEDBACK_MS);
      },
    });
  }

  public clearDay(): void {
    const date = this.selectedDate();
    if (date === null) {
      return;
    }
    this.calendarRef().getApi().removeAllEvents();
    Reflect.deleteProperty(this.windowsByDate, date);
    this.rebuildHighlights();
  }

  private async loadAvailability(): Promise<void> {
    const [shows, availability] = await Promise.all([
      firstValueFrom(this.api.getShows()),
      firstValueFrom(this.api.getAvailability()),
    ]);
    for (const win of availability.windows) {
      const date = utcToLocalDate(win.start);
      this.windowsByDate[date] ??= [];
      this.windowsByDate[date].push({ end: win.end, start: win.start });
    }

    const days = buildFestivalDays(shows, this.windowsByDate);
    this.calendarMonths.set(buildCalendarMonths(days));
    this.loading.set(false);
  }

  private syncWindowsFromCalendar(): void {
    const date = this.selectedDate(),
      events = this.calendarRef().getApi().getEvents();
    if (date === null) {
      return;
    }

    this.windowsByDate[date] = events.map((eventType: EventImpl) => ({
      end: eventType.endStr,
      start: eventType.startStr,
    }));
    this.rebuildHighlights();
  }

  private rebuildHighlights(): void {
    this.calendarMonths.update((months) =>
      months.map((month) => ({
        ...month,
        weeks: month.weeks.map((week) =>
          week.map((cell) => this.withHighlight(cell)),
        ),
      })),
    );
  }

  private withHighlight(cell: Readonly<CalendarCell>): CalendarCell {
    if (!cell.isFestival || cell.date === null || cell.festivalDay === null) {
      return cell;
    }
    return {
      ...cell,
      festivalDay: {
        ...cell.festivalDay,
        hasWindows:
          (this.windowsByDate[cell.date]?.length ?? EMPTY_COUNT) > EMPTY_COUNT,
      },
    };
  }
}
