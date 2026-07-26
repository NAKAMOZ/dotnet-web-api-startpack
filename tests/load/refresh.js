import http from 'k6/http';
import { check } from 'k6';
import {
  baseUrl,
  defaultHeaders,
  loginBody,
  parseTokens,
  thresholds,
} from './config.js';

let refreshToken;

export const options = {
  scenarios: {
    refresh: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.REFRESH_RPS || 200),
      timeUnit: '1s',
      duration: __ENV.DURATION || '2m',
      preAllocatedVUs: 100,
      maxVUs: 400,
    },
  },
  thresholds: {
    ...thresholds.refresh,
    checks: ['rate>0.99'],
  },
};

export default function () {
  if (!refreshToken) {
    const login = http.post(
      `${baseUrl}/api/v1/auth/login`,
      loginBody(),
      { headers: defaultHeaders, tags: { operation: 'setup' } },
    );
    const tokens = parseTokens(login);
    refreshToken = tokens && tokens.refreshToken;
  }

  const response = http.post(
    `${baseUrl}/api/v1/auth/refresh`,
    JSON.stringify({ refreshToken }),
    {
      headers: defaultHeaders,
      tags: { operation: 'refresh' },
    },
  );

  if (response.status === 200) {
    refreshToken = response.json().refreshToken;
  }

  check(response, {
    'refresh rotated': (result) => result.status === 200,
  });
}
