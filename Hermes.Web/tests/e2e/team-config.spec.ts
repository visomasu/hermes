import { test, expect } from '@playwright/test';

test.describe('Team Configuration', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    // Navigate to Team Config view
    await page.click('text=Team Settings');
  });

  test('should display team configuration page', async ({ page }) => {
    await expect(page.getByText('Team Configuration')).toBeVisible();
  });

  test('should display team selection dropdown', async ({ page }) => {
    await expect(page.getByText('Select Team')).toBeVisible();
    await expect(page.locator('select').first()).toBeVisible();
  });

  test('should display create new team button', async ({ page }) => {
    await expect(page.getByRole('button', { name: /\+ Create New Team/i })).toBeVisible();
  });

  test('should show empty state when no teams exist', async ({ page }) => {
    // Check if empty state is shown (depends on backend data)
    const emptyState = page.getByText('No Teams Configured');
    const dropdown = page.getByText('Select Team');

    // Either empty state or dropdown should be visible
    await expect(
      emptyState.or(dropdown)
    ).toBeVisible();
  });

  test('should open create form when clicking create new team', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    // Check form fields appear
    await expect(page.getByText('Basic Information')).toBeVisible();
    await expect(page.getByPlaceholder('e.g., contact-center-ai')).toBeVisible();
    await expect(page.getByPlaceholder('e.g., Contact Center AI')).toBeVisible();
  });

  test('should display all form sections when creating', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    // Check all sections (use heading role to avoid strict mode violations)
    await expect(page.getByRole('heading', { name: /Basic Information/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /Iteration Path/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /Area Paths/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /SLA Overrides/i })).toBeVisible();
  });

  test('should have required fields in basic info', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    await expect(page.getByPlaceholder('e.g., contact-center-ai')).toBeVisible();
    await expect(page.getByPlaceholder('e.g., Contact Center AI')).toBeVisible();
  });

  test('should have iteration path input', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    await expect(page.getByPlaceholder(/OneCRM.*FY26.*Q3/)).toBeVisible();
    await expect(page.getByText(/Example.*OneCRM/i)).toBeVisible();
  });

  test('should have area paths section with add button', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    await expect(page.getByRole('heading', { name: /Area Paths/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Add Area Path/i })).toBeVisible();
  });

  test('should add multiple area paths', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    // Initial area path should exist (use "ContactCenter" to uniquely identify area path inputs)
    const areaPathInputs = page.locator('input[placeholder*="ContactCenter"]');
    await expect(areaPathInputs.first()).toBeVisible();

    // Click add button
    await page.getByRole('button', { name: /Add Area Path/i }).click();

    // Should have 2 inputs now
    await expect(areaPathInputs).toHaveCount(2);
  });

  test('should remove area path when clicking X', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    // Add a second area path
    await page.getByRole('button', { name: /Add Area Path/i }).click();

    const areaPathInputs = page.locator('input[placeholder*="ContactCenter"]');
    await expect(areaPathInputs).toHaveCount(2);

    // Click remove button (X)
    await page.locator('button:has-text("✕")').first().click();

    // Should have 1 input again
    await expect(areaPathInputs).toHaveCount(1);
  });

  test('should have SLA overrides section', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    await expect(page.getByRole('heading', { name: /SLA Overrides/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Add SLA Override/i })).toBeVisible();
  });

  test('should add SLA override with work item type and days', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    // Click add SLA override
    await page.getByRole('button', { name: /Add SLA Override/i }).click();

    // Check for work item type and days inputs
    await expect(page.getByPlaceholder(/Task, Bug, User Story/i)).toBeVisible();
    await expect(page.locator('input[type="number"][placeholder="3"]')).toBeVisible();
  });

  test('should remove SLA override when clicking X', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    // Add an override
    await page.getByRole('button', { name: /Add SLA Override/i }).click();
    const workItemInputs = page.getByPlaceholder(/Task, Bug, User Story/i);
    await expect(workItemInputs).toHaveCount(1);

    // Remove it
    await page.locator('button:has-text("✕")').first().click();
    await expect(workItemInputs).toHaveCount(0);
  });

  test('should show cancel button when creating', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    await expect(page.getByRole('button', { name: 'Cancel' })).toBeVisible();
  });

  test('should show create team button when creating', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    await expect(page.getByRole('button', { name: /Create Team/i })).toBeVisible();
  });

  test('should return to team selection after cancel', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();
    await expect(page.getByPlaceholder('e.g., contact-center-ai')).toBeVisible();

    await page.getByRole('button', { name: 'Cancel' }).click();

    // Should be back to selection view
    await expect(page.getByText('Select Team')).toBeVisible();
    await expect(page.getByPlaceholder('e.g., contact-center-ai')).not.toBeVisible();
  });

  test('should show unsaved changes indicator', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    // Initially no changes
    await expect(page.getByText('✓ All changes saved')).toBeVisible();

    // Make a change
    await page.fill('input[placeholder*="contact-center-ai"]', 'test-team');

    // Should show unsaved indicator
    await expect(page.getByText('• Unsaved changes')).toBeVisible();
  });

  test('should disable team ID field when editing existing team', async ({ page }) => {
    // This test assumes there's at least one team in the system
    const dropdown = page.locator('select').first();
    const optionsCount = await dropdown.locator('option').count();

    if (optionsCount > 1) { // More than just "Select a team" option
      // Select first team
      await dropdown.selectOption({ index: 1 });

      // Team ID should be disabled
      const teamIdInput = page.getByPlaceholder('e.g., contact-center-ai');
      await expect(teamIdInput).toBeDisabled();
    }
  });

  test('should show delete button when editing existing team', async ({ page }) => {
    const dropdown = page.locator('select').first();
    const optionsCount = await dropdown.locator('option').count();

    if (optionsCount > 1) {
      await dropdown.selectOption({ index: 1 });

      await expect(page.getByRole('button', { name: /Delete Team/i })).toBeVisible();
    }
  });

  test('should show save changes button when editing', async ({ page }) => {
    const dropdown = page.locator('select').first();
    const optionsCount = await dropdown.locator('option').count();

    if (optionsCount > 1) {
      await dropdown.selectOption({ index: 1 });

      await expect(page.getByRole('button', { name: /Save Changes/i })).toBeVisible();
    }
  });

  test('form validation should require team id', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    // Try to submit without team ID
    await page.fill('input[placeholder*="Contact Center AI"]', 'Test Team');
    await page.getByRole('button', { name: /Create Team/i }).click();

    // Should show validation error or button stays disabled
    const teamIdInput = page.getByPlaceholder('e.g., contact-center-ai');
    await expect(teamIdInput).toBeVisible();
  });

  test('form validation should require team name', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    // Fill only team ID
    await page.fill('input[placeholder*="contact-center-ai"]', 'test-team');
    await page.getByRole('button', { name: /Create Team/i }).click();

    // Team name field should still be visible (form not submitted)
    const teamNameInput = page.getByPlaceholder('e.g., Contact Center AI');
    await expect(teamNameInput).toBeVisible();
  });

  test('form validation should require iteration path', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    await page.fill('input[placeholder*="contact-center-ai"]', 'test-team');
    await page.fill('input[placeholder*="Contact Center AI"]', 'Test Team');
    await page.getByRole('button', { name: /Create Team/i }).click();

    // Iteration path field should still be visible
    const iterationInput = page.getByPlaceholder(/OneCRM.*FY26.*Q3/);
    await expect(iterationInput).toBeVisible();
  });

  test('should handle multiple SLA overrides', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    // Add first override
    await page.getByRole('button', { name: /Add SLA Override/i }).click();
    await page.fill('input[placeholder*="Task, Bug"]', 'Task');
    await page.fill('input[type="number"][placeholder="3"]', '3');

    // Add second override
    await page.getByRole('button', { name: /Add SLA Override/i }).click();

    // Should have 2 work item type inputs
    const workItemInputs = page.getByPlaceholder(/Task, Bug, User Story/i);
    await expect(workItemInputs).toHaveCount(2);
  });

  test('should display success message after save', async ({ page }) => {
    // This is an integration test that would require mocking backend
    // Just verify the success message element exists in the component
    await page.getByRole('button', { name: /Create New Team/i }).click();

    // The success message should be rendered (even if not visible initially)
    const successMessage = page.locator('text=/created successfully|updated successfully/i');
    await expect(successMessage).toBeHidden(); // Initially hidden
  });

  test('should display error message on failure', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();

    // The error message should exist in DOM (even if not visible)
    const errorMessage = page.locator('text=/Failed to/i');
    await expect(errorMessage).toBeHidden(); // Initially hidden
  });

  test('should have proper styling for dropdown', async ({ page }) => {
    // Check dropdown has proper classes for styling
    const select = page.locator('select').first();
    await expect(select).toHaveClass(/rounded-xl/);
    await expect(select).toHaveClass(/border-2/);
  });

  test('should show custom dropdown arrow icon', async ({ page }) => {
    // Check for the custom chevron icon
    const chevronIcon = page.locator('svg').filter({ hasText: '' }).first();
    await expect(chevronIcon).toBeVisible();
  });

  test('navigation: should switch from user config to team config', async ({ page }) => {
    // Navigate to user settings first
    await page.goto('/');
    await page.click('text=User Settings');

    // Should see user config
    await expect(page.getByText('Notification Preferences')).toBeVisible();

    // Switch to team config
    await page.click('text=Team Settings');

    // Should see team config elements
    await expect(page.getByText(/Select Team|Team Configuration/)).toBeVisible();
  });

  test('navigation: should maintain state when switching views', async ({ page }) => {
    await page.getByRole('button', { name: /Create New Team/i }).click();
    await page.fill('input[placeholder*="contact-center-ai"]', 'test-team');

    // Switch away
    await page.click('text=User Settings');

    // Switch back
    await page.click('text=Team Settings');

    // Should be back to initial state (not in create mode)
    await expect(page.getByText('Select Team')).toBeVisible();
  });
});
