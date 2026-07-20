import { Component, input } from '@angular/core';
import type { TransferConflict } from '../../../../models';

@Component({
  imports: [],
  selector: 'fg-transfer-conflict-detail',
  styleUrl: './transfer-conflict-detail.scss',
  templateUrl: './transfer-conflict-detail.html',
})
export class TransferConflictDetailComponent {
  public readonly conflict = input.required<Readonly<TransferConflict>>();

  private static formatLabel(title: string, venueName: string | null): string {
    if (venueName !== null && venueName !== '') {
      return `${title} (${venueName})`;
    }
    return title;
  }

  protected originLabel(): string {
    const conflict = this.conflict();
    return TransferConflictDetailComponent.formatLabel(
      conflict.originShowTitle,
      conflict.originVenueName,
    );
  }

  protected destinationLabel(): string {
    const conflict = this.conflict();
    return TransferConflictDetailComponent.formatLabel(
      conflict.destinationShowTitle,
      conflict.destinationVenueName,
    );
  }
}
