import http from 'k6/http';
import { check } from 'k6';
import {
  adoptTokens,
  checkFloor,
  createTokenPool,
  defaultHeaders,
  routes,
  thresholds,
  url,
} from './config.js';

const maxVUs = Number(__ENV.REFRESH_MAX_VUS || 100);

export const options = {
  scenarios: {
    refresh: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.REFRESH_RPS || 200),
      timeUnit: '1s',
      duration: __ENV.DURATION || '2m',
      preAllocatedVUs: Math.min(20, maxVUs),
      maxVUs,
    },
  },
  thresholds: {
    ...thresholds.refresh,
    ...checkFloor,
  },
  setupTimeout: '5m',
};

export function setup() {
  return createTokenPool(maxVUs);
}

let tokens;

export default function (tokenPool) {
  tokens ||= tokenPool[__VU - 1];
  if (!tokens?.refreshToken) {
    throw new Error(`No refresh session was prepared for VU ${__VU}.`);
  }

  const response = http.post(
    url(routes.refresh),
    JSON.stringify({ refreshToken: tokens.refreshToken }),
    {
      headers: defaultHeaders,
      tags: { operation: 'refresh' },
    },
  );

  // Rotation invalidates the presented token, so the successor has to replace it or the
  // next iteration replays a burnt one and trips reuse detection.
  tokens = adoptTokens(response);

  check(response, {
    'refresh rotated': (result) => result.status === 200,
  });
}
