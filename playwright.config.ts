import { defineConfig, devices } from '@playwright/test';

const PORT = Number(process.env['E2E_PORT'] || 4300);
const baseURL = `http://localhost:${PORT}`;

/**
 * Headless end-to-end configuration for the MAUI Designer.
 * The Angular dev server is started automatically unless E2E_BASE_URL is provided.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  workers: process.env['CI'] ? 1 : undefined,
  reporter: process.env['CI'] ? [['list'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL: process.env['E2E_BASE_URL'] || baseURL,
    headless: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    viewport: { width: 1440, height: 900 }
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ],
  webServer: process.env['E2E_BASE_URL']
    ? undefined
    : {
        command: `npx ng serve --port ${PORT} --host 127.0.0.1`,
        url: baseURL,
        reuseExistingServer: !process.env['CI'],
        timeout: 180_000
      }
});
