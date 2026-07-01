import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CookieConsentService } from '../../services/cookie-consent.service';

@Component({
  selector: 'fg-cookie-consent-drawer',
  imports: [RouterLink],
  templateUrl: './cookie-consent-drawer.html',
  styleUrl: './cookie-consent-drawer.scss',
})
export class CookieConsentDrawer {
  protected readonly consent = inject(CookieConsentService);
}
