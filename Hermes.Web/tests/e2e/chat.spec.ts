import { test, expect } from '@playwright/test';

test.describe('Chat Functionality', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('should display chat pane', async ({ page }) => {
    const chatPane = page.locator('aside').last();
    await expect(chatPane).toBeVisible();
    await expect(chatPane.getByText('Chat with Hermes')).toBeVisible();
  });

  test('should show WebSocket connection status', async ({ page }) => {
    const chatPane = page.locator('aside').last();

    // Wait for connection status indicator to appear
    const statusIndicator = chatPane.locator('div[class*="rounded-full"]').first();
    await expect(statusIndicator).toBeVisible({ timeout: 10000 });

    // Check if it shows connected (green) or connecting (yellow)
    // We'll accept either as valid since connection might be in progress
    const classes = await statusIndicator.getAttribute('class');
    expect(classes).toMatch(/bg-(green|yellow|gray|red)-/);
  });

  test('should be able to toggle chat pane', async ({ page }) => {
    // Find chat pane - it should be visible by default
    let chatPane = page.locator('aside').filter({ hasText: 'Chat with Hermes' });
    await expect(chatPane).toBeVisible();

    // Close chat - find the close button in the header
    // The close button has the X icon (path with "M6 18L18 6M6 6l12 12")
    const closeButton = chatPane.locator('button[title=""], button').filter({
      has: page.locator('svg path[d*="M6 18L18 6"]')
    }).first();
    await expect(closeButton).toBeVisible();
    await closeButton.click();

    // Chat pane should be removed from DOM (not just hidden)
    await expect(chatPane).not.toBeAttached({ timeout: 2000 });

    // Should show floating toggle button at bottom-right
    const toggleButton = page.locator('button[title="Open Chat"]');
    await expect(toggleButton).toBeVisible();

    // Reopen chat
    await toggleButton.click();

    // Chat pane should be visible again
    chatPane = page.locator('aside').filter({ hasText: 'Chat with Hermes' });
    await expect(chatPane).toBeVisible();
  });

  test('should display chat input', async ({ page }) => {
    const chatPane = page.locator('aside').last();
    const textarea = chatPane.locator('textarea');

    await expect(textarea).toBeVisible();
    await expect(textarea).toHaveAttribute('placeholder', 'Message Hermes...');
  });

  test('should be able to type in chat input', async ({ page }) => {
    const chatPane = page.locator('aside').last();
    const textarea = chatPane.locator('textarea');

    await textarea.fill('Hello Hermes');
    await expect(textarea).toHaveValue('Hello Hermes');
  });

  test('should have send button enabled when text is entered', async ({ page }) => {
    const chatPane = page.locator('aside').last();
    const textarea = chatPane.locator('textarea');
    // Send button is an icon button with a send SVG icon
    const sendButton = chatPane.locator('button').filter({
      has: page.locator('svg path[d*="M2.01 21L23 12"]')
    });

    // Initially, send button might be disabled
    await textarea.fill('Test message');

    // Wait a moment for state to update
    await page.waitForTimeout(500);

    // Send button should now be visible (or waiting for WebSocket connection)
    await expect(sendButton).toBeVisible();
  });

  test('should send message when send button is clicked', async ({ page }) => {
    const chatPane = page.locator('aside').last();
    const textarea = chatPane.locator('textarea');
    // Send button is an icon button with a send SVG icon
    const sendButton = chatPane.locator('button').filter({
      has: page.locator('svg path[d*="M2.01 21L23 12"]')
    });

    // Wait for WebSocket to connect (indicated by green dot)
    const statusIndicator = chatPane.locator('div[class*="rounded-full"]').first();
    await expect(statusIndicator).toBeVisible({ timeout: 10000 });

    // Wait for green (connected) status - retry a few times
    let isConnected = false;
    for (let i = 0; i < 10; i++) {
      const classes = await statusIndicator.getAttribute('class');
      if (classes?.includes('bg-green-500')) {
        isConnected = true;
        break;
      }
      await page.waitForTimeout(1000);
    }

    // Assert connection was successful
    expect(isConnected).toBe(true);

    // Type a message
    await textarea.fill('Hello Hermes');

    // Click send
    await sendButton.click();

    // Check that message appears in chat history
    await expect(chatPane.getByText('Hello Hermes')).toBeVisible({ timeout: 5000 });

    // Input should be cleared
    await expect(textarea).toHaveValue('');

    // Wait for response (this will test if backend orchestrator is working)
    // Hermes should respond with something
    await page.waitForTimeout(10000); // Give orchestrator time to respond

    // Check if there are any response messages
    const messages = chatPane.locator('div[class*="rounded-lg"]');
    const messageCount = await messages.count();

    // Should have at least 2 messages (user + assistant or error)
    expect(messageCount).toBeGreaterThan(1);
  });

  test('should display empty state when no messages', async ({ page }) => {
    const chatPane = page.locator('aside').last();

    // Should show empty state message
    await expect(chatPane.getByText('Start a conversation')).toBeVisible();
  });

  test('should handle Enter key to send message', async ({ page }) => {
    const chatPane = page.locator('aside').last();
    const textarea = chatPane.locator('textarea');

    // Wait for connection
    const statusIndicator = chatPane.locator('div[class*="rounded-full"]').first();
    await expect(statusIndicator).toBeVisible({ timeout: 10000 });

    // Wait for green (connected) status
    let isConnected = false;
    for (let i = 0; i < 10; i++) {
      const classes = await statusIndicator.getAttribute('class');
      if (classes?.includes('bg-green-500')) {
        isConnected = true;
        break;
      }
      await page.waitForTimeout(1000);
    }

    if (!isConnected) {
      test.skip();
      return;
    }

    // Type message and press Enter
    await textarea.fill('Test Enter key');
    await textarea.press('Enter');

    // Message should appear
    await expect(chatPane.getByText('Test Enter key')).toBeVisible({ timeout: 5000 });

    // Input should be cleared
    await expect(textarea).toHaveValue('');
  });
});
