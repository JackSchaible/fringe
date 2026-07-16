import { Component, type OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'fg-auth-callback',
  styleUrl: './auth-callback.scss',
  templateUrl: './auth-callback.html',
})
export class AuthCallbackPage implements OnInit {
  private readonly router = inject(Router);

  public ngOnInit(): void {
    void this.router.navigate(['/shows']);
  }
}
