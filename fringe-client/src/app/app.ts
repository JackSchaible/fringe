import { Component, type OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import {
  faArrowRightFromBracket,
  faCalendarDays,
  faClock,
  faFilm,
  faUsers,
} from '@fortawesome/pro-solid-svg-icons';
import { AuthService } from './services/auth.service';
import { CookieConsentDrawerComponent } from './components/cookie-consent-drawer/cookie-consent-drawer';
import { CookieConsentService } from './services/cookie-consent.service';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';

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
}
