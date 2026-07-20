import { CdkDrag, CdkDragHandle } from '@angular/cdk/drag-drop';
import { Component, input, output } from '@angular/core';
import {
  faArrowUpRightFromSquare,
  faCircleInfo,
  faGripVertical,
  faMasksTheater,
  faXmark,
} from '../../../vendor/fontawesome-icons/solid';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { NgOptimizedImage } from '@angular/common';
import type { Show } from '../../../models';
import { fringeUrl } from '../fringe-url';

const THUMB_SIZE = 38;

@Component({
  imports: [CdkDrag, CdkDragHandle, FaIconComponent, NgOptimizedImage],
  selector: 'fg-ranked-show-item',
  styleUrl: './ranked-show-item.scss',
  templateUrl: './ranked-show-item.html',
})
export class RankedShowItemComponent {
  public readonly show = input.required<Show>();
  public readonly rank = input.required<number>();
  public readonly viewDetails = output<Show>();
  public readonly remove = output<Show>();

  protected readonly thumbSize = THUMB_SIZE;
  protected readonly fringeUrl = fringeUrl;

  protected readonly faGripVertical = faGripVertical;
  protected readonly faMasksTheater = faMasksTheater;
  protected readonly faArrowUpRightFromSquare = faArrowUpRightFromSquare;
  protected readonly faCircleInfo = faCircleInfo;
  protected readonly faXmark = faXmark;

  protected onRemoveClick(event: Event): void {
    event.stopPropagation();
    this.remove.emit(this.show());
  }
}
