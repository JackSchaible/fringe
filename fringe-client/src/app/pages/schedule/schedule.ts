import type { AlternateProposal, MissedShow, ScheduleItem } from '../../models';
import { Component, type OnInit, inject, signal } from '@angular/core';
import {
  faCalendarClock,
  faCalendarXmark,
  faUserGroup,
} from '@fortawesome/pro-light-svg-icons';
import { AlternateProposalsComponent } from './alternate-proposals/alternate-proposals';
import { ApiService } from '../../services/api.service';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import type { HttpErrorResponse } from '@angular/common/http';
import { MissedShowsListComponent } from './missed-shows-list/missed-shows-list';
import { RouterLink } from '@angular/router';
import { ScheduleItemRowComponent } from './schedule-item-row/schedule-item-row';
import { faCheck } from '@fortawesome/pro-solid-svg-icons';

const HTTP_BAD_REQUEST = 400;

@Component({
  imports: [
    RouterLink,
    FaIconComponent,
    ScheduleItemRowComponent,
    MissedShowsListComponent,
    AlternateProposalsComponent,
  ],
  selector: 'fg-schedule',
  styleUrl: './schedule.scss',
  templateUrl: './schedule.html',
})
export class SchedulePage implements OnInit {
  public readonly loading = signal(true);
  public readonly noGroup = signal(false);
  public readonly hasVotes = signal(false);
  public readonly schedule = signal<Array<ScheduleItem>>([]);
  public readonly proposals = signal<Array<AlternateProposal>>([]);
  public readonly missedShows = signal<Array<MissedShow>>([]);
  public readonly activeProposalIndex = signal<number | null>(null);

  protected readonly faUserGroup = faUserGroup;
  protected readonly faCalendarXmark = faCalendarXmark;
  protected readonly faCalendarClock = faCalendarClock;
  protected readonly faCheck = faCheck;

  private readonly api = inject(ApiService);

  public activeSchedule(): Array<ScheduleItem> {
    const idx = this.activeProposalIndex();
    if (idx !== null) {
      return this.proposals()[idx]?.items ?? this.schedule();
    }
    return this.schedule();
  }

  public acceptProposal(index: number): void {
    this.activeProposalIndex.set(index);
  }

  public resetToMain(): void {
    this.activeProposalIndex.set(null);
  }

  public ngOnInit(): void {
    this.api.getSchedule().subscribe({
      error: (err: HttpErrorResponse) => {
        if (err.status === HTTP_BAD_REQUEST) {
          this.noGroup.set(true);
        }
        this.loading.set(false);
      },
      next: (response) => {
        this.hasVotes.set(response.hasVotes);
        this.schedule.set(response.items);
        this.proposals.set(response.alternateProposals);
        this.missedShows.set(response.missedShows);
        this.loading.set(false);
      },
    });
  }
}
