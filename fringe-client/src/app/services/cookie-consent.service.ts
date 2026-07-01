import { computed, effect, Injectable, signal } from '@angular/core';

export type ConsentState = 'accepted' | 'rejected' | null;

const STORAGE_KEY = 'fringe_cookie_consent';

@Injectable({ providedIn: 'root' })
export class CookieConsentService {
  readonly consent = signal<ConsentState>(
    localStorage.getItem(STORAGE_KEY) as ConsentState,
  );
  readonly hasDecided = computed(() => this.consent() !== null);
  readonly accepted = computed(() => this.consent() === 'accepted');

  constructor() {
    effect(() => {
      if (this.accepted()) this.loadOptionalScripts();
    });
  }

  accept(): void {
    localStorage.setItem(STORAGE_KEY, 'accepted');
    this.consent.set('accepted');
  }

  reject(): void {
    localStorage.setItem(STORAGE_KEY, 'rejected');
    this.consent.set('rejected');
  }

  private loadOptionalScripts(): void {
    this.injectGoogleFonts();
    // GA: add here when ready
    // this.injectGoogleAnalytics('G-XXXXXXXXXX');
  }

  private injectGoogleFonts(): void {
    if (document.getElementById('google-fonts')) return;
    const preconnect1 = document.createElement('link');
    preconnect1.rel = 'preconnect';
    preconnect1.href = 'https://fonts.googleapis.com';
    document.head.appendChild(preconnect1);

    const preconnect2 = document.createElement('link');
    preconnect2.rel = 'preconnect';
    preconnect2.href = 'https://fonts.gstatic.com';
    preconnect2.crossOrigin = '';
    document.head.appendChild(preconnect2);

    const link = document.createElement('link');
    link.id = 'google-fonts';
    link.rel = 'stylesheet';
    link.href = 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap';
    document.head.appendChild(link);
  }
}
