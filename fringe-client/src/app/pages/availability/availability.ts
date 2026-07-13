import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { faPlus, faTrash, faCheck } from '@fortawesome/pro-regular-svg-icons';
import { faCalendarClock } from '@fortawesome/pro-light-svg-icons';
import { ApiService } from '../../services/api.service';
import { Show } from '../../models';

interface TimeWindow {
  start: string; // 'HH:MM' local time
  end: string;   // 'HH:MM' local time
}

interface DayAvailability {
  date: string;        // 'YYYY-MM-DD' local date
  label: string;       // 'Fri Aug 15'
  windows: TimeWindow[];
}

@Component({
  selector: 'fg-availability',
  imports: [FormsModule, FaIconComponent],
  templateUrl: './availability.html',
  styleUrl: './availability.scss',
})
export class AvailabilityPage implements OnInit {
  private readonly api = inject(ApiService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly days = signal<DayAvailability[]>([]);

  protected readonly faPlus = faPlus;
  protected readonly faTrash = faTrash;
  protected readonly faCheck = faCheck;
  protected readonly faCalendarClock = faCalendarClock;

  ngOnInit(): void {
    Promise.all([
      this.api.getShows().toPromise(),
      this.api.getAvailability().toPromise(),
    ]).then(([shows, availability]) => {
      const festivalDays = this.deriveFestivalDays(shows ?? []);
      const existingWindowsByDate = this.parseExistingWindows(availability?.windows ?? []);

      this.days.set(festivalDays.map(date => ({
        date,
        label: this.formatDayLabel(date),
        windows: existingWindowsByDate[date] ?? [],
      })));
      this.loading.set(false);
    });
  }

  addWindow(day: DayAvailability): void {
    day.windows = [...day.windows, { start: '10:00', end: '23:00' }];
    this.days.update(d => [...d]);
  }

  removeWindow(day: DayAvailability, index: number): void {
    day.windows = day.windows.filter((_, i) => i !== index);
    this.days.update(d => [...d]);
  }

  updateWindow(day: DayAvailability, index: number, field: 'start' | 'end', value: string): void {
    day.windows[index] = { ...day.windows[index], [field]: value };
    this.days.update(d => [...d]);
  }

  save(): void {
    this.saving.set(true);
    this.saved.set(false);

    const windows = this.days()
      .flatMap(day =>
        day.windows
          .filter(w => w.start && w.end && w.start < w.end)
          .map(w => ({
            start: this.localToUtc(day.date, w.start),
            end: this.localToUtc(day.date, w.end),
          }))
      );

    this.api.saveAvailability({ windows }).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 3000);
      },
      error: () => this.saving.set(false),
    });
  }

  clearAll(): void {
    this.days.update(days => days.map(d => ({ ...d, windows: [] })));
  }

  private deriveFestivalDays(shows: Show[]): string[] {
    const dates = new Set<string>();
    for (const show of shows) {
      for (const st of show.showTimes) {
        dates.add(this.utcToLocalDate(st));
      }
    }
    return [...dates].sort();
  }

  private parseExistingWindows(windows: { start: string; end: string }[]): Record<string, TimeWindow[]> {
    const byDate: Record<string, TimeWindow[]> = {};
    for (const w of windows) {
      const date = this.utcToLocalDate(w.start);
      byDate[date] ??= [];
      byDate[date].push({
        start: this.utcToLocalTime(w.start),
        end: this.utcToLocalTime(w.end),
      });
    }
    return byDate;
  }

  private localToUtc(date: string, time: string): string {
    return new Date(`${date}T${time}:00`).toISOString();
  }

  private utcToLocalDate(utcIso: string): string {
    const d = new Date(utcIso);
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  private utcToLocalTime(utcIso: string): string {
    const d = new Date(utcIso);
    return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
  }

  private formatDayLabel(date: string): string {
    return new Date(`${date}T12:00:00`).toLocaleDateString('en-CA', {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    });
  }
}
