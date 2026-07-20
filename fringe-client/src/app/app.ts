import { Component, LOCALE_ID, type OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import {
  faArrowRightFromBracket,
  faCalendarDays,
  faClock,
  faFilm,
  faUsers,
} from './vendor/fontawesome-icons/solid';
import { AuthService } from './services/auth.service';
import { CookieConsentDrawerComponent } from './components/cookie-consent-drawer/cookie-consent-drawer';
import { CookieConsentService } from './services/cookie-consent.service';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';

const FR_PATH_PREFIX = '/fr';

@Component({
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    FaIconComponent,
    CookieConsentDrawerComponent,
  ],
  selector: 'fg-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class AppComponent implements OnInit {
  protected readonly auth = inject(AuthService);
  protected readonly year = new Date().getFullYear();
  // Injected here to ensure eager instantiation — loads optional scripts if already accepted
  protected readonly cookieConsent = inject(CookieConsentService);

  protected readonly isFrench = inject(LOCALE_ID)
    .toLowerCase()
    .startsWith('fr');

  protected readonly faFilm = faFilm;
  protected readonly faCalendarDays = faCalendarDays;
  protected readonly faUsers = faUsers;
  protected readonly faArrowRightFromBracket = faArrowRightFromBracket;
  protected readonly faClock = faClock;

  public ngOnInit(): void {
    if (this.auth.devMode) {
      this.auth.initDevSession();
    } else {
      void this.auth.loadUserFromCognito();
    }
  }

  /*
   * Each locale is a fully separate build, so switching locale is a real
   * navigation (not routerLink) to the same path under the other locale's
   * URL prefix.
   */
  protected switchLocaleHref(): string {
    const { pathname, search, hash } = window.location;
    return `${this.targetPathForOtherLocale(pathname)}${search}${hash}`;
  }

  private targetPathForOtherLocale(pathname: string): string {
    if (this.isFrench) {
      return pathname.replace(new RegExp(`^${FR_PATH_PREFIX}/?`, 'u'), '/');
    }
    if (pathname === '/') {
      return FR_PATH_PREFIX;
    }
    return `${FR_PATH_PREFIX}${pathname}`;
  }
}
