import { check } from 'k6';
import { checkFloor, login, thresholds } from './config.js';

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
    ...checkFloor,
  },
};

export default function () {
  const response = login('login');

  check(response, {
    'login completed': (result) => result.status === 200,
  });
}
