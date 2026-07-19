import { buildComponent, makeApiSpy, makeAuthSpy } from './group-test-helpers.spec';
import { throwError } from 'rxjs';

const TOAST_DURATION_MS = 2_000,
  TWO_CALLS = 2;

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
