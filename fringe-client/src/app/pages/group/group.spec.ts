import {
  buildComponent,
  makeApiSpy,
  makeAuthSpy,
  mockGroup,
} from './group-test-helpers.spec';
import { throwError } from 'rxjs';

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
});

describe('GroupPage createGroup errors', () => {
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
});

describe('GroupPage joinGroup errors', () => {
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
