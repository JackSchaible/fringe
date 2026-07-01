import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { faCheck } from '@fortawesome/pro-solid-svg-icons';
import { faUsers, faCirclePlus, faTicket, faCopy } from '@fortawesome/pro-regular-svg-icons';
import { ApiService } from '../../services/api.service';
import { Group } from '../../models';

@Component({
  selector: 'fg-group',
  imports: [FormsModule, FaIconComponent],
  templateUrl: './group.html',
  styleUrl: './group.scss',
})
export class GroupPage implements OnInit {
  private readonly api = inject(ApiService);

  readonly loading = signal(true);
  readonly myGroup = signal<Group | null>(null);
  readonly error = signal('');
  readonly copied = signal(false);

  readonly groupName = signal('');
  readonly inviteCode = signal('');

  protected readonly faCheck = faCheck;
  protected readonly faUsers = faUsers;
  protected readonly faCirclePlus = faCirclePlus;
  protected readonly faTicket = faTicket;
  protected readonly faCopy = faCopy;

  ngOnInit(): void {
    this.api.getMyGroup().subscribe({
      next: g => {
        this.myGroup.set(g);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  createGroup() {
    const name = this.groupName().trim();
    if (!name) return;
    this.error.set('');
    this.api.createGroup(name).subscribe({
      next: g => this.myGroup.set(g),
      error: e => this.error.set(e.error ?? 'Failed to create group'),
    });
  }

  joinGroup() {
    const code = this.inviteCode().trim().toUpperCase();
    if (!code) return;
    this.error.set('');
    this.api.joinGroup(code).subscribe({
      next: g => this.myGroup.set(g),
      error: e => this.error.set(e.error ?? 'Invalid invite code'),
    });
  }

  copyCode() {
    const code = this.myGroup()?.inviteCode;
    if (!code) return;
    navigator.clipboard.writeText(code).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    });
  }
}
