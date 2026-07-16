import {
  buildComponent,
  makeApiSpy,
  makeAuthSpy,
  mockGroup,
} from './group-test-helpers.spec';
import { throwError } from 'rxjs';

const TOAST_DURATION_MS = 2_000,
  TWO_CALLS = 2;

describe('GroupPage ngOnInit success', () => {
  it('calls getMyGroup on init', async () => {
    const apiSpy = makeApiSpy('success');
    await buildComponent(apiSpy, makeAuthSpy());
    expect(apiSpy.getMyGroup).toHaveBeenCalled();
  });

  it('sets myGroup signal on success', async () => {
    const { component } = await buildComponent(
      makeApiSpy('success'),
      makeAuthSpy(),
    );
    expect(component.myGroup()).toEqual(mockGroup);
  });

  it('sets loading to false', async () => {
    const { component } = await buildComponent(
      makeApiSpy('success'),
      makeAuthSpy(),
    );
    expect(component.loading()).toBeFalse();
  });

  it('calls getMe on init', async () => {
    const apiSpy = makeApiSpy('success');
    await buildComponent(apiSpy, makeAuthSpy());
    expect(apiSpy.getMe).toHaveBeenCalled();
  });

  it('sets username signal from getMe response', async () => {
    const { component } = await buildComponent(
      makeApiSpy('success'),
      makeAuthSpy(),
    );
    expect(component.username()).toBe('Alice');
  });
});

describe('GroupPage ngOnInit getMyGroup error', () => {
  it('sets loading to false on error', async () => {
    const { component } = await buildComponent(
      makeApiSpy('error'),
      makeAuthSpy(),
    );
    expect(component.loading()).toBeFalse();
  });

  it('leaves myGroup as null', async () => {
    const { component } = await buildComponent(
      makeApiSpy('error'),
      makeAuthSpy(),
    );
    expect(component.myGroup()).toBeNull();
  });
});

