import http from 'k6/http';
import { check } from 'k6';
import {
  adoptTokens,
  checkFloor,
  currentTokens,
  defaultHeaders,
  routes,
  thresholds,
  url,
} from './config.js';

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
    ...checkFloor,
  },
};

export default function () {
  const response = http.post(
    url(routes.refresh),
    JSON.stringify({ refreshToken: currentTokens().refreshToken }),
    {
      headers: defaultHeaders,
      tags: { operation: 'refresh' },
    },
  );

  // Rotation invalidates the presented token, so the successor has to replace it or the
  // next iteration replays a burnt one and trips reuse detection.
  adoptTokens(response);

  check(response, {
    'refresh rotated': (result) => result.status === 200,
  });
}
