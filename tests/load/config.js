export const baseUrl = (__ENV.BASE_URL || 'http://localhost:5035').replace(/\/$/, '');
export const email = __ENV.TEST_EMAIL || 'user@localhost.dev';
export const password = __ENV.TEST_PASSWORD || 'Dev_User_Password_1!';

export const defaultHeaders = {
  'Content-Type': 'application/json',
};

export function loginBody() {
  return JSON.stringify({ email, password });
}

export function parseTokens(response) {
  if (response.status !== 200) {
    return null;
  }

  const body = response.json();
  return {
    accessToken: body.accessToken,
    refreshToken: body.refreshToken,
  };
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
