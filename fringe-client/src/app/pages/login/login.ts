import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { faPaperPlane, faArrowRightToBracket, faArrowLeft } from '@fortawesome/pro-solid-svg-icons';
import { faEnvelope, faKey } from '@fortawesome/pro-regular-svg-icons';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../services/api.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'fg-login',
  imports: [FormsModule, FaIconComponent],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class LoginPage {
  protected readonly auth = inject(AuthService);
  protected readonly api = inject(ApiService);
  protected readonly router = inject(Router);
  protected readonly cognitoConfigured = !!environment.cognitoUserPoolId;

  readonly step = signal<'email' | 'otp'>('email');
  readonly email = signal('');
  readonly otp = signal('');
  readonly devUserId = signal('user1');
  readonly error = signal('');
  readonly loading = signal(false);

  protected readonly faPaperPlane = faPaperPlane;
  protected readonly faArrowRightToBracket = faArrowRightToBracket;
  protected readonly faArrowLeft = faArrowLeft;
  protected readonly faEnvelope = faEnvelope;
  protected readonly faKey = faKey;

  async sendCode() {
    this.loading.set(true);
    this.error.set('');
    try {
      await this.auth.sendOtp(this.email());
      this.step.set('otp');
    } catch {
      this.error.set('Failed to send code. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  async verifyCode() {
    this.loading.set(true);
    this.error.set('');
    try {
      await this.auth.confirmOtp(this.otp());
      const user = this.auth.currentUser();
      if (user) {
        await firstValueFrom(this.api.upsertMe(user.displayName, user.email));
      }
      await this.router.navigate(['/shows']);
    } catch {
      this.error.set('Incorrect code. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  back() {
    this.step.set('email');
    this.otp.set('');
    this.error.set('');
  }

  signInDev() {
    if (!this.devUserId().trim()) return;
    this.auth.signInDev(this.devUserId().trim());
    void this.router.navigate(['/shows']);
  }
}
