import { Injectable } from '@angular/core';

const DB_NAME = 'the-prey-db';
const DB_VERSION = 2;

/**
 * Shared IndexedDB connection for the whole app.
 * Version 2 adds the `playfields` object store alongside the existing `user-profile` store.
 * All domain DB services should inject this instead of opening their own connections.
 */
@Injectable({ providedIn: 'root' })
export class AppDbService {
  private dbPromise: Promise<IDBDatabase> | null = null;

  getDb(): Promise<IDBDatabase> {
    this.dbPromise ??= new Promise<IDBDatabase>((resolve, reject) => {
      const req = indexedDB.open(DB_NAME, DB_VERSION);
      req.onupgradeneeded = () => {
        const db = req.result;
        if (!db.objectStoreNames.contains('user-profile')) {
          db.createObjectStore('user-profile', { keyPath: 'key' });
        }
        if (!db.objectStoreNames.contains('playfields')) {
          db.createObjectStore('playfields', { keyPath: 'id' });
        }
      };
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
    return this.dbPromise;
  }
}
