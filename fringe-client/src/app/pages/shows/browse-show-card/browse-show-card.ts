import { Component, input, output } from '@angular/core';
import {
  faCircleInfo,
  faMasksTheater,
} from '../../../vendor/fontawesome-icons/solid';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { NgOptimizedImage } from '@angular/common';
import type { Show } from '../../../models';
import { fringeUrl } from '../fringe-url';

const EMPTY_PRICE = 0,
  CARD_IMAGE_WIDTH = 640,
  CARD_IMAGE_HEIGHT = 360;

@Component({
  imports: [FaIconComponent, NgOptimizedImage],
  selector: 'fg-browse-show-card',
  styleUrl: './browse-show-card.scss',
  templateUrl: './browse-show-card.html',
})
export class BrowseShowCardComponent {
  public readonly show = input.required<Show>();
  public readonly viewDetails = output<Show>();
  public readonly add = output<Show>();

  protected readonly cardImageWidth = CARD_IMAGE_WIDTH;
  protected readonly cardImageHeight = CARD_IMAGE_HEIGHT;
  protected readonly emptyPrice = EMPTY_PRICE;
  protected readonly fringeUrl = fringeUrl;

  protected readonly faMasksTheater = faMasksTheater;
  protected readonly faCircleInfo = faCircleInfo;

  protected onSelectClick(event: Event): void {
    event.stopPropagation();
    this.viewDetails.emit(this.show());
  }
}
