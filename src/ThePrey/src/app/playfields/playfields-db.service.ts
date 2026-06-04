import { inject, Injectable } from '@angular/core';
import { AppDbService } from '../db/app-db.service';
import { PlayFieldRecord } from './playfield.model';

const STORE = 'playfields';

@Injectable({ providedIn: 'root' })
export class PlayfieldsDbService {
  private readonly appDb = inject(AppDbService);

  async getAll(ownerId: string): Promise<PlayFieldRecord[]> {
    const db = await this.appDb.getDb();
    const all = await idbGetAll<PlayFieldRecord>(db, STORE);
    return all.filter((r) => r.ownerId === ownerId);
  }

  /**
   * Insert or update a playfield using server-wins conflict resolution.
   * The record is only written if no local copy exists or the incoming
   * `lastUpdatedOn` is strictly newer than the stored value.
   */
  async upsert(record: PlayFieldRecord): Promise<void> {
    const db = await this.appDb.getDb();
    const existing = await idbGet<PlayFieldRecord>(db, STORE, record.id);
    if (existing && new Date(existing.lastUpdatedOn) >= new Date(record.lastUpdatedOn)) {
      return;
    }
    await idbPut(db, STORE, record);
  }

  async getUnsynced(): Promise<PlayFieldRecord[]> {
    const db = await this.appDb.getDb();
    const all = await idbGetAll<PlayFieldRecord>(db, STORE);
    return all.filter((r) => !r.isSynced);
  }

  async markSynced(id: string): Promise<void> {
    const db = await this.appDb.getDb();
    const record = await idbGet<PlayFieldRecord>(db, STORE, id);
    if (record) {
      await idbPut(db, STORE, { ...record, isSynced: true });
    }
  }

  async delete(id: string): Promise<void> {
    const db = await this.appDb.getDb();
    await idbDelete(db, STORE, id);
  }
}

function idbGet<T>(db: IDBDatabase, store: string, key: string): Promise<T | null> {
  return new Promise((resolve, reject) => {
    const req = db.transaction(store, 'readonly').objectStore(store).get(key);
    req.onsuccess = () => resolve((req.result ?? null) as T | null);
    req.onerror = () => reject(req.error);
  });
}

function idbGetAll<T>(db: IDBDatabase, store: string): Promise<T[]> {
  return new Promise((resolve, reject) => {
    const req = db.transaction(store, 'readonly').objectStore(store).getAll();
    req.onsuccess = () => resolve((req.result ?? []) as T[]);
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
