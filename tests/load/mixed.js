import http from 'k6/http';
import { check } from 'k6';
import {
  baseUrl,
  defaultHeaders,
  loginBody,
  parseTokens,
  thresholds,
} from './config.js';

let accessToken;
let refreshToken;

export const options = {
  scenarios: {
    login: {
      executor: 'constant-arrival-rate',
      exec: 'login',
      rate: Number(__ENV.MIXED_LOGIN_RPS || 10),
      timeUnit: '1s',
      duration: __ENV.DURATION || '5m',
      preAllocatedVUs: 20,
      maxVUs: 100,
    },
    refresh: {
      executor: 'constant-arrival-rate',
      exec: 'refresh',
      rate: Number(__ENV.MIXED_REFRESH_RPS || 40),
      timeUnit: '1s',
      duration: __ENV.DURATION || '5m',
      preAllocatedVUs: 40,
      maxVUs: 200,
    },
    me: {
      executor: 'constant-arrival-rate',
      exec: 'me',
      rate: Number(__ENV.MIXED_ME_RPS || 100),
      timeUnit: '1s',
      duration: __ENV.DURATION || '5m',
      preAllocatedVUs: 50,
      maxVUs: 300,
    },
  },
  thresholds: {
    ...thresholds.login,
    ...thresholds.refresh,
    ...thresholds.me,
    checks: ['rate>0.99'],
  },
};

export function login() {
  const response = http.post(
    `${baseUrl}/api/v1/auth/login`,
    loginBody(),
    { headers: defaultHeaders, tags: { operation: 'login' } },
  );
  check(response, { 'login completed': (result) => result.status === 200 });
}

export function refresh() {
  ensureTokens();
  const response = http.post(
    `${baseUrl}/api/v1/auth/refresh`,
    JSON.stringify({ refreshToken }),
    { headers: defaultHeaders, tags: { operation: 'refresh' } },
  );

  if (response.status === 200) {
    const tokens = parseTokens(response);
    accessToken = tokens && tokens.accessToken;
    refreshToken = tokens && tokens.refreshToken;
  }

  check(response, { 'refresh rotated': (result) => result.status === 200 });
}

export function me() {
  ensureTokens();
  const response = http.get(
    `${baseUrl}/api/v1/users/me`,
    {
      headers: { Authorization: `Bearer ${accessToken}` },
      tags: { operation: 'me' },
    },
  );
  check(response, { 'profile returned': (result) => result.status === 200 });
}

function ensureTokens() {
  if (accessToken && refreshToken) {
    return;
  }

  const response = http.post(
    `${baseUrl}/api/v1/auth/login`,
    loginBody(),
    { headers: defaultHeaders, tags: { operation: 'setup' } },
  );
  const tokens = parseTokens(response);
  accessToken = tokens && tokens.accessToken;
  refreshToken = tokens && tokens.refreshToken;
}
