export const environment = {
  production: false,
  // Fallback version shown on platforms where Capacitor's App.getInfo() is not
  // implemented (e.g. the browser/dev server). On native builds the real version
  // (GitVersion semVer baked into the bundle's versionName) is read at runtime.
  version: '0.0.1',
  apiUrl:
    'https://gateway.jollyfield-ab1afcde.westeurope.azurecontainerapps.io',
  // Play Store listing opened from the "update required" banner when the version gate blocks.
  playStoreUrl:
    'https://play.google.com/store/apps/details?id=nl.hexmaster.theprey',
  auth0: {
    domain: 'theprey.eu.auth0.com',
    clientId: 'tJrm2nPrAX4kES7XEnjUsL38cqbAbraJ',
  },
  // NOTE: No machine-to-machine client id/secret is stored in the app. Location
  // reporting reuses the in-app Auth0 user session (see GameLocationService); the
  // Bearer token is attached by authTokenInterceptor on every API request.
};
