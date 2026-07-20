import { Component, input } from '@angular/core';
import type { MissedShow } from '../../../models';
import { MissedShowCardComponent } from './missed-show-card/missed-show-card';

@Component({
  imports: [MissedShowCardComponent],
  selector: 'fg-missed-shows-list',
  styleUrl: './missed-shows-list.scss',
  templateUrl: './missed-shows-list.html',
})
export class MissedShowsListComponent {
  public readonly missedShows =
    input.required<ReadonlyArray<Readonly<MissedShow>>>();
}
