import { Capacitor } from '@capacitor/core';
import { environment } from '../environments/environment';

const APP_ID = 'nl.hexmaster.theprey';

export const nativeCallbackUri = `${APP_ID}://${environment.auth0.domain}/capacitor/${APP_ID}/callback`;

/** Returns the correct OAuth redirect/return URI for the current runtime context. */
export function getCallbackUri(): string {
  return Capacitor.isNativePlatform() ? nativeCallbackUri : window.location.origin;
}
