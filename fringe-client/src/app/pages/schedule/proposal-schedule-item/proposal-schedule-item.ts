import { Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import type { ScheduleItem } from '../../../models';

@Component({
  imports: [DatePipe],
  selector: 'fg-proposal-schedule-item',
  styleUrl: './proposal-schedule-item.scss',
  templateUrl: './proposal-schedule-item.html',
})
export class ProposalScheduleItemComponent {
  public readonly item = input.required<Readonly<ScheduleItem>>();
  public readonly isNew = input.required<boolean>();
}
