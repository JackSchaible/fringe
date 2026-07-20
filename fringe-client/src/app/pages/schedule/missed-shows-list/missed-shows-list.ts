import { Component, input } from '@angular/core';
import type { Show, TransferConflict } from '../../../models';
import { TransferConflictTagComponent } from './transfer-conflict-tag/transfer-conflict-tag';

@Component({
  imports: [TransferConflictTagComponent],
  selector: 'fg-missed-shows-list',
  styleUrl: './missed-shows-list.scss',
  templateUrl: './missed-shows-list.html',
})
export class MissedShowsListComponent {
  public readonly missedShows = input.required<
    ReadonlyArray<
      Readonly<{
        show: Show;
        conflictsWithScheduled: boolean;
        blockedByMembers: ReadonlyArray<string>;
        transferConflict: Readonly<TransferConflict> | null;
      }>
    >
  >();
}
