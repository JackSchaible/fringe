import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  type AlternateProposal,
  type MissedShow,
  type ScheduleItem,
  type TravelMode,
  isTravelMode,
} from '../../models';
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
import { ScheduleItemRowComponent } from './schedule-item-row/schedule-item-row';
import { TravelModeAssumptionComponent } from './travel-mode-assumption/travel-mode-assumption';
import { TravelModeSelectorComponent } from './travel-mode-selector/travel-mode-selector';
import { faCheck } from '@fortawesome/pro-solid-svg-icons';

const HTTP_BAD_REQUEST = 400;
const DEFAULT_TRAVEL_MODE: TravelMode = 'walking';

@Component({
  imports: [
    RouterLink,
    FaIconComponent,
    ScheduleItemRowComponent,
    MissedShowsListComponent,
    AlternateProposalsComponent,
    TravelModeSelectorComponent,
    TravelModeAssumptionComponent,
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
  public readonly travelMode = signal<TravelMode>(DEFAULT_TRAVEL_MODE);

  protected readonly faUserGroup = faUserGroup;
  protected readonly faCalendarXmark = faCalendarXmark;
  protected readonly faCalendarClock = faCalendarClock;
  protected readonly faCheck = faCheck;

  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

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

  public setTravelMode(mode: TravelMode): void {
    if (mode === this.travelMode()) {
      return;
    }
    this.loading.set(true);
    void this.router.navigate([], {
      queryParams: { mode },
      queryParamsHandling: 'merge',
      relativeTo: this.route,
      replaceUrl: true,
    });
    this.fetchSchedule(mode);
  }

  public ngOnInit(): void {
    const queryMode = this.route.snapshot.queryParamMap.get('mode');
    let initialMode = DEFAULT_TRAVEL_MODE;
    if (isTravelMode(queryMode)) {
      initialMode = queryMode;
    }
    this.travelMode.set(initialMode);
    this.fetchSchedule(initialMode);
  }

  private fetchSchedule(mode: TravelMode): void {
    this.api.getSchedule(mode).subscribe({
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
        this.travelMode.set(response.travelMode);
        this.loading.set(false);
      },
    });
  }
}
