import { App } from '@capacitor/app';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { environment } from '../../environments/environment';

/**
 * The real native app version (the GitVersion semVer baked into the bundle) when running on a
 * native platform, falling back to the build-time `environment.version` on the web or if the
 * native call fails. Used to feed the server-side version gate.
 */
export async function getAppVersion(): Promise<string> {
  if (!Capacitor.isNativePlatform()) {
    return environment.version;
  }
  try {
    const info = await App.getInfo();
    return info.version || environment.version;
  } catch {
    return environment.version;
  }
}

/**
 * Open the app's Play Store listing from the "update required" banner. Uses Capacitor's
 * in-app browser on native platforms and falls back to a plain navigation on the web.
 */
export async function openPlayStore(): Promise<void> {
  const url = environment.playStoreUrl;
  if (Capacitor.isNativePlatform()) {
    await Browser.open({ url });
  } else {
    window.open(url, '_blank');
  }
}
