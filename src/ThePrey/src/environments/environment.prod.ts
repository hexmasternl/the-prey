export const environment = {
  production: true,
  apiUrl:
    'https://gateway.jollyfield-ab1afcde.westeurope.azurecontainerapps.io',
  auth0: {
    domain: 'theprey.eu.auth0.com',
    clientId: 'tJrm2nPrAX4kES7XEnjUsL38cqbAbraJ',
  },
  // NOTE: No machine-to-machine client id/secret is stored in the app. Location
  // reporting reuses the in-app Auth0 user session (see GameLocationService); the
  // Bearer token is attached by authTokenInterceptor on every API request.
};
