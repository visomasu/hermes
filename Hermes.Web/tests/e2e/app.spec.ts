import { test, expect } from '@playwright/test';

test.describe('Hermes Web UI', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('should load the application', async ({ page }) => {
    // Check page title
    await expect(page).toHaveTitle(/Hermes/);

    // Check main heading
    await expect(page.locator('h1')).toContainText('Hermes');
  });

  test('should have 3-pane layout', async ({ page }) => {
    // Left sidebar should be visible
    await expect(page.locator('aside').first()).toBeVisible();

    // Main content area should be visible
    await expect(page.locator('main')).toBeVisible();

    // Chat pane should be visible by default
    await expect(page.locator('aside').last()).toBeVisible();
  });

  test('should display navigation items', async ({ page }) => {
    const sidebar = page.locator('aside').first();

    // Check navigation items
    await expect(sidebar.getByText('User Settings')).toBeVisible();
    await expect(sidebar.getByText('Team Settings')).toBeVisible();
    await expect(sidebar.getByText('About')).toBeVisible();
  });

  test('should navigate between views', async ({ page }) => {
    // Default view should be User Settings
    await expect(page.locator('main')).toContainText('Notification Preferences');

    // Click Team Settings
    await page.getByText('Team Settings').click();
    await expect(page.locator('main')).toContainText('Team Configuration');

    // Click About
    await page.getByText('About').click();
    await expect(page.locator('main')).toContainText('What is Hermes?');

    // Click back to User Settings
    await page.getByText('User Settings').click();
    await expect(page.locator('main')).toContainText('Notification Preferences');
  });
});
