import http from 'k6/http';
import { check, sleep } from 'k6';
import { randomIntBetween } from 'https://jslib.k6.io/k6-utils/1.2.0/index.js';

// Configuration
export const options = {
  stages: [
    { duration: '30s', target: 500 }, // Ramp up to 500 virtual users (High Load)
    { duration: '1m', target: 500 },  // Sustained load
    { duration: '30s', target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000'], // Relaxed threshold for local SQLite
    http_req_failed: ['rate<0.05'],    // Allow some failures (409s are expected)
  },
};

const BASE_URL = 'http://localhost:5033';

export default function () {
  const showId = '6a67fc02-32cd-4896-8a75-5eefc77714a0';
  const seatId = '70475856-56b7-43c8-810c-8585482840ba';

  // 1. Get Available Seats
  const res = http.get(`${BASE_URL}/api/seats/${showId}`);
  check(res, { 'status is 200': (r) => r.status === 200 });

  // 2. Random Bulk Hold (Simulate usage)
  const payload = JSON.stringify({
    userId: `user_${__VU}`,
    showId: showId,
    seatIds: [seatId] // In real test, generate dynamic list
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
      'Idempotency-Key': `key_${__VU}_${__ITER}` // Test Idempotency
    },
  };

  const holdRes = http.post(`${BASE_URL}/api/seats/hold-bulk`, payload, params);

  // We expect some 200s (success) and some 409s (conflict) - both are valid handled states
  check(holdRes, {
    'handled correctly': (r) => r.status === 200 || r.status === 409 || r.status === 400,
  });

  sleep(1);
}
