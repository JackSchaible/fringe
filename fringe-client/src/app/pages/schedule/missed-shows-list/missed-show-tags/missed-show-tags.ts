import { Component, input } from '@angular/core';

@Component({
  imports: [],
  selector: 'fg-missed-show-tags',
  styleUrl: './missed-show-tags.scss',
  templateUrl: './missed-show-tags.html',
})
export class MissedShowTagsComponent {
  public readonly conflictsWithScheduled = input.required<boolean>();
  public readonly blockedByMembers = input.required<ReadonlyArray<string>>();
  public readonly hasTransferConflict = input.required<boolean>();
}
