import type { AlternateProposal, ScheduleItem } from '../../../models';
import { Component, input, output, signal } from '@angular/core';
import { faChevronDown, faChevronUp } from '@fortawesome/pro-light-svg-icons';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { NeedsChangeComponent } from '../needs-change/needs-change';
import { ProposalScheduleItemComponent } from '../proposal-schedule-item/proposal-schedule-item';
import { faCheck } from '@fortawesome/pro-solid-svg-icons';

type ReadonlyAlternateProposal = Readonly<{
  description: AlternateProposal['description'];
  excludedMemberName: AlternateProposal['excludedMemberName'];
  items: ReadonlyArray<Readonly<ScheduleItem>>;
}>;

@Component({
  imports: [
    FaIconComponent,
    NeedsChangeComponent,
    ProposalScheduleItemComponent,
  ],
  selector: 'fg-alternate-proposals',
  styleUrl: './alternate-proposals.scss',
  templateUrl: './alternate-proposals.html',
})
export class AlternateProposalsComponent {
  public readonly proposals =
    input.required<ReadonlyArray<ReadonlyAlternateProposal>>();
  public readonly schedule =
    input.required<ReadonlyArray<Readonly<ScheduleItem>>>();
  public readonly subtitle = input.required<string>();
  public readonly accept = output<number>();

  protected readonly expandedProposal = signal<number | null>(null);

  protected readonly faChevronDown = faChevronDown;
  protected readonly faChevronUp = faChevronUp;
  protected readonly faCheck = faCheck;

  protected toggleProposal(index: number): void {
    this.expandedProposal.update((cur) => {
      if (cur === index) {
        return null;
      }
      return index;
    });
  }

  protected onAccept(index: number): void {
    this.accept.emit(index);
  }

  protected blockedShows(
    proposal: ReadonlyAlternateProposal,
  ): ReadonlyArray<Readonly<ScheduleItem>> {
    const mainIds = new Set(this.schedule().map((entry) => entry.show.showId));
    return proposal.items.filter((item) => !mainIds.has(item.show.showId));
  }

  protected isNewInProposal(item: Readonly<ScheduleItem>): boolean {
    const mainIds = new Set(this.schedule().map((entry) => entry.show.showId));
    return !mainIds.has(item.show.showId);
  }
}
