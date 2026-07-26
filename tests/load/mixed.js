import http from 'k6/http';
import { check } from 'k6';
import {
  adoptTokens,
  checkFloor,
  currentTokens,
  defaultHeaders,
  login as postLogin,
  routes,
  thresholds,
  url,
} from './config.js';

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
    ...checkFloor,
  },
};

export function login() {
  check(postLogin('login'), { 'login completed': (result) => result.status === 200 });
}

export function refresh() {
  const response = http.post(
    url(routes.refresh),
    JSON.stringify({ refreshToken: currentTokens().refreshToken }),
    { headers: defaultHeaders, tags: { operation: 'refresh' } },
  );

  adoptTokens(response);

  check(response, { 'refresh rotated': (result) => result.status === 200 });
}

export function me() {
  const response = http.get(url(routes.me), {
    headers: { Authorization: `Bearer ${currentTokens().accessToken}` },
    tags: { operation: 'me' },
  });

  check(response, { 'profile returned': (result) => result.status === 200 });
}
