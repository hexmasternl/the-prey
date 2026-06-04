import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface UserSettings {
  callsign: string;
  language: string;
}

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly http = inject(HttpClient);

  get(): Observable<UserSettings> {
    return this.http.get<UserSettings>(`${environment.apiUrl}/users/settings`);
  }

  save(settings: UserSettings): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/users/settings`, settings);
  }
}
