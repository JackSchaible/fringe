import { Component, type OnInit, inject, signal } from '@angular/core';
import {
  faCirclePlus,
  faCopy,
  faTicket,
  faUser,
  faUsers,
} from '@fortawesome/pro-regular-svg-icons';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { FormsModule } from '@angular/forms';
import type { Group } from '../../models';
import { GroupMembersListComponent } from './group-members-list/group-members-list';
import type { HttpErrorResponse } from '@angular/common/http';
import { faCheck } from '@fortawesome/pro-solid-svg-icons';

const TOAST_DURATION_MS = 2_000;

@Component({
  imports: [FormsModule, FaIconComponent, GroupMembersListComponent],
  selector: 'fg-group',
  styleUrl: './group.scss',
  templateUrl: './group.html',
})
export class GroupPage implements OnInit {
  public readonly loading = signal(true);
  public readonly myGroup = signal<Group | null>(null);
  public readonly error = signal('');
  public readonly copied = signal(false);
  public readonly confirmingDelete = signal(false);
  public readonly deleting = signal(false);

  public readonly groupName = signal('');
  public readonly inviteCode = signal('');
  public readonly username = signal('');
  public readonly usernameSaved = signal(false);
  public readonly savingUsername = signal(false);

  protected readonly faCheck = faCheck;
  protected readonly faUsers = faUsers;
  protected readonly faCirclePlus = faCirclePlus;
  protected readonly faTicket = faTicket;
  protected readonly faCopy = faCopy;
  protected readonly faUser = faUser;

  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);

  public ngOnInit(): void {
    this.api.getMyGroup().subscribe({
      error: () => {
        this.loading.set(false);
      },
      next: (group) => {
        this.myGroup.set(group);
        this.loading.set(false);
      },
    });

    this.api.getMe().subscribe({
      error: () => {
        // Failing to load the display name is non-fatal; the field just stays blank.
      },
      next: (user) => {
        this.username.set(user.displayName);
      },
    });
  }

  public createGroup(): void {
    const name = this.groupName().trim();
    if (!name) {
      return;
    }
    this.error.set('');
    this.api.createGroup(name).subscribe({
      error: (err: HttpErrorResponse) => {
        let message = 'Failed to create group';
        if (typeof err.error === 'string') {
          message = err.error;
        }
        this.error.set(message);
      },
      next: (group) => {
        this.myGroup.set(group);
      },
    });
  }

  public joinGroup(): void {
    const code = this.inviteCode().trim().toUpperCase();
    if (!code) {
      return;
    }
    this.error.set('');
    this.api.joinGroup(code).subscribe({
      error: (err: HttpErrorResponse) => {
        let message = 'Invalid invite code';
        if (typeof err.error === 'string') {
          message = err.error;
        }
        this.error.set(message);
      },
      next: (group) => {
        this.myGroup.set(group);
      },
    });
  }

  public copyCode(): void {
    const code = this.myGroup()?.inviteCode;
    if (typeof code !== 'string') {
      return;
    }
    void navigator.clipboard.writeText(code).then(() => {
      this.copied.set(true);
      setTimeout(() => {
        this.copied.set(false);
      }, TOAST_DURATION_MS);
    });
  }

  public saveUsername(): void {
    const name = this.username().trim();
    if (!name) {
      return;
    }
    this.savingUsername.set(true);
    this.api.updateDisplayName(name).subscribe({
      error: () => {
        this.savingUsername.set(false);
      },
      next: () => {
        this.savingUsername.set(false);
        this.usernameSaved.set(true);
        setTimeout(() => {
          this.usernameSaved.set(false);
        }, TOAST_DURATION_MS);
        this.refreshGroup();
      },
    });
  }

  public deleteAccount(): void {
    this.deleting.set(true);
    this.api.deleteMe().subscribe({
      error: () => {
        this.deleting.set(false);
        this.confirmingDelete.set(false);
        this.error.set('Failed to delete account. Please try again.');
      },
      next: () => {
        void this.auth.signOut();
      },
    });
  }

  private refreshGroup(): void {
    // Refresh group so the member row reflects the new name.
    this.api.getMyGroup().subscribe({
      error: () => {
        // A stale member row is non-fatal; the next successful refresh will fix it.
      },
      next: (group) => {
        this.myGroup.set(group);
      },
    });
  }
}
