import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { IdToken } from '@auth0/auth0-angular';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../environments/environment';
import { LanguageService } from '../i18n/language.service';
import { UserDbService } from './user-db.service';
import { CreateUserRequest, UserDto, UserProfile } from './user.model';

/**
 * Single source of truth for the authenticated user's profile.
 *
 * Lifecycle:
 *   1. init(claims) → restores from IndexedDB immediately (menu appears fast)
 *   2. POSTs to /users in background → updates signal + IndexedDB with fresh data
 *   3. clear() on logout → erases both in-memory state and IndexedDB
 */
@Injectable({ providedIn: 'root' })
export class UserStateService {
  private readonly http = inject(HttpClient);
  private readonly db = inject(UserDbService);
  private readonly language = inject(LanguageService);

  private readonly _profile = signal<UserProfile | null>(null);
  readonly profile = this._profile.asReadonly();

  readonly isSyncing = signal(false);
  /** True only when the server call failed AND no cached profile exists. */
  readonly syncFailed = signal(false);

  /**
   * Restore the cached profile (fast) then sync with the server in background.
   * Safe to call multiple times — subsequent calls re-trigger a server sync.
   */
  async init(claims: IdToken): Promise<void> {
    // Immediately surface the cached profile so the menu can render
    const cached = await this.db.getProfile();
    if (cached && !this._profile()) {
      this._profile.set(cached);
    }

    // Always sync to create or update the server-side record
    this.syncFailed.set(false);
    this.syncWithServer(claims);
  }

  /** Remove the profile from memory and IndexedDB (call before Auth0 logout). */
  async clear(): Promise<void> {
    this._profile.set(null);
    await this.db.clearProfile();
  }

  /** Apply a fresh server DTO to both the in-memory signal and the IndexedDB cache. */
  async applyServerUser(dto: UserDto): Promise<void> {
    const profile: UserProfile = {
      userId: dto.userId,
      callsign: dto.callsign,
      preferredLanguage: dto.preferredLanguage,
    };
    this._profile.set(profile);
    await this.db.saveProfile(profile);
  }

  private syncWithServer(claims: IdToken): void {
    this.isSyncing.set(true);

    const request: CreateUserRequest = {
      firstName: claims['given_name'] as string | undefined,
      lastName: claims['family_name'] as string | undefined,
      emailAddress: (claims['email'] as string) ?? '',
      isEmailVerified: (claims['email_verified'] as boolean) ?? false,
      preferredLanguage: this.language.current,
    };

    firstValueFrom(
      this.http.post<UserDto>(`${environment.apiUrl}/users`, request),
    )
      .then(dto => this.applyServerUser(dto))
      .catch(() => {
        // If there is no cached profile to fall back on, surface the error to the UI
        if (!this._profile()) {
          this.syncFailed.set(true);
        }
      })
      .finally(() => {
        this.isSyncing.set(false);
      });
  }
}
