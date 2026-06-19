import { Injectable } from '@angular/core';
import { Preferences } from '@capacitor/preferences';

/** The two in-game tours, tracked independently so each role's tour shows once. */
export type TourRole = 'hunter' | 'prey';

const TOUR_KEYS: Record<TourRole, string> = {
  hunter: 'tour.seen.hunter',
  prey: 'tour.seen.prey',
};

/**
 * Per-device record of which in-game tours a player has already been shown. Backed by
 * `@capacitor/preferences` with one key per role, so the hunter and prey tours are
 * independent settings. All failures are swallowed and treated as "not seen" so a storage
 * problem never blocks play (at worst a tour shows again next session).
 */
@Injectable({ providedIn: 'root' })
export class TourService {
  async hasSeen(role: TourRole): Promise<boolean> {
    try {
      const { value } = await Preferences.get({ key: TOUR_KEYS[role] });
      return value === 'true';
    } catch {
      // Read failed — treat as not seen so the tour is still offered once.
      return false;
    }
  }

  async markSeen(role: TourRole): Promise<void> {
    try {
      await Preferences.set({ key: TOUR_KEYS[role], value: 'true' });
    } catch {
      // Never block play on a write failure; the tour simply may reappear next session.
    }
  }
}
