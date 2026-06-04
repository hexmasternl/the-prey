import { inject, Injectable } from '@angular/core';
import { AppDbService } from '../db/app-db.service';
import { UserProfile } from './user.model';

const STORE = 'user-profile';
const RECORD_KEY = 'current';

/**
 * Thin IndexedDB wrapper that persists the local UserProfile so the home menu
 * can appear immediately on the next launch without waiting for the server.
 */
@Injectable({ providedIn: 'root' })
export class UserDbService {
  private readonly appDb = inject(AppDbService);

  async getProfile(): Promise<UserProfile | null> {
    try {
      const db = await this.appDb.getDb();
      return await idbGet<UserProfile>(db, STORE, RECORD_KEY);
    } catch {
      return null;
    }
  }

  async saveProfile(profile: UserProfile): Promise<void> {
    try {
      const db = await this.appDb.getDb();
      await idbPut(db, STORE, { key: RECORD_KEY, ...profile });
    } catch {
      // Persist failure is non-fatal — in-memory state is still fresh
    }
  }

  async clearProfile(): Promise<void> {
    try {
      const db = await this.appDb.getDb();
      await idbDelete(db, STORE, RECORD_KEY);
    } catch {
      // Ignore — the profile will be missing on next load, triggering a server sync
    }
  }
}

function idbGet<T>(db: IDBDatabase, store: string, key: string): Promise<T | null> {
  return new Promise((resolve, reject) => {
    const req = db.transaction(store, 'readonly').objectStore(store).get(key);
    req.onsuccess = () => resolve((req.result ?? null) as T | null);
    req.onerror = () => reject(req.error);
  });
}

function idbPut(db: IDBDatabase, store: string, value: object): Promise<void> {
  return new Promise((resolve, reject) => {
    const req = db.transaction(store, 'readwrite').objectStore(store).put(value);
    req.onsuccess = () => resolve();
    req.onerror = () => reject(req.error);
  });
}

function idbDelete(db: IDBDatabase, store: string, key: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const req = db.transaction(store, 'readwrite').objectStore(store).delete(key);
    req.onsuccess = () => resolve();
    req.onerror = () => reject(req.error);
  });
}
