import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { faFilm, faCalendarDays, faUsers, faArrowRightFromBracket } from '@fortawesome/pro-solid-svg-icons';
import { AuthService } from './services/auth.service';
import { CookieConsentService } from './services/cookie-consent.service';
import { CookieConsentDrawer } from './components/cookie-consent-drawer/cookie-consent-drawer';

@Component({
  selector: 'fg-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FaIconComponent, CookieConsentDrawer],
  templateUrl: './app.html',
  styleUrl: './app.scss'
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

  ngOnInit(): void {
    if (this.auth.devMode) {
      this.auth.initDevSession();
    } else {
      void this.auth.loadUserFromCognito();
    }
  }
}
