// The load test of the main screen (FR-040, SC-006, research R14). Each virtual user owns one session token from
// load/.sessions.json, written by scripts/load-test.sh, and sends it as the Cookie header. Before each action the user
// sleeps a uniformly random 1 to 5 seconds. Every 10th action switches the language (PUT /api/me/locale, alternating
// en and pl); the others open the main screen: GET / then GET /api/me.
import http from 'k6/http';
import { check, sleep } from 'k6';
import { SharedArray } from 'k6/data';

const users = Number(__ENV.LOAD_USERS || 100);
const duration = __ENV.LOAD_DURATION || '10m';
const p95Ms = Number(__ENV.LOAD_P95_MS || 300);
const baseUrl = __ENV.BASE_URL || 'http://localhost:18080';

const tokens = new SharedArray('sessions', () => JSON.parse(open('./.sessions.json')));

export const options = {
  vus: users,
  duration,
  // Failed requests and failed checks fail the run too: a regression that answers every request fast with an error
  // (401 on /api/me) would otherwise pass on the duration alone.
  thresholds: {
    http_req_duration: [`p(95)<${p95Ms}`],
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
  },
};

export default function () {
  const cookie = tokens[(__VU - 1) % tokens.length];
  const headers = { Cookie: `session=${cookie}` };
  sleep(1 + Math.random() * 4);

  const action = __ITER + 1;
  if (action % 10 === 0) {
    const locale = (action / 10) % 2 === 1 ? 'en' : 'pl';
    const response = http.put(`${baseUrl}/api/me/locale`, JSON.stringify({ locale }), {
      headers: { ...headers, 'Content-Type': 'application/json' },
    });
    check(response, { 'locale changed (204)': (r) => r.status === 204 });
  } else {
    const page = http.get(`${baseUrl}/`, { headers });
    const me = http.get(`${baseUrl}/api/me`, { headers });
    check(page, { 'main screen (200)': (r) => r.status === 200 });
    check(me, { 'current user (200)': (r) => r.status === 200 });
  }
}
