import { Component, inject } from '@angular/core';
import { CookieConsentService } from '../../services/cookie-consent.service';
import { RouterLink } from '@angular/router';

@Component({
  imports: [RouterLink],
  selector: 'fg-cookie-consent-drawer',
  styleUrl: './cookie-consent-drawer.scss',
  templateUrl: './cookie-consent-drawer.html',
})
export class CookieConsentDrawerComponent {
  protected readonly consent = inject(CookieConsentService);
}
