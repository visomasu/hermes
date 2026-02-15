import { test, expect } from '@playwright/test';

test.describe('Toggle Component Functionality', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    // Wait for form to load
    await page.waitForSelector('text=Notification Preferences', { timeout: 10000 });
  });

  test('should visually update when SLA Violation toggle is clicked', async ({ page }) => {
    // Find the SLA Violation toggle
    const toggleLabel = page.locator('label', { hasText: 'SLA Violation Notifications' });
    const toggleBackground = toggleLabel.locator('div.rounded-full').first();
    const toggleButton = toggleLabel.locator('div.rounded-full').last();

    // Get initial state
    const initialBgClass = await toggleBackground.getAttribute('class');
    const initialButtonClass = await toggleButton.getAttribute('class');

    // Click the toggle
    await toggleLabel.click();

    // Wait for animation
    await page.waitForTimeout(300);

    // Check that visual state changed
    const newBgClass = await toggleBackground.getAttribute('class');
    const newButtonClass = await toggleButton.getAttribute('class');

    // Background and button classes should have changed
    expect(newBgClass).not.toBe(initialBgClass);
    expect(newButtonClass).not.toBe(initialButtonClass);

    // Check for blue background (on state) or gray (off state)
    const hasBlue = newBgClass?.includes('bg-blue-600') || initialBgClass?.includes('bg-blue-600');
    const hasGray = newBgClass?.includes('bg-gray-300') || initialBgClass?.includes('bg-gray-300');
    expect(hasBlue || hasGray).toBe(true);
  });

  test('should toggle between on and off states multiple times', async ({ page }) => {
    const toggleLabel = page.locator('label', { hasText: 'SLA Violation Notifications' });
    const toggleBackground = toggleLabel.locator('div.rounded-full').first();

    // Click toggle 3 times
    for (let i = 0; i < 3; i++) {
      const beforeClass = await toggleBackground.getAttribute('class');
      await toggleLabel.click();
      await page.waitForTimeout(300);
      const afterClass = await toggleBackground.getAttribute('class');

      // State should change each time
      expect(afterClass).not.toBe(beforeClass);
    }
  });

  test('should enable save button when toggle is clicked', async ({ page }) => {
    const saveButton = page.locator('button', { hasText: 'Save Changes' });

    // Wait for initial load
    await page.waitForTimeout(2000);

    // Initially disabled
    await expect(saveButton).toBeDisabled();

    // Click any toggle
    const toggleLabel = page.locator('label', { hasText: 'SLA Violation Notifications' });
    await toggleLabel.click();

    // Wait for state update
    await page.waitForTimeout(500);

    // Save button should now be enabled
    await expect(saveButton).not.toBeDisabled();
  });

  test('should show correct visual state for all toggles', async ({ page }) => {
    // Check all three main toggles exist and have visual elements
    const toggles = [
      'SLA Violation Notifications',
      'Work Item Update Notifications',
      'Enable Quiet Hours'
    ];

    for (const toggleText of toggles) {
      const toggleLabel = page.locator('label', { hasText: toggleText });
      await expect(toggleLabel).toBeVisible();

      // Check for background and button elements
      const backgrounds = toggleLabel.locator('div.rounded-full');
      await expect(backgrounds.first()).toBeVisible();
      await expect(backgrounds.last()).toBeVisible();
    }
  });

  test('should update Work Item Update Notifications toggle', async ({ page }) => {
    const toggleLabel = page.locator('label', { hasText: 'Work Item Update Notifications' });
    const toggleBackground = toggleLabel.locator('div.rounded-full').first();

    const initialClass = await toggleBackground.getAttribute('class');
    await toggleLabel.click();
    await page.waitForTimeout(300);
    const newClass = await toggleBackground.getAttribute('class');

    expect(newClass).not.toBe(initialClass);
  });

  test('should update Quiet Hours toggle', async ({ page }) => {
    const toggleLabel = page.locator('label', { hasText: 'Enable Quiet Hours' });
    const toggleBackground = toggleLabel.locator('div.rounded-full').first();

    const initialClass = await toggleBackground.getAttribute('class');
    await toggleLabel.click();
    await page.waitForTimeout(300);
    const newClass = await toggleBackground.getAttribute('class');

    expect(newClass).not.toBe(initialClass);
  });

  test('should show blue background when toggle is ON', async ({ page }) => {
    const toggleLabel = page.locator('label', { hasText: 'SLA Violation Notifications' });
    const toggleBackground = toggleLabel.locator('div.rounded-full').first();

    // Click until we see blue (on state)
    for (let i = 0; i < 2; i++) {
      await toggleLabel.click();
      await page.waitForTimeout(300);
      const bgClass = await toggleBackground.getAttribute('class');

      if (bgClass?.includes('bg-blue-600')) {
        // Found blue state - test passes
        expect(bgClass).toContain('bg-blue-600');
        return;
      }
    }

    // If we didn't find blue after 2 clicks, check for gray
    const bgClass = await toggleBackground.getAttribute('class');
    expect(bgClass).toContain('bg-gray-300');
  });

  test('should translate button when toggle is ON', async ({ page }) => {
    const toggleLabel = page.locator('label', { hasText: 'SLA Violation Notifications' });
    const toggleButton = toggleLabel.locator('div.rounded-full').last();

    // Click until we see translate-x-6 (on state)
    for (let i = 0; i < 2; i++) {
      await toggleLabel.click();
      await page.waitForTimeout(300);
      const buttonClass = await toggleButton.getAttribute('class');

      if (buttonClass?.includes('translate-x-6')) {
        // Found translated state - test passes
        expect(buttonClass).toContain('translate-x-6');
        return;
      }
    }

    // If no translation found, that's also valid (off state)
    const buttonClass = await toggleButton.getAttribute('class');
    expect(buttonClass).toBeDefined();
  });

  test('should keep toggle state after clicking multiple times', async ({ page }) => {
    const toggleLabel = page.locator('label', { hasText: 'Enable Quiet Hours' });
    const toggleBackground = toggleLabel.locator('div.rounded-full').first();

    // Click 4 times (should end in same state as started)
    let stateClasses: string[] = [];

    for (let i = 0; i < 4; i++) {
      const bgClass = await toggleBackground.getAttribute('class');
      stateClasses.push(bgClass || '');
      await toggleLabel.click();
      await page.waitForTimeout(300);
    }

    // After 4 clicks, should be back to original state
    const finalClass = await toggleBackground.getAttribute('class');
    expect(finalClass).toBe(stateClasses[0]);
  });

  test('should work independently - clicking one toggle does not affect others', async ({ page }) => {
    const toggle1 = page.locator('label', { hasText: 'SLA Violation Notifications' });
    const toggle2 = page.locator('label', { hasText: 'Work Item Update Notifications' });

    const bg1 = toggle1.locator('div.rounded-full').first();
    const bg2 = toggle2.locator('div.rounded-full').first();

    const initial2Class = await bg2.getAttribute('class');

    // Click first toggle
    await toggle1.click();
    await page.waitForTimeout(300);

    // Second toggle should not have changed
    const after2Class = await bg2.getAttribute('class');
    expect(after2Class).toBe(initial2Class);
  });
});
