import {
  type HttpErrorResponse,
  provideHttpClient,
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ApiService } from './api.service';
import { TestBed } from '@angular/core/testing';
import type { User } from '../models';
import { environment } from '../../environments/environment';
import { provideZonelessChangeDetection } from '@angular/core';

const BASE = environment.apiUrl,
  mockUser: User = {
    displayName: 'Tester',
    email: 'test@example.com',
    groupId: null,
    userId: 'u1',
  },
  setup = (): { httpMock: HttpTestingController; service: ApiService } => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    return {
      httpMock: TestBed.inject(HttpTestingController),
      service: TestBed.inject(ApiService),
    };
  };

describe('ApiService getMe', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('returns user', () => {
    const { httpMock, service } = setup();
    service.getMe().subscribe((user) => {
      expect(user).toEqual(mockUser);
    });
    httpMock.expectOne(`${BASE}/api/users/me`).flush(mockUser);
  });
});

describe('ApiService upsertMe', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('sends PUT with displayName and email', () => {
    const { httpMock, service } = setup();
    service.upsertMe('Tester', 'test@example.com').subscribe();
    const req = httpMock.expectOne(`${BASE}/api/users/me`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      displayName: 'Tester',
      email: 'test@example.com',
    });
    req.flush(null);
  });
});

describe('ApiService updateDisplayName', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('sends PUT with displayName', () => {
    const { httpMock, service } = setup();
    service.updateDisplayName('NewName').subscribe();
    const req = httpMock.expectOne(`${BASE}/api/users/me/display-name`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ displayName: 'NewName' });
    req.flush(null);
  });
});

describe('ApiService verifyCaptcha', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('sends POST with token', () => {
    const { httpMock, service } = setup();
    service.verifyCaptcha('my-token').subscribe();
    const req = httpMock.expectOne(`${BASE}/api/captcha/verify`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ token: 'my-token' });
    req.flush(null);
  });

  it('propagates error', () => {
    const { httpMock, service } = setup();
    service.verifyCaptcha('bad-token').subscribe({
      error: (error: HttpErrorResponse) => {
        expect(error).toBeTruthy();
      },
    });
    httpMock
      .expectOne(`${BASE}/api/captcha/verify`)
      .flush('Invalid', { status: 400, statusText: 'Bad Request' });
  });
});

describe('ApiService deleteMe', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('sends DELETE to /api/users/me', () => {
    const { httpMock, service } = setup();
    let completed = false;
    service.deleteMe().subscribe({
      complete: () => {
        completed = true;
      },
    });
    const req = httpMock.expectOne(`${BASE}/api/users/me`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    expect(completed).toBeTrue();
  });

  it('propagates error', () => {
    const { httpMock, service } = setup();
    service.deleteMe().subscribe({
      error: (error: HttpErrorResponse) => {
        expect(error).toBeTruthy();
      },
    });
    httpMock
      .expectOne(`${BASE}/api/users/me`)
      .flush('Error', { status: 500, statusText: 'Internal Server Error' });
  });
});
