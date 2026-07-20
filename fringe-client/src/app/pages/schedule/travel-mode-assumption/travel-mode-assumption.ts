import { Component, input } from '@angular/core';
import type { TravelMode } from '../../../models';

@Component({
  imports: [],
  selector: 'fg-travel-mode-assumption',
  styleUrl: './travel-mode-assumption.scss',
  templateUrl: './travel-mode-assumption.html',
})
export class TravelModeAssumptionComponent {
  public readonly mode = input.required<TravelMode>();
}
