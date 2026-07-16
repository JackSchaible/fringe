import { Injectable, computed, effect, signal } from '@angular/core';

export enum ConsentState {
  accepted = 'accepted',
  rejected = 'rejected',
}

const STORAGE_KEY = 'fringe_cookie_consent',
  configurePreconnect = (
    href: string,
    corsValue: string | null = null,
  ): void => {
    const preconnect1 = document.createElement('link');
    preconnect1.rel = 'preconnect';
    preconnect1.href = href;

    if (corsValue !== null) {
      preconnect1.crossOrigin = corsValue;
    }

    document.head.appendChild(preconnect1);
  },
  injectGoogleFonts = (): void => {
    const link = document.createElement('link');
    link.id = 'google-fonts';
    link.rel = 'stylesheet';
    link.href =
      'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap';
    document.head.appendChild(link);
  },
  injectGoogleUtilities = (): void => {
    if (document.getElementById('google-fonts')) {
      return;
    }

    configurePreconnect('https://fonts.googleapis.com');
    configurePreconnect('https://fonts.gstatic.com', '');
    injectGoogleFonts();
  },
  readStoredConsent = (): ConsentState | null => {
    const stored = localStorage.getItem(STORAGE_KEY);

    if (stored === ConsentState.accepted || stored === ConsentState.rejected) {
      return stored;
    }

    return null;
  };

@Injectable({ providedIn: 'root' })
export class CookieConsentService {
  public readonly consent = signal<ConsentState | null>(readStoredConsent());
  public readonly hasDecided = computed(() => this.consent() !== null);
  public readonly accepted = computed(
    () => this.consent() === ConsentState.accepted,
  );

  public constructor() {
    effect(() => {
      if (this.accepted()) {
        injectGoogleUtilities();
        // GA: add here when ready
        // InjectGoogleAnalytics('G-XXXXXXXXXX');
      }
    });
  }

  public accept(): void {
    localStorage.setItem(STORAGE_KEY, ConsentState.accepted);
    this.consent.set(ConsentState.accepted);
  }

  public reject(): void {
    localStorage.setItem(STORAGE_KEY, ConsentState.rejected);
    this.consent.set(ConsentState.rejected);
  }
}
