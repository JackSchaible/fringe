import { Component, computed, input } from '@angular/core';
import { DatePipe, NgOptimizedImage } from '@angular/common';
import { faClock, faLocationDot } from '@fortawesome/pro-light-svg-icons';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import type { ScheduleItem } from '../../../models';
import { faArrowUpRightFromSquare } from '@fortawesome/pro-solid-svg-icons';
import { venueDisplayName } from '../../../venue-display';

const fringeUrl = (showId: number): string =>
  `https://tickets.fringetheatre.ca/event/601:${showId}`;

@Component({
  imports: [DatePipe, FaIconComponent, NgOptimizedImage],
  selector: 'fg-schedule-item-row',
  styleUrl: './schedule-item-row.scss',
  templateUrl: './schedule-item-row.html',
})
export class ScheduleItemRowComponent {
  public readonly item = input.required<Readonly<ScheduleItem>>();

  public readonly fringeUrl = fringeUrl;

  protected readonly faClock = faClock;
  protected readonly faLocationDot = faLocationDot;
  protected readonly faArrowUpRightFromSquare = faArrowUpRightFromSquare;

  protected readonly venueName = computed<string>(() =>
    venueDisplayName(this.item().show.venue),
  );
}
