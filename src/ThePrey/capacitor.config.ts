import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'nl.hexmaster.theprey',
  appName: 'ThePrey',
  webDir: 'www',
  plugins: {
    // Route fetch/XHR through the native HTTP stack instead of the WebView. On-device
    // the WebView origin is https://localhost, so every cross-origin call (Auth0's
    // /oauth/token exchange during handleRedirectCallback, and our own API requests)
    // is otherwise subject to browser CORS and fails with "TypeError: Failed to fetch".
    // Native HTTP is not bound by CORS, which resolves both at once.
    CapacitorHttp: {
      enabled: true,
    },
  },
};

export default config;
