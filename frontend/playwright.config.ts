import { defineConfig, devices } from '@playwright/test';

// A phone (research R12): `isMobile` makes the browser honour <meta name="viewport">, which a narrow desktop window
// ignores.
const phone = {
  viewport: { width: 360, height: 740 },
  isMobile: true,
  hasTouch: true,
  deviceScaleFactor: 2,
};

export default defineConfig({
  testDir: './e2e',
  forbidOnly: !!process.env['CI'],
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'phone-360', use: { ...devices['Desktop Chrome'], ...phone } },
    {
      name: 'phone-320',
      use: { ...devices['Desktop Chrome'], ...phone, viewport: { width: 320, height: 640 } },
      grep: /@width/,
    },
    {
      name: 'desktop-1280',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } },
      grep: /@width/,
    },
    { name: 'webkit-360', use: { ...devices['Desktop Safari'], ...phone }, grep: /@main-path/ },
  ],
  // The run starts its own development loop, so a developer's scripts/dev.sh must be stopped first: the `e2e` script
  // runs scripts/e2e-preflight.sh before Playwright, which says so. Every test requests codes from one address, so
  // the per-IP limit is raised. SIGTERM lets dev.sh stop the host and ng serve, which run in their own process groups.
  webServer: {
    command: 'bash ../scripts/dev.sh',
    url: 'http://localhost:4200/health',
    env: { Identity__CodeRequestLimits__PerIpAddress: '1000' },
    reuseExistingServer: false,
    timeout: 180_000,
    gracefulShutdown: { signal: 'SIGTERM', timeout: 10_000 },
  },
});
