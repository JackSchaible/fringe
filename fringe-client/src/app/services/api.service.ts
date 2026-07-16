import type {
  AlternateProposal,
  Group,
  MissedShow,
  ScheduleItem,
  ScheduleResponse,
  Show,
  User,
  UserAvailability,
  Vote,
} from '../models';
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import type { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { map } from 'rxjs/operators';

const EMPTY_COUNT = 0;

const isObject = (objectIn: unknown): objectIn is Record<string, unknown> =>
  typeof objectIn === 'object' && objectIn !== null && !Array.isArray(objectIn);

const isScheduleItem = (item: unknown): item is ScheduleItem =>
  isObject(item) &&
  isObject(item['show']) &&
  typeof item['showTime'] === 'string';

const isMissedShow = (show: unknown): show is MissedShow =>
  isObject(show) &&
  isObject(show['show']) &&
  typeof show['conflictsWithScheduled'] === 'boolean';

const getItems = (
  raw: Readonly<Record<string, unknown>>,
): Array<ScheduleItem> => {
  if (Array.isArray(raw['items'])) {
    return raw['items'].filter(isScheduleItem);
  }
  return [];
};

const getProposalDescription = (
  proposal: Readonly<Record<string, unknown>>,
): string => {
  if (typeof proposal['description'] === 'string') {
    return proposal['description'];
  }
  return '';
};

const getExcludedMemberName = (
  proposal: Readonly<Record<string, unknown>>,
): string => {
  if (typeof proposal['excludedMemberName'] === 'string') {
    return proposal['excludedMemberName'];
  }
  return '';
};

const getItemsFromProposal = (
  proposal: Readonly<Record<string, unknown>>,
): Array<ScheduleItem> => {
  if (Array.isArray(proposal['items'])) {
    return proposal['items'].filter(isScheduleItem);
  }
  return [];
};

const getAlternateProposals = (
  raw: Readonly<Record<string, unknown>>,
): Array<AlternateProposal> => {
  if (Array.isArray(raw['alternateProposals'])) {
    return raw['alternateProposals']
      .filter(
        (proposal) =>
          isObject(proposal) &&
          typeof proposal['excludedMemberName'] === 'string',
      )
      .map((proposal: unknown) => {
        if (!isObject(proposal) || !Array.isArray(proposal['items'])) {
          return {
            description: '',
            excludedMemberName: '',
            items: [],
          };
        }

        return {
          description: getProposalDescription(proposal),
          excludedMemberName: getExcludedMemberName(proposal),
          items: getItemsFromProposal(proposal),
        };
      });
  }

  return [];
};

const getMissedShows = (
  raw: Readonly<Record<string, unknown>>,
): Array<MissedShow> => {
  if (Array.isArray(raw['missedShows'])) {
    return raw['missedShows'].filter(isMissedShow);
  }
  return [];
};

const getHasVotes = (
  raw: Readonly<Record<string, unknown>>,
  items: ReadonlyArray<ScheduleItem>,
): boolean => {
  if (typeof raw['hasVotes'] === 'boolean') {
    return raw['hasVotes'];
  }
  return items.length > EMPTY_COUNT;
};

const parseScheduleResponse = (raw: unknown): ScheduleResponse => {
  // Old backend returned a plain Array<ScheduleItem> — treat it as a partial result with no metadata.
  if (Array.isArray(raw)) {
    const items = raw.filter(isScheduleItem);
    return {
      alternateProposals: [],
      hasVotes: items.length > EMPTY_COUNT,
      items,
      missedShows: [],
    };
  }

  if (!isObject(raw)) {
    return {
      alternateProposals: [],
      hasVotes: false,
      items: [],
      missedShows: [],
    };
  }

  const items: Array<ScheduleItem> = getItems(raw),
    alternateProposals: Array<AlternateProposal> = getAlternateProposals(raw),
    missedShows: Array<MissedShow> = getMissedShows(raw),
    hasVotes = getHasVotes(raw, items);

  return { alternateProposals, hasVotes, items, missedShows };
};

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  public getShows(): Observable<Array<Show>> {
    return this.http.get<Array<Show>>(`${this.base}/api/shows`);
  }

  public getVotes(): Observable<Array<Vote>> {
    return this.http.get<Array<Vote>>(`${this.base}/api/votes`);
  }

  public saveVotes(votes: ReadonlyArray<Vote>): Observable<unknown> {
    return this.http.put(`${this.base}/api/votes`, votes);
  }

  public getMyGroup(): Observable<Group> {
    return this.http.get<Group>(`${this.base}/api/groups/me`);
  }

  public createGroup(name: string): Observable<Group> {
    return this.http.post<Group>(`${this.base}/api/groups`, { name });
  }

  public joinGroup(inviteCode: string): Observable<Group> {
    return this.http.post<Group>(`${this.base}/api/groups/join`, {
      inviteCode,
    });
  }

  public getSchedule(): Observable<ScheduleResponse> {
    return this.http
      .get<unknown>(`${this.base}/api/schedule`)
      .pipe(map(parseScheduleResponse));
  }

  public getAvailability(): Observable<UserAvailability> {
    return this.http.get<UserAvailability>(`${this.base}/api/availability`);
  }

  public saveAvailability(
    availability: Readonly<UserAvailability>,
  ): Observable<unknown> {
    return this.http.put(`${this.base}/api/availability`, availability);
  }

  public getMe(): Observable<User> {
    return this.http.get<User>(`${this.base}/api/users/me`);
  }

  public upsertMe(displayName: string, email: string): Observable<unknown> {
    return this.http.put(`${this.base}/api/users/me`, {
      displayName,
      email,
    });
  }

  public updateDisplayName(displayName: string): Observable<unknown> {
    return this.http.put(`${this.base}/api/users/me/display-name`, {
      displayName,
    });
  }

  public verifyCaptcha(token: string): Observable<unknown> {
    return this.http.post(`${this.base}/api/captcha/verify`, { token });
  }

  public deleteMe(): Observable<unknown> {
    return this.http.delete(`${this.base}/api/users/me`);
  }
}
