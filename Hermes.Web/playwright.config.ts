import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: false, // Run tests sequentially for WebSocket stability
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1, // Single worker to avoid WebSocket connection conflicts
  reporter: 'html',

  use: {
    baseURL: 'http://localhost:5175',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  webServer: [
    {
      command: 'npm run dev',
      url: 'http://localhost:5175',
      reuseExistingServer: !process.env.CI,
      timeout: 120 * 1000,
    },
    {
      command: 'cd ../Hermes && dotnet run --configuration Release',
      url: 'http://localhost:3978/api/health',
      reuseExistingServer: !process.env.CI,
      timeout: 180 * 1000, // 3 minutes for backend to fully start
      stdout: 'pipe',
      stderr: 'pipe',
    },
  ],
});
