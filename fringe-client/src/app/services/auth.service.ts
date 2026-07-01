import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { User } from '../models';

const DEV_USER_KEY = 'fringe_dev_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly currentUser = signal<User | null>(null);
  readonly devMode = !environment.cognitoUserPoolId;

  private readonly router = inject(Router);

  // ── Email OTP (production) ───────────────────────────────────────────────

  async sendOtp(email: string): Promise<void> {
    const { signUp, signIn, signOut } = await import('aws-amplify/auth');
    try { await signOut(); } catch { /* ignore stale session */ }
    try {
      await signUp({
        username: email,
        password: crypto.randomUUID(),
        options: { userAttributes: { email } },
      });
    } catch (e: unknown) {
      if ((e as { name?: string }).name !== 'UsernameExistsException') throw e;
    }
    await signIn({ username: email, options: { authFlowType: 'CUSTOM_WITHOUT_SRP' } });
  }

  async confirmOtp(code: string): Promise<void> {
    const { confirmSignIn } = await import('aws-amplify/auth');
    const { isSignedIn } = await confirmSignIn({ challengeResponse: code.trim() });
    if (!isSignedIn) throw new Error('Incorrect code — please try again.');
    await this.loadUserFromCognito();
  }

  async loadUserFromCognito(): Promise<void> {
    try {
      const { fetchUserAttributes, getCurrentUser } = await import('aws-amplify/auth');
      const cognitoUser = await getCurrentUser();
      const attrs = await fetchUserAttributes();
      this.currentUser.set({
        userId: cognitoUser.userId,
        email: attrs.email ?? '',
        displayName: attrs.name ?? attrs.email ?? cognitoUser.username,
        groupId: undefined,
      });
    } catch {
      this.currentUser.set(null);
    }
  }

  async getToken(): Promise<string | null> {
    if (this.devMode) return null;
    try {
      const { fetchAuthSession } = await import('aws-amplify/auth');
      const session = await fetchAuthSession();
      return session.tokens?.accessToken.toString() ?? null;
    } catch {
      return null;
    }
  }

  async signOut(): Promise<void> {
    if (this.devMode) {
      localStorage.removeItem(DEV_USER_KEY);
      this.currentUser.set(null);
      await this.router.navigate(['/login']);
      return;
    }
    const { signOut } = await import('aws-amplify/auth');
    await signOut();
    this.currentUser.set(null);
    await this.router.navigate(['/login']);
  }

  async isAuthenticated(): Promise<boolean> {
    if (this.devMode) {
      return !!this.getDevUserId();
    }
    return (await this.getToken()) !== null;
  }

  clearSession(): void {
    if (this.devMode) {
      localStorage.removeItem(DEV_USER_KEY);
    }
    this.currentUser.set(null);
  }

  // ── Dev mode ─────────────────────────────────────────────────────────────

  getDevUserId(): string | null {
    return localStorage.getItem(DEV_USER_KEY);
  }

  signInDev(userId: string): void {
    localStorage.setItem(DEV_USER_KEY, userId);
    this.currentUser.set({ userId, email: `${userId}@dev`, displayName: userId });
  }

  initDevSession(): void {
    const id = this.getDevUserId();
    if (id) {
      this.currentUser.set({ userId: id, email: `${id}@dev`, displayName: id });
    }
  }
}
