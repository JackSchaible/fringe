import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'fg-auth-callback',
  template: `<div style="display:flex;justify-content:center;align-items:center;height:100vh;color:#94a3b8">Redirecting…</div>`,
})
export class AuthCallbackPage implements OnInit {
  private readonly router = inject(Router);

  ngOnInit(): void {
    void this.router.navigate(['/shows']);
  }
}
