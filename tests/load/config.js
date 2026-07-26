import http from 'k6/http';

export const baseUrl = (__ENV.BASE_URL || 'http://localhost:5035').replace(/\/$/, '');
export const email = __ENV.TEST_EMAIL || 'user@localhost.dev';
export const password = __ENV.TEST_PASSWORD || 'Dev_User_Password_1!';

export const defaultHeaders = {
  'Content-Type': 'application/json',
};

// Named once. Spelling a route at each call site is how one scenario keeps hitting the old
// path after a version bump.
export const routes = {
  login: '/api/v1/auth/login',
  refresh: '/api/v1/auth/refresh',
  me: '/api/v1/users/me',
};

export function url(route) {
  return `${baseUrl}${route}`;
}

export function loginBody() {
  return JSON.stringify({ email, password });
}

/**
 * Returns `{}` rather than `null` on failure, so callers can read `.accessToken` off the
 * result without every one of them repeating a `tokens && tokens.accessToken` guard.
 */
export function parseTokens(response) {
  if (response.status !== 200) {
    return {};
  }

  const body = response.json();
  return {
    accessToken: body.accessToken,
    refreshToken: body.refreshToken,
  };
}

export function login(operation) {
  return http.post(url(routes.login), loginBody(), {
    headers: defaultHeaders,
    tags: { operation },
  });
}

// Per-VU token cache: k6 instantiates the module graph once per VU, so this holds exactly
// the same scope a `let` in each scenario file did.
let tokens = {};

/**
 * The tokens for this VU, logging in once if it does not have them yet. The bootstrap login
 * is tagged `setup` so it stays out of the `login` scenario's own thresholds.
 */
export function currentTokens() {
  if (!tokens.accessToken || !tokens.refreshToken) {
    tokens = parseTokens(login('setup'));
  }

  return tokens;
}

/** Adopts a rotation's tokens. Ignores a failed rotation, leaving the cache as it was. */
export function adoptTokens(response) {
  const rotated = parseTokens(response);

  if (rotated.refreshToken) {
    tokens = rotated;
  }

  return rotated;
}

export const thresholds = {
  login: {
    'http_req_duration{operation:login}': ['p(95)<500'],
    'http_req_failed{operation:login}': ['rate<0.01'],
  },
  refresh: {
    'http_req_duration{operation:refresh}': ['p(95)<100'],
    'http_req_failed{operation:refresh}': ['rate<0.01'],
  },
  me: {
    'http_req_duration{operation:me}': ['p(95)<50'],
    'http_req_failed{operation:me}': ['rate<0.01'],
  },
};

/** The check floor every scenario applies. */
export const checkFloor = {
  checks: ['rate>0.99'],
};
