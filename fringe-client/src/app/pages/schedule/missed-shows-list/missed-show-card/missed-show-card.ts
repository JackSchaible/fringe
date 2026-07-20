import { Component, computed, input } from '@angular/core';
import type { Show, TransferConflict } from '../../../../models';
import { MissedShowTagsComponent } from '../missed-show-tags/missed-show-tags';
import { TransferConflictDetailComponent } from '../transfer-conflict-detail/transfer-conflict-detail';

@Component({
  imports: [MissedShowTagsComponent, TransferConflictDetailComponent],
  selector: 'fg-missed-show-card',
  styleUrl: './missed-show-card.scss',
  templateUrl: './missed-show-card.html',
})
export class MissedShowCardComponent {
  public readonly show = input.required<Show>();
  public readonly conflictsWithScheduled = input.required<boolean>();
  public readonly blockedByMembers = input.required<ReadonlyArray<string>>();
  public readonly transferConflict =
    input.required<Readonly<TransferConflict> | null>();

  protected readonly primaryReason = computed<boolean>(
    () =>
      this.conflictsWithScheduled() ||
      Boolean(this.blockedByMembers().length) ||
      this.transferConflict() !== null,
  );
}
