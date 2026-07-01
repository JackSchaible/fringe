import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { faPaperPlane, faArrowRightToBracket, faArrowLeft } from '@fortawesome/pro-solid-svg-icons';
import { faEnvelope, faKey } from '@fortawesome/pro-regular-svg-icons';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../services/api.service';
import { environment } from '../../../environments/environment';

interface Turnstile {
  render(container: HTMLElement, options: {
    sitekey: string;
    callback?: (token: string) => void;
    'expired-callback'?: () => void;
    'error-callback'?: () => void;
  }): string;
  reset(widgetId: string): void;
  remove(widgetId: string): void;
}

@Component({
  selector: 'fg-login',
  imports: [FormsModule, FaIconComponent, RouterLink],
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
  readonly captchaToken = signal<string | null>(null);
  readonly agreedToTerms = signal(false);

  private turnstileWidgetId: string | null = null;

  protected readonly faPaperPlane = faPaperPlane;
  protected readonly faArrowRightToBracket = faArrowRightToBracket;
  protected readonly faArrowLeft = faArrowLeft;
  protected readonly faEnvelope = faEnvelope;
  protected readonly faKey = faKey;

  constructor() {
    effect(() => {
      if (this.step() === 'email') {
        setTimeout(() => this.renderTurnstile(), 0);
      } else {
        this.captchaToken.set(null);
        this.turnstileWidgetId = null;
      }
    });
  }

  private renderTurnstile(attempt = 0) {
    if (!this.cognitoConfigured) return;
    const turnstile = (window as Window & { turnstile?: Turnstile }).turnstile;
    if (!turnstile) {
      if (attempt < 20) setTimeout(() => this.renderTurnstile(attempt + 1), 100);
      return;
    }
    const container = document.getElementById('turnstile-widget');
    if (!container) return;
    this.turnstileWidgetId = turnstile.render(container, {
      sitekey: environment.turnstileSiteKey,
      callback: (token) => this.captchaToken.set(token),
      'expired-callback': () => this.captchaToken.set(null),
      'error-callback': () => this.captchaToken.set(null),
    });
  }

  private resetCaptcha() {
    const turnstile = (window as Window & { turnstile?: Turnstile }).turnstile;
    if (turnstile && this.turnstileWidgetId != null) {
      turnstile.reset(this.turnstileWidgetId);
    }
    this.captchaToken.set(null);
  }

  async sendCode() {
    this.loading.set(true);
    this.error.set('');
    try {
      await firstValueFrom(this.api.verifyCaptcha(this.captchaToken()!));
      await this.auth.sendOtp(this.email());
      this.step.set('otp');
    } catch {
      this.error.set('Failed to send code. Please try again.');
      this.resetCaptcha();
    } finally {
      this.loading.set(false);
    }
  }

  async verifyCode() {
    this.loading.set(true);
    this.error.set('');
    try {
      await this.auth.confirmOtp(this.otp());
    } catch {
      this.error.set('Incorrect code. Please try again.');
      this.loading.set(false);
      return;
    }
    const user = this.auth.currentUser();
    if (user) {
      try {
        await firstValueFrom(this.api.upsertMe(user.displayName, user.email));
      } catch {
        // upsertMe failure is non-fatal — user is authenticated, proceed to app
      }
    }
    this.loading.set(false);
    await this.router.navigate(['/shows']);
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
