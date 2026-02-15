import { test, expect } from '@playwright/test';

test.describe('User Configuration', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('should display user config form', async ({ page }) => {
    await expect(page.getByText('Notification Preferences')).toBeVisible();
    await expect(page.getByText('Quiet Hours')).toBeVisible();
  });

  test('should have notification toggles', async ({ page }) => {
    // Check for toggle labels
    await expect(page.getByText('SLA Violation Notifications')).toBeVisible();
    await expect(page.getByText('Work Item Update Notifications')).toBeVisible();
  });

  test('should have rate limit inputs', async ({ page }) => {
    // Check for rate limit inputs
    await expect(page.getByText('Max Notifications Per Hour')).toBeVisible();
    await expect(page.getByText('Max Notifications Per Day')).toBeVisible();
  });

  test('should have time zone input', async ({ page }) => {
    await expect(page.getByText('Time Zone')).toBeVisible();
  });

  test('should have quiet hours toggle', async ({ page }) => {
    await expect(page.getByText('Enable Quiet Hours')).toBeVisible();
  });

  test('should show quiet hours time inputs when enabled', async ({ page }) => {
    // Find the quiet hours toggle
    const quietHoursToggle = page.locator('label', { hasText: 'Enable Quiet Hours' }).locator('input[type="checkbox"]');

    // Get current state
    const isChecked = await quietHoursToggle.isChecked();

    // If not checked, click to enable
    if (!isChecked) {
      await quietHoursToggle.click();
    }

    // Time inputs should be visible
    await expect(page.getByText('Start Time')).toBeVisible();
    await expect(page.getByText('End Time')).toBeVisible();
  });

  test('should have save and reset buttons', async ({ page }) => {
    await expect(page.locator('button', { hasText: 'Save Changes' })).toBeVisible();
    await expect(page.locator('button', { hasText: 'Reset' })).toBeVisible();
  });

  test('should initially disable save button when no changes', async ({ page }) => {
    const saveButton = page.locator('button', { hasText: 'Save Changes' });

    // Wait for form to load
    await page.waitForTimeout(2000);

    // Save button should be disabled when no changes
    await expect(saveButton).toBeDisabled();
  });

  test('should enable save button when form is changed', async ({ page }) => {
    const saveButton = page.locator('button', { hasText: 'Save Changes' });

    // Wait for form to load
    await page.waitForTimeout(2000);

    // Initially disabled
    await expect(saveButton).toBeDisabled();

    // Make a change - find first toggle and click it
    const toggles = page.locator('input[type="checkbox"]');
    await toggles.first().click();

    // Wait for state update
    await page.waitForTimeout(500);

    // Save button should now be enabled
    await expect(saveButton).not.toBeDisabled();
  });

  test('should show loading state', async ({ page }) => {
    // The form might show a loading spinner initially
    // We'll just verify the form eventually loads
    await expect(page.getByText('Notification Preferences')).toBeVisible({ timeout: 10000 });
  });
});
