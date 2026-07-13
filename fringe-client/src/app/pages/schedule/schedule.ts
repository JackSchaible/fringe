import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { faClock, faLocationDot, faUserGroup, faCalendarXmark, faChevronDown, faChevronUp } from '@fortawesome/pro-light-svg-icons';
import { faArrowUpRightFromSquare, faCheck } from '@fortawesome/pro-solid-svg-icons';
import { ApiService } from '../../services/api.service';
import { AlternateProposal, ScheduleItem } from '../../models';

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
  readonly proposals = signal<AlternateProposal[]>([]);
  readonly expandedProposal = signal<number | null>(null);
  readonly activeProposalIndex = signal<number | null>(null);

  protected readonly faClock = faClock;
  protected readonly faLocationDot = faLocationDot;
  protected readonly faUserGroup = faUserGroup;
  protected readonly faCalendarXmark = faCalendarXmark;
  protected readonly faArrowUpRightFromSquare = faArrowUpRightFromSquare;
  protected readonly faChevronDown = faChevronDown;
  protected readonly faChevronUp = faChevronUp;
  protected readonly faCheck = faCheck;

  fringeUrl(showId: number): string {
    return `https://tickets.fringetheatre.ca/event/601:${showId}`;
  }

  activeSchedule(): ScheduleItem[] {
    const idx = this.activeProposalIndex();
    if (idx !== null) return this.proposals()[idx]?.items ?? this.schedule();
    return this.schedule();
  }

  toggleProposal(index: number): void {
    this.expandedProposal.update(cur => cur === index ? null : index);
  }

  acceptProposal(index: number): void {
    this.activeProposalIndex.set(index);
    this.expandedProposal.set(null);
  }

  resetToMain(): void {
    this.activeProposalIndex.set(null);
  }

  ngOnInit(): void {
    this.api.getSchedule().subscribe({
      next: response => {
        this.schedule.set(response.items);
        this.proposals.set(response.alternateProposals);
        this.loading.set(false);
      },
      error: (err) => {
        if (err.status === 400) this.noGroup.set(true);
        this.loading.set(false);
      },
    });
  }
}
