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

/**
 * Creates independent sessions before a rotation scenario starts. Batching four logins at
 * a time bounds Argon2 memory to roughly 256 MiB while keeping setup reasonably short.
 * Setup traffic is tagged out of endpoint thresholds.
 */
export function createTokenPool(size) {
  const tokens = [];
  const batchSize = 4;

  for (let offset = 0; offset < size; offset += batchSize) {
    const count = Math.min(batchSize, size - offset);
    const responses = http.batch(
      Array.from({ length: count }, () => ({
        method: 'POST',
        url: url(routes.login),
        body: loginBody(),
        params: { headers: defaultHeaders, tags: { operation: 'setup' } },
      })),
    );

    for (const response of responses) {
      const issued = parseTokens(response);
      if (!issued.refreshToken) {
        throw new Error('Token-pool setup login failed.');
      }

      tokens.push(issued);
    }
  }

  return tokens;
}

/** Parses a rotation response. Each scenario owns its own per-VU token variable. */
export function adoptTokens(response) {
  return parseTokens(response);
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
