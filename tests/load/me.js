import http from 'k6/http';
import { check } from 'k6';
import { checkFloor, login, parseTokens, routes, thresholds, url } from './config.js';

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

export function setup() {
  const tokens = parseTokens(login('setup'));
  if (!tokens.accessToken) {
    throw new Error('Profile scenario setup login failed.');
  }

  return tokens;
}

export default function (tokens) {
  const response = http.get(url(routes.me), {
    headers: {
      Authorization: `Bearer ${tokens.accessToken}`,
    },
    tags: { operation: 'me' },
  });

  check(response, {
    'profile returned': (result) => result.status === 200,
  });
}
