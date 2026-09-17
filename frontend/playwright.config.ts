import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./tests",
  fullyParallel: true,
  timeout: 30_000,
  use: {
    baseURL: "http://127.0.0.1:4200",
    ...devices["Desktop Chrome"],
    channel: process.env["PLAYWRIGHT_CHANNEL"] || "chromium",
    trace: "retain-on-failure",
  },
  webServer: {
    command:
      "node node_modules/@angular/cli/bin/ng.js serve --host 127.0.0.1 --port 4200",
    url: "http://127.0.0.1:4200",
    reuseExistingServer: !process.env["CI"],
    timeout: 120_000,
  },
});