describe('GroupPage createGroup', () => {
  it('calls api.createGroup with trimmed name', async () => {
    const apiSpy = makeApiSpy('error'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    component.groupName.set('  My Group  ');
    component.createGroup();
    expect(apiSpy.createGroup).toHaveBeenCalledWith('My Group');
  });

  it('sets myGroup on success', async () => {
    const { component } = await buildComponent(
      makeApiSpy('error'),
      makeAuthSpy(),
    );
    component.groupName.set('My Group');
    component.createGroup();
    expect(component.myGroup()).toEqual(mockGroup);
  });

  it('does nothing when groupName is empty', async () => {
    const apiSpy = makeApiSpy('error'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    component.groupName.set('   ');
    component.createGroup();
    expect(apiSpy.createGroup).not.toHaveBeenCalled();
  });

  it('clears error before calling API', async () => {
    const { component } = await buildComponent(
      makeApiSpy('error'),
      makeAuthSpy(),
    );
    component.error.set('previous error');
    component.groupName.set('Test');
    component.createGroup();
    expect(component.error()).toBe('');
  });

  it('sets error signal on API failure', async () => {
    const apiSpy = makeApiSpy('error'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    apiSpy.createGroup.and.returnValue(
      throwError(() => ({ error: 'Name taken' })),
    );
    component.groupName.set('Taken Name');
    component.createGroup();
    expect(component.error()).toBe('Name taken');
  });

  it('sets fallback error message when error.error is absent', async () => {
    const apiSpy = makeApiSpy('error'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    apiSpy.createGroup.and.returnValue(throwError(() => ({})));
    component.groupName.set('Test');
    component.createGroup();
    expect(component.error()).toBe('Failed to create group');
  });
});

describe('GroupPage joinGroup', () => {
  it('calls api.joinGroup with uppercased trimmed code', async () => {
    const apiSpy = makeApiSpy('error'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    component.inviteCode.set('  abc123  ');
    component.joinGroup();
    expect(apiSpy.joinGroup).toHaveBeenCalledWith('ABC123');
  });

  it('sets myGroup on success', async () => {
    const { component } = await buildComponent(
      makeApiSpy('error'),
      makeAuthSpy(),
    );
    component.inviteCode.set('ABC123');
    component.joinGroup();
    expect(component.myGroup()).toEqual(mockGroup);
  });

  it('does nothing when inviteCode is empty', async () => {
    const apiSpy = makeApiSpy('error'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    component.inviteCode.set('   ');
    component.joinGroup();
    expect(apiSpy.joinGroup).not.toHaveBeenCalled();
  });

  it('clears error before calling API', async () => {
    const { component } = await buildComponent(
      makeApiSpy('error'),
      makeAuthSpy(),
    );
    component.error.set('old error');
    component.inviteCode.set('CODE1');
    component.joinGroup();
    expect(component.error()).toBe('');
  });

  it('sets error on failure', async () => {
    const apiSpy = makeApiSpy('error'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    apiSpy.joinGroup.and.returnValue(
      throwError(() => ({ error: 'Invalid invite code' })),
    );
    component.inviteCode.set('INVALID');
    component.joinGroup();
    expect(component.error()).toBe('Invalid invite code');
  });

  it('sets fallback error when error.error is absent', async () => {
    const apiSpy = makeApiSpy('error'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    apiSpy.joinGroup.and.returnValue(throwError(() => ({})));
    component.inviteCode.set('NOPE');
    component.joinGroup();
    expect(component.error()).toBe('Invalid invite code');
  });
});

describe('GroupPage copyCode', () => {
  it('calls clipboard.writeText with invite code', async () => {
    const { component } = await buildComponent(
      makeApiSpy('success'),
      makeAuthSpy(),
    );
    spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
    component.copyCode();
    await Promise.resolve();
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('ABC123');
  });

  it('sets copied to true after clipboard write', async () => {
    const { component } = await buildComponent(
      makeApiSpy('success'),
      makeAuthSpy(),
    );
    spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
    component.copyCode();
    await Promise.resolve();
    expect(component.copied()).toBeTrue();
  });

  it('resets copied to false after 2000ms', async () => {
    const { component } = await buildComponent(
      makeApiSpy('success'),
      makeAuthSpy(),
    );
    spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
    jasmine.clock().install();
    component.copyCode();
    await Promise.resolve();
    jasmine.clock().tick(TOAST_DURATION_MS);
    expect(component.copied()).toBeFalse();
    jasmine.clock().uninstall();
  });

  it('does nothing when myGroup is null', async () => {
    const { component } = await buildComponent(
      makeApiSpy('success'),
      makeAuthSpy(),
    );
    spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
    component.myGroup.set(null);
    component.copyCode();
    await Promise.resolve();
    expect(navigator.clipboard.writeText).not.toHaveBeenCalled();
  });
});

describe('GroupPage saveUsername', () => {
  it('does nothing when username is empty', async () => {
    const apiSpy = makeApiSpy('success'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    component.username.set('   ');
    component.saveUsername();
    expect(apiSpy.updateDisplayName).not.toHaveBeenCalled();
  });

  it('calls updateDisplayName with trimmed name', async () => {
    const apiSpy = makeApiSpy('success'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    component.username.set('  Bob  ');
    component.saveUsername();
    expect(apiSpy.updateDisplayName).toHaveBeenCalledWith('Bob');
  });

  it('sets savingUsername to false after observable completes', async () => {
    const { component } = await buildComponent(
      makeApiSpy('success'),
      makeAuthSpy(),
    );
    component.username.set('Bob');
    component.saveUsername();
    expect(component.savingUsername()).toBeFalse();
  });

  it('sets usernameSaved to true on success', async () => {
    const { component } = await buildComponent(
      makeApiSpy('success'),
      makeAuthSpy(),
    );
    component.username.set('Bob');
    component.saveUsername();
    expect(component.usernameSaved()).toBeTrue();
  });

  it('resets usernameSaved after 2000ms', async () => {
    const { component } = await buildComponent(
      makeApiSpy('success'),
      makeAuthSpy(),
    );
    jasmine.clock().install();
    component.username.set('Bob');
    component.saveUsername();
    jasmine.clock().tick(TOAST_DURATION_MS);
    expect(component.usernameSaved()).toBeFalse();
    jasmine.clock().uninstall();
  });
});

describe('GroupPage saveUsername refresh and errors', () => {
  it('refreshes group after saving username (called once in ngOnInit, once after save)', async () => {
    const apiSpy = makeApiSpy('success'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    component.username.set('Bob');
    component.saveUsername();
    expect(apiSpy.getMyGroup).toHaveBeenCalledTimes(TWO_CALLS);
  });

  it('resets savingUsername on error', async () => {
    const apiSpy = makeApiSpy('success'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    apiSpy.updateDisplayName.and.returnValue(
      throwError(() => new Error('Error')),
    );
    component.username.set('Bob');
    component.saveUsername();
    expect(component.savingUsername()).toBeFalse();
  });
});

describe('GroupPage deleteAccount', () => {
  it('calls api.deleteMe', async () => {
    const apiSpy = makeApiSpy('success'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    component.deleteAccount();
    expect(apiSpy.deleteMe).toHaveBeenCalled();
  });

  it('calls auth.signOut after successful delete', async () => {
    const authSpy = makeAuthSpy(),
      { component } = await buildComponent(makeApiSpy('success'), authSpy);
    component.deleteAccount();
    expect(authSpy.signOut).toHaveBeenCalled();
  });

  it('sets error and resets flags on failure', async () => {
    const apiSpy = makeApiSpy('success'),
      { component } = await buildComponent(apiSpy, makeAuthSpy());
    apiSpy.deleteMe.and.returnValue(
      throwError(() => new Error('server error')),
    );
    component.deleteAccount();
    expect(component.deleting()).toBeFalse();
    expect(component.confirmingDelete()).toBeFalse();
    expect(component.error()).toBe(
      'Failed to delete account. Please try again.',
    );
  });
});
