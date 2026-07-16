import { Component, input } from '@angular/core';
import { faClock, faLocationDot } from '@fortawesome/pro-light-svg-icons';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import type { Show } from '../../../models';

@Component({
  imports: [FaIconComponent],
  selector: 'fg-show-detail-stats',
  styleUrl: './show-detail-stats.scss',
  templateUrl: './show-detail-stats.html',
})
export class ShowDetailStatsComponent {
  public readonly show = input.required<Show>();

  protected readonly faClock = faClock;
  protected readonly faLocationDot = faLocationDot;
}
