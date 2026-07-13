import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Group, ScheduleResponse, Show, User, UserAvailability, Vote } from '../models';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  getShows() {
    return this.http.get<Show[]>(`${this.base}/api/shows`);
  }

  getVotes() {
    return this.http.get<Vote[]>(`${this.base}/api/votes`);
  }

  saveVotes(votes: Vote[]) {
    return this.http.put<void>(`${this.base}/api/votes`, votes);
  }

  getMyGroup() {
    return this.http.get<Group>(`${this.base}/api/groups/me`);
  }

  createGroup(name: string) {
    return this.http.post<Group>(`${this.base}/api/groups`, { name });
  }

  joinGroup(inviteCode: string) {
    return this.http.post<Group>(`${this.base}/api/groups/join`, { inviteCode });
  }

  getSchedule() {
    return this.http.get<ScheduleResponse>(`${this.base}/api/schedule`);
  }

  getAvailability() {
    return this.http.get<UserAvailability>(`${this.base}/api/availability`);
  }

  saveAvailability(availability: UserAvailability) {
    return this.http.put<void>(`${this.base}/api/availability`, availability);
  }

  getMe() {
    return this.http.get<User>(`${this.base}/api/users/me`);
  }

  upsertMe(displayName: string, email: string) {
    return this.http.put<void>(`${this.base}/api/users/me`, { displayName, email });
  }

  verifyCaptcha(token: string) {
    return this.http.post<void>(`${this.base}/api/captcha/verify`, { token });
  }

  deleteMe() {
    return this.http.delete<void>(`${this.base}/api/users/me`);
  }
}
