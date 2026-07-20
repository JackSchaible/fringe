import { Component, input, output } from '@angular/core';
import {
  faCar,
  faPersonBiking,
  faPersonWalking,
} from '../../../vendor/fontawesome-icons/light';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import type { TravelMode } from '../../../models';

@Component({
  imports: [FaIconComponent],
  selector: 'fg-travel-mode-selector',
  styleUrl: './travel-mode-selector.scss',
  templateUrl: './travel-mode-selector.html',
})
export class TravelModeSelectorComponent {
  public readonly mode = input.required<TravelMode>();
  public readonly modeChange = output<TravelMode>();

  protected readonly faPersonWalking = faPersonWalking;
  protected readonly faPersonBiking = faPersonBiking;
  protected readonly faCar = faCar;

  protected select(mode: TravelMode): void {
    if (mode !== this.mode()) {
      this.modeChange.emit(mode);
    }
  }
}
