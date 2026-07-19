import { Component, input, output } from '@angular/core';

export interface PillOption {
  readonly label: string;
  readonly title?: string;
  readonly value: string;
}

@Component({
  imports: [],
  selector: 'fg-pill-group',
  styleUrl: './pill-group.scss',
  templateUrl: './pill-group.html',
})
export class PillGroupComponent {
  public readonly label = input('');
  public readonly options = input.required<ReadonlyArray<PillOption>>();
  public readonly selected = input.required<ReadonlySet<string>>();

  public readonly toggled = output<string>();
}
