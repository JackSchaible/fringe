import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { faClock, faLocationDot, faUserGroup, faCalendarXmark } from '@fortawesome/pro-light-svg-icons';
import { ApiService } from '../../services/api.service';
import { ScheduleItem } from '../../models';

@Component({
  selector: 'fg-schedule',
  imports: [DatePipe, RouterLink, FaIconComponent],
  templateUrl: './schedule.html',
  styleUrl: './schedule.scss',
})
export class SchedulePage implements OnInit {
  private readonly api = inject(ApiService);

  readonly loading = signal(true);
  readonly noGroup = signal(false);
  readonly schedule = signal<ScheduleItem[]>([]);

  protected readonly faClock = faClock;
  protected readonly faLocationDot = faLocationDot;
  protected readonly faUserGroup = faUserGroup;
  protected readonly faCalendarXmark = faCalendarXmark;

  ngOnInit(): void {
    this.api.getSchedule().subscribe({
      next: items => {
        this.schedule.set(items);
        this.loading.set(false);
      },
      error: (err) => {
        if (err.status === 400) this.noGroup.set(true);
        this.loading.set(false);
      },
    });
  }
}
