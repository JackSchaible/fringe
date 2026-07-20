import { Component, input } from '@angular/core';
import type { TransferConflict } from '../../../../models';

@Component({
  imports: [],
  selector: 'fg-transfer-conflict-tag',
  styleUrl: './transfer-conflict-tag.scss',
  templateUrl: './transfer-conflict-tag.html',
})
export class TransferConflictTagComponent {
  public readonly conflict = input.required<Readonly<TransferConflict>>();
}
