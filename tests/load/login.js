import http from 'k6/http';
import { check } from 'k6';
import { baseUrl, defaultHeaders, loginBody, thresholds } from './config.js';

export const options = {
  scenarios: {
    login: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.LOGIN_RPS || 50),
      timeUnit: '1s',
      duration: __ENV.DURATION || '2m',
      preAllocatedVUs: 50,
      maxVUs: 200,
    },
  },
  thresholds: {
    ...thresholds.login,
    checks: ['rate>0.99'],
  },
};

export default function () {
  const response = http.post(
    `${baseUrl}/api/v1/auth/login`,
    loginBody(),
    {
      headers: defaultHeaders,
      tags: { operation: 'login' },
    },
  );

  check(response, {
    'login completed': (result) => result.status === 200,
  });
}
