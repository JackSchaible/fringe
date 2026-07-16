import { Component, input, output } from '@angular/core';
import {
  faArrowUpRightFromSquare,
  faXmark,
} from '@fortawesome/pro-solid-svg-icons';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { NgOptimizedImage } from '@angular/common';
import type { Show } from '../../../models';
import { ShowDetailStatsComponent } from '../show-detail-stats/show-detail-stats';
import { fringeUrl } from '../fringe-url';

const EMPTY_PRICE = 0,
  DETAIL_IMAGE_WIDTH = 640,
  DETAIL_IMAGE_HEIGHT = 360;

@Component({
  imports: [FaIconComponent, NgOptimizedImage, ShowDetailStatsComponent],
  selector: 'fg-show-detail-panel',
  styleUrl: './show-detail-panel.scss',
  templateUrl: './show-detail-panel.html',
})
export class ShowDetailPanelComponent {
  public readonly show = input.required<Show>();
  public readonly dismiss = output();

  protected readonly detailImageWidth = DETAIL_IMAGE_WIDTH;
  protected readonly detailImageHeight = DETAIL_IMAGE_HEIGHT;
  protected readonly emptyPrice = EMPTY_PRICE;
  protected readonly fringeUrl = fringeUrl;

  protected readonly faXmark = faXmark;
  protected readonly faArrowUpRightFromSquare = faArrowUpRightFromSquare;
}
