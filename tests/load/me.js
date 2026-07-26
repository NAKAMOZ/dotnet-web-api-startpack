import http from 'k6/http';
import { check } from 'k6';
import { checkFloor, currentTokens, routes, thresholds, url } from './config.js';

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
    ...checkFloor,
  },
};

export default function () {
  const response = http.get(url(routes.me), {
    headers: {
      Authorization: `Bearer ${currentTokens().accessToken}`,
    },
    tags: { operation: 'me' },
  });

  check(response, {
    'profile returned': (result) => result.status === 200,
  });
}
