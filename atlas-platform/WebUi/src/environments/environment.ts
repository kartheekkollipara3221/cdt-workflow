// Local preview: the stand-in API (atlas-platform/src/BBH.CP.Atlas.App, launch profile StandIn).
// At the target, the existing environment files stay (apiBaseUrl "/v1/" or the development URL).
// The platform URLs have no v1/ prefix, because the base URL gives it.

/** Settings of the local preview build. */
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5080/v1/',
};
