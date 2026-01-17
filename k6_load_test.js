import http from 'k6/http';
import { check, sleep } from 'k6';
import { randomIntBetween } from 'https://jslib.k6.io/k6-utils/1.2.0/index.js';

// Configuration
export const options = {
  stages: [
    { duration: '30s', target: 100 }, // Ramp up to 100 virtual users
    { duration: '1m', target: 500 },  // Spike to 500 users
    { duration: '30s', target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<200'], // 95% of requests should be under 200ms
    http_req_failed: ['rate<0.01'],   // Error rate should be less than 1%
  },
};

const BASE_URL = 'http://localhost:5033';

export default function () {
  const showId = 'YOUR_SHOW_UUID_HERE'; // Replace with a valid Show ID from your DB
  const seatId = 'YOUR_SEAT_UUID_HERE'; // Replace with valid Seat ID (or randomize)

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
