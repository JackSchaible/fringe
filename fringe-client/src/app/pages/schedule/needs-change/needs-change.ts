import { Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import type { ScheduleItem } from '../../../models';
import { faCircleInfo } from '../../../vendor/fontawesome-icons/light';

const MS_PER_MINUTE = 60_000,
  showEndTime = (item: Readonly<ScheduleItem>): Date =>
    new Date(
      new Date(item.showTime).getTime() +
        item.show.lengthInMinutes * MS_PER_MINUTE,
    );

@Component({
  imports: [DatePipe, FaIconComponent],
  selector: 'fg-needs-change',
  styleUrl: './needs-change.scss',
  templateUrl: './needs-change.html',
})
export class NeedsChangeComponent {
  public readonly newShows =
    input.required<ReadonlyArray<Readonly<ScheduleItem>>>();
  public readonly excludedMemberName = input.required<string>();

  public readonly showEndTime = showEndTime;

  protected readonly faCircleInfo = faCircleInfo;
}
