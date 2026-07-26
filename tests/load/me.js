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

export const options = {
  scenarios: {
    me: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.ME_RPS || 500),
      timeUnit: '1s',
      duration: __ENV.DURATION || '2m',
      preAllocatedVUs: 100,
      maxVUs: 500,
    },
  },
  thresholds: {
    ...thresholds.me,
    checks: ['rate>0.99'],
  },
};

export default function () {
  if (!accessToken) {
    const login = http.post(
      `${baseUrl}/api/v1/auth/login`,
      loginBody(),
      { headers: defaultHeaders, tags: { operation: 'setup' } },
    );
    const tokens = parseTokens(login);
    accessToken = tokens && tokens.accessToken;
  }

  const response = http.get(
    `${baseUrl}/api/v1/users/me`,
    {
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
      tags: { operation: 'me' },
    },
  );

  check(response, {
    'profile returned': (result) => result.status === 200,
  });
}
