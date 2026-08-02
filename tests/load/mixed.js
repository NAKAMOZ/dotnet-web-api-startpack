import http from 'k6/http';
import { check } from 'k6';
import {
  adoptTokens,
  checkFloor,
  createTokenPool,
  defaultHeaders,
  login as postLogin,
  parseTokens,
  routes,
  thresholds,
  url,
} from './config.js';

const loginMaxVUs = Number(__ENV.MIXED_LOGIN_MAX_VUS || 100);
const refreshMaxVUs = Number(__ENV.MIXED_REFRESH_MAX_VUS || 200);
const meMaxVUs = Number(__ENV.MIXED_ME_MAX_VUS || 300);
const totalMaxVUs = loginMaxVUs + refreshMaxVUs + meMaxVUs;

export const options = {
  scenarios: {
    login: {
      executor: 'constant-arrival-rate',
      exec: 'login',
      rate: Number(__ENV.MIXED_LOGIN_RPS || 10),
      timeUnit: '1s',
      duration: __ENV.DURATION || '5m',
      preAllocatedVUs: 20,
      maxVUs: loginMaxVUs,
    },
    refresh: {
      executor: 'constant-arrival-rate',
      exec: 'refresh',
      rate: Number(__ENV.MIXED_REFRESH_RPS || 40),
      timeUnit: '1s',
      duration: __ENV.DURATION || '5m',
      preAllocatedVUs: 40,
      maxVUs: refreshMaxVUs,
    },
    me: {
      executor: 'constant-arrival-rate',
      exec: 'me',
      rate: Number(__ENV.MIXED_ME_RPS || 100),
      timeUnit: '1s',
      duration: __ENV.DURATION || '5m',
      preAllocatedVUs: 50,
      maxVUs: meMaxVUs,
    },
  },
  thresholds: {
    ...thresholds.login,
    ...thresholds.refresh,
    ...thresholds.me,
    ...checkFloor,
  },
  setupTimeout: '10m',
};

export function setup() {
  const profileTokens = parseTokens(postLogin('setup'));
  if (!profileTokens.accessToken) {
    throw new Error('Mixed scenario setup login failed.');
  }

  // k6 assigns __VU globally across all concurrent scenarios. Preparing the total maximum
  // makes every possible refresh VU index safe without sharing a rotating token.
  return {
    profileTokens,
    refreshTokenPool: createTokenPool(totalMaxVUs),
  };
}

let refreshTokens;

export function login() {
  check(postLogin('login'), { 'login completed': (result) => result.status === 200 });
}

export function refresh(data) {
  refreshTokens ||= data.refreshTokenPool[__VU - 1];
  if (!refreshTokens?.refreshToken) {
    throw new Error(`No mixed refresh session was prepared for VU ${__VU}.`);
  }

  const response = http.post(
    url(routes.refresh),
    JSON.stringify({ refreshToken: refreshTokens.refreshToken }),
    { headers: defaultHeaders, tags: { operation: 'refresh' } },
  );

  refreshTokens = adoptTokens(response);

  check(response, { 'refresh rotated': (result) => result.status === 200 });
}

export function me(data) {
  const response = http.get(url(routes.me), {
    headers: { Authorization: `Bearer ${data.profileTokens.accessToken}` },
    tags: { operation: 'me' },
  });

  check(response, { 'profile returned': (result) => result.status === 200 });
}
