import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { Group, User } from '../../models';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { GroupPage } from './group';
import { provideZonelessChangeDetection } from '@angular/core';

export const mockGroup: Group = {
    groupId: 'g1',
    inviteCode: 'ABC123',
    members: [
      { displayName: 'Alice', email: 'a@b.com', userId: 'u1', voteCount: 2 },
    ],
    name: 'Fringe Squad',
  },
  mockUser: User = {
    displayName: 'Alice',
    email: 'a@b.com',
    groupId: null,
    userId: 'u1',
  },
  makeApiSpy = (
    groupResult: 'success' | 'error' = 'success',
  ): jasmine.SpyObj<ApiService> => {
    const spy = jasmine.createSpyObj<ApiService>('ApiService', [
      'getMyGroup',
      'getMe',
      'createGroup',
      'joinGroup',
      'updateDisplayName',
      'deleteMe',
    ]);
    if (groupResult === 'success') {
      spy.getMyGroup.and.returnValue(of(mockGroup));
    } else {
      spy.getMyGroup.and.returnValue(throwError(() => new Error('no group')));
    }
    spy.getMe.and.returnValue(of(mockUser));
    spy.createGroup.and.returnValue(of(mockGroup));
    spy.joinGroup.and.returnValue(of(mockGroup));
    spy.updateDisplayName.and.returnValue(of(undefined));
    spy.deleteMe.and.returnValue(of(undefined));
    return spy;
  },
  makeAuthSpy = (): jasmine.SpyObj<AuthService> => {
    const spy = jasmine.createSpyObj<AuthService>('AuthService', ['signOut']);
    spy.signOut.and.returnValue(Promise.resolve());
    return spy;
  },
  buildComponent = async (
    api: jasmine.SpyObj<ApiService>,
    auth: jasmine.SpyObj<AuthService>,
  ): Promise<{
    component: GroupPage;
    fixture: ComponentFixture<GroupPage>;
  }> => {
    TestBed.configureTestingModule({
      imports: [GroupPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: ApiService, useValue: api },
        { provide: AuthService, useValue: auth },
      ],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(GroupPage),
      component = fixture.componentInstance;
    fixture.detectChanges();
    return { component, fixture };
  };
