import { Capacitor } from '@capacitor/core';
import { environment } from '../environments/environment';

const APP_ID = 'nl.hexmaster.theprey';

/**
 * Base of every custom-scheme deep link the OS routes back into the app
 * (registered via @string/custom_url_scheme in the Android manifest). Both the
 * Auth0 callback and game-join links hang off this base.
 */
const nativeDeepLinkBase = `${APP_ID}://${environment.auth0.domain}/capacitor/${APP_ID}`;

export const nativeCallbackUri = `${nativeDeepLinkBase}/callback`;

/** Deep link that opens the app on the join screen for a specific game. */
export function nativeGameJoinUri(gameId: string): string {
  return `${nativeDeepLinkBase}/games/join/${gameId}`;
}

/** Returns the correct OAuth redirect/return URI for the current runtime context. */
export function getCallbackUri(): string {
  return Capacitor.isNativePlatform() ? nativeCallbackUri : window.location.origin;
}
