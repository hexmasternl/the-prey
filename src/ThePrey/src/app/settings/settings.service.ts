import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { UserDto } from '../users/user.model';

export interface UserSettings {
  callsign: string;
  language: string;
}

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly http = inject(HttpClient);

  get(): Observable<UserDto> {
    return this.http.get<UserDto>(`${environment.apiUrl}/users/me`);
  }

  save(settings: UserSettings): Observable<UserDto> {
    return this.http.put<UserDto>(`${environment.apiUrl}/users/settings`, {
      callsign: settings.callsign,
      preferredLanguage: settings.language,
    });
  }
}
