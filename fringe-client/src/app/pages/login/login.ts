import { Component, effect, inject, signal } from '@angular/core';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { FormsModule } from '@angular/forms';
import { LoginEmailStepComponent } from './email-step/email-step';
import { LoginOtpStepComponent } from './otp-step/otp-step';
import { LoginUsernameStepComponent } from './username-step/username-step';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { firstValueFrom } from 'rxjs';

const INITIAL_ATTEMPT = 0,
  MAX_TURNSTILE_RETRIES = 20,
  NEXT_TICK_MS = 0,
  RETRY_STEP = 1,
  TURNSTILE_RETRY_INTERVAL_MS = 100;

interface Turnstile {
  render: (
    container: HTMLElement,
    options: Readonly<{
      sitekey: string;
      callback?: (token: string) => void;
      'expired-callback'?: () => void;
      'error-callback'?: () => void;
    }>,
  ) => string;
  reset: (widgetId: string) => void;
  remove: (widgetId: string) => void;
}

@Component({
  imports: [
    FormsModule,
    LoginEmailStepComponent,
    LoginOtpStepComponent,
    LoginUsernameStepComponent,
  ],
  selector: 'fg-login',
  styleUrl: './login.scss',
  templateUrl: './login.html',
})
export class LoginPage {
  public readonly step = signal<'email' | 'otp' | 'username'>('email');
  public readonly email = signal('');
  public readonly otp = signal('');
  public readonly username = signal('');
  public readonly devUserId = signal('user1');
  public readonly error = signal('');
  public readonly loading = signal(false);
  public readonly captchaToken = signal<string | null>(null);
  public readonly agreedToTerms = signal(false);

  protected readonly auth = inject(AuthService);
  protected readonly api = inject(ApiService);
  protected readonly router = inject(Router);
  protected readonly cognitoConfigured = Boolean(environment.cognitoUserPoolId);

  private turnstileWidgetId: string | null = null;

  public constructor() {
    effect(() => {
      if (this.step() === 'email') {
        setTimeout(() => {
          this.renderTurnstile();
        }, NEXT_TICK_MS);
      } else {
        this.captchaToken.set(null);
        this.turnstileWidgetId = null;
      }
    });
  }

  public async sendCode(): Promise<void> {
    const token = this.captchaToken();
    if (token === null) {
      this.error.set('Please complete the captcha.');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    await this.submitCaptchaAndSendOtp(token);
    this.loading.set(false);
  }

  public async verifyCode(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    const confirmed = await this.tryConfirmOtp();
    if (!confirmed) {
      this.loading.set(false);
      return;
    }
    await this.proceedPastOtp();
  }

  public async saveUsername(): Promise<void> {
    const name = this.username().trim();
    if (!name) {
      await this.router.navigate(['/shows']);
      return;
    }
    this.loading.set(true);
    try {
      await firstValueFrom(this.api.updateDisplayName(name));
    } catch {
      // Non-fatal — proceed anyway
    }
    this.loading.set(false);
    await this.router.navigate(['/shows']);
  }

  public async skipUsername(): Promise<void> {
    await this.router.navigate(['/shows']);
  }

  public back(): void {
    this.step.set('email');
    this.otp.set('');
    this.error.set('');
  }

  public signInDev(): void {
    if (!this.devUserId().trim()) {
      return;
    }
    this.auth.signInDev(this.devUserId().trim());
    void this.router.navigate(['/shows']);
  }

  private async submitCaptchaAndSendOtp(token: string): Promise<void> {
    try {
      await firstValueFrom(this.api.verifyCaptcha(token));
      await AuthService.sendOtp(this.email());
      this.step.set('otp');
    } catch {
      this.error.set('Failed to send code. Please try again.');
      this.resetCaptcha();
    }
  }

  private async proceedPastOtp(): Promise<void> {
    const isReturningUser = await this.upsertUserProfile();
    this.loading.set(false);
    if (isReturningUser) {
      await this.router.navigate(['/shows']);
      return;
    }
    this.step.set('username');
  }

  private async tryConfirmOtp(): Promise<boolean> {
    try {
      await this.auth.confirmOtp(this.otp());
      return true;
    } catch {
      this.error.set('Incorrect code. Please try again.');
      return false;
    }
  }

  /** Returns true if a profile already existed (returning user) — leaves it untouched. */
  private async upsertUserProfile(): Promise<boolean> {
    const user = this.auth.currentUser();
    if (!user) {
      return false;
    }
    try {
      await firstValueFrom(this.api.getMe());
      return true;
    } catch {
      // No profile yet — this is a first-time login.
    }
    try {
      await firstValueFrom(this.api.upsertMe(user.displayName, user.email));
    } catch {
      // Non-fatal
    }
    return false;
  }

  private renderTurnstile(attempt = INITIAL_ATTEMPT): void {
    if (!this.cognitoConfigured) {
      return;
    }
    const { turnstile } = window as Window & { turnstile?: Turnstile },
      container = document.getElementById('turnstile-widget');
    if (!turnstile) {
      if (attempt < MAX_TURNSTILE_RETRIES) {
        setTimeout(() => {
          this.renderTurnstile(attempt + RETRY_STEP);
        }, TURNSTILE_RETRY_INTERVAL_MS);
      }
      return;
    }
    if (!container) {
      return;
    }
    this.turnstileWidgetId = turnstile.render(container, {
      callback: (token) => {
        this.captchaToken.set(token);
      },
      'error-callback': () => {
        this.captchaToken.set(null);
      },
      'expired-callback': () => {
        this.captchaToken.set(null);
      },
      sitekey: environment.turnstileSiteKey,
    });
  }

  private resetCaptcha(): void {
    const { turnstile } = window as Window & { turnstile?: Turnstile };
    if (turnstile && this.turnstileWidgetId !== null) {
      turnstile.reset(this.turnstileWidgetId);
    }
    this.captchaToken.set(null);
  }
}
