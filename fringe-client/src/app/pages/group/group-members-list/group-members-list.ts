import { Component, input } from '@angular/core';
import type { GroupMember } from '../../../models';
import { UpperCasePipe } from '@angular/common';

@Component({
  imports: [UpperCasePipe],
  selector: 'fg-group-members-list',
  styleUrl: './group-members-list.scss',
  templateUrl: './group-members-list.html',
})
export class GroupMembersListComponent {
  public readonly members =
    input.required<ReadonlyArray<Readonly<GroupMember>>>();
}
