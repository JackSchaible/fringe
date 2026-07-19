import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import type { User } from '../models';
import { environment } from '../../environments/environment';

const DEV_USER_KEY = 'fringe_dev_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  public readonly currentUser = signal<User | null>(null);
  public readonly devMode = !environment.cognitoUserPoolId;

  private readonly router = inject(Router);

  // ── Email OTP (production) ───────────────────────────────────────────────
  public static async sendOtp(email: string): Promise<void> {
    const { signUp, signIn, signOut } = await import('aws-amplify/auth');
    try {
      await signOut();
    } catch {
      /* Ignore stale session */
    }
    try {
      await signUp({
        options: { userAttributes: { email } },
        password: crypto.randomUUID(),
        username: email,
      });
    } catch (error: unknown) {
      if (error instanceof Error && error.name !== 'UsernameExistsException') {
        throw error;
      }
    }
    await signIn({
      options: { authFlowType: 'CUSTOM_WITHOUT_SRP' },
      username: email,
    });
  }

  public static getDevUserId(): string | null {
    return localStorage.getItem(DEV_USER_KEY);
  }

  public async confirmOtp(code: string): Promise<void> {
    const { confirmSignIn } = await import('aws-amplify/auth'),
      { isSignedIn } = await confirmSignIn({ challengeResponse: code.trim() });
    if (!isSignedIn) {
      throw new Error('Incorrect code — please try again.');
    }
    await this.loadUserFromCognito();
  }

  public async loadUserFromCognito(): Promise<void> {
    try {
      const { fetchUserAttributes, getCurrentUser } =
          await import('aws-amplify/auth'),
        attrs = await fetchUserAttributes(),
        cognitoUser = await getCurrentUser();

      this.currentUser.set({
        displayName: attrs.name ?? attrs.email ?? cognitoUser.username,
        email: attrs.email ?? '',
        groupId: null,
        userId: cognitoUser.userId,
      });
    } catch {
      this.currentUser.set(null);
    }
  }

  public async getToken(): Promise<string | null> {
    if (this.devMode) {
      return null;
    }
    try {
      const { fetchAuthSession } = await import('aws-amplify/auth'),
        session = await fetchAuthSession();
      return session.tokens?.accessToken.toString() ?? null;
    } catch {
      return null;
    }
  }

  public async signOut(): Promise<void> {
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

  public async isAuthenticated(): Promise<boolean> {
    if (this.devMode) {
      return Boolean(AuthService.getDevUserId());
    }
    return (await this.getToken()) !== null;
  }

  public clearSession(): void {
    if (this.devMode) {
      localStorage.removeItem(DEV_USER_KEY);
    }
    this.currentUser.set(null);
  }

  // ── Dev mode ─────────────────────────────────────────────────────────────

  public signInDev(userId: string): void {
    localStorage.setItem(DEV_USER_KEY, userId);
    this.currentUser.set({
      displayName: userId,
      email: `${userId}@dev`,
      groupId: null,
      userId,
    });
  }

  public initDevSession(): void {
    const id = AuthService.getDevUserId();
    if (id !== null) {
      this.currentUser.set({
        displayName: id,
        email: `${id}@dev`,
        groupId: null,
        userId: id,
      });
    }
  }
}
