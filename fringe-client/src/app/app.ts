import {
  Component,
  LOCALE_ID,
  type OnInit,
  Renderer2,
  inject,
  signal,
} from '@angular/core';
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
import { NgOptimizedImage } from '@angular/common';

const FR_PATH_PREFIX = '/fr';
const NAV_HIDE_THRESHOLD_PX = 80;
const SCROLL_TOP_PX = 0;

@Component({
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    FaIconComponent,
    NgOptimizedImage,
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

  protected readonly navHidden = signal(false);
  private readonly renderer = inject(Renderer2);
  private lastScrollY = SCROLL_TOP_PX;

  public ngOnInit(): void {
    if (this.auth.devMode) {
      this.auth.initDevSession();
    } else {
      void this.auth.loadUserFromCognito();
    }
    this.renderer.listen('window', 'scroll', () => {
      this.onWindowScroll();
    });
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

  /*
   * Hides the sticky nav on scroll-down and brings it back on scroll-up,
   * mirroring the common mobile-browser chrome pattern.
   */
  private onWindowScroll(): void {
    const currentY = window.scrollY;
    const scrolledDown = currentY > this.lastScrollY;
    this.navHidden.set(scrolledDown && currentY > NAV_HIDE_THRESHOLD_PX);
    this.lastScrollY = currentY;
  }
}
