import { Component, input, output } from '@angular/core';

const DEFAULT_STEP = 1;

@Component({
  imports: [],
  selector: 'fg-range-slider',
  styleUrl: './range-slider.scss',
  templateUrl: './range-slider.html',
})
export class RangeSliderComponent {
  public readonly label = input('');
  public readonly min = input.required<number>();
  public readonly max = input.required<number>();
  public readonly step = input(DEFAULT_STEP);
  public readonly value = input.required<readonly [number, number]>();
  public readonly valuePrefix = input('');
  public readonly valueSuffix = input('');

  public readonly valueChange = output<readonly [number, number]>();

  protected onMinInput(rawValue: string): void {
    const [, currentMax] = this.value(),
      nextMin = Math.min(Number(rawValue), currentMax);
    this.valueChange.emit([nextMin, currentMax]);
  }

  protected onMaxInput(rawValue: string): void {
    const [currentMin] = this.value(),
      nextMax = Math.max(Number(rawValue), currentMin);
    this.valueChange.emit([currentMin, nextMax]);
  }
}
