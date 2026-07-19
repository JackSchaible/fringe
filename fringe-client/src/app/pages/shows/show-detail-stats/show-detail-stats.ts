import { Component, computed, input } from '@angular/core';
import { faClock, faLocationDot } from '@fortawesome/pro-light-svg-icons';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import type { Show } from '../../../models';
import { venueDisplayName } from '../../../venue-display';

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

  protected readonly venueName = computed<string>(() =>
    venueDisplayName(this.show().venue),
  );

  // Omitted when it's identical to venueName — i.e. the name itself already fell back to the address, so showing it again below would be redundant.
  protected readonly venueAddress = computed<string>(() => {
    const address = this.show().venue?.address ?? '';
    if (address === '' || address === this.venueName()) {
      return '';
    }
    return address;
  });
}
