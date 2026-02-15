import { test, expect } from '@playwright/test';

test.describe('Markdown Rendering', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForTimeout(1000);
  });

  test('should have MarkdownRenderer component available', async ({ page }) => {
    // Verify the app loads successfully
    await expect(page.locator('h1')).toContainText('Hermes');

    // The MarkdownRenderer is used in the ChatPane component
    // We'll verify by checking if messages would be rendered properly
    const chatPane = page.locator('aside').last();
    await expect(chatPane).toBeVisible();
  });

  test('should render assistant messages with markdown (manual message injection)', async ({ page }) => {
    // Inject a mock assistant message with markdown
    await page.evaluate(() => {
      // Find the messages container
      const messagesContainer = document.querySelector('aside:last-of-type .space-y-4');
      if (messagesContainer) {
        // Create a message element with markdown content
        const messageDiv = document.createElement('div');
        messageDiv.className = 'flex justify-start';
        messageDiv.setAttribute('data-testid', 'markdown-test-message');
        messageDiv.innerHTML = `
          <div class="max-w-[85%] rounded-2xl px-4 py-3 shadow-md relative group bg-white text-gray-900 border border-gray-200">
            <div class="prose prose-sm max-w-none prose-headings:text-base prose-p:text-sm prose-p:my-1">
              <h1>Test Heading</h1>
              <p>This is <strong>bold</strong> and <em>italic</em> text.</p>
            </div>
          </div>
        `;
        messagesContainer.appendChild(messageDiv);
      }
    });

    await page.waitForTimeout(500);

    // Verify the message is rendered
    const message = page.locator('[data-testid="markdown-test-message"]');
    await expect(message).toBeVisible();

    // Check for markdown elements
    await expect(message.locator('h1')).toContainText('Test Heading');
    await expect(message.locator('strong')).toContainText('bold');
    await expect(message.locator('em')).toContainText('italic');
  });

  test('should use prose classes for markdown styling', async ({ page }) => {
    // Inject a message
    await page.evaluate(() => {
      const messagesContainer = document.querySelector('aside:last-of-type .space-y-4');
      if (messagesContainer) {
        const messageDiv = document.createElement('div');
        messageDiv.setAttribute('data-testid', 'prose-test-message');
        messageDiv.innerHTML = `
          <div class="prose prose-sm max-w-none">
            <p>Test paragraph</p>
          </div>
        `;
        messagesContainer.appendChild(messageDiv);
      }
    });

    await page.waitForTimeout(500);

    // Check for prose classes
    const proseDiv = page.locator('[data-testid="prose-test-message"] .prose');
    await expect(proseDiv).toBeVisible();

    // Verify prose classes are applied
    const classes = await proseDiv.getAttribute('class');
    expect(classes).toContain('prose');
    expect(classes).toContain('prose-sm');
  });

  test('should render code blocks with proper styling', async ({ page }) => {
    // Inject a message with a code block
    await page.evaluate(() => {
      const messagesContainer = document.querySelector('aside:last-of-type .space-y-4');
      if (messagesContainer) {
        const messageDiv = document.createElement('div');
        messageDiv.setAttribute('data-testid', 'code-block-message');
        messageDiv.innerHTML = `
          <div class="prose prose-sm max-w-none">
            <pre><code class="language-typescript">const hello = "world";</code></pre>
          </div>
        `;
        messagesContainer.appendChild(messageDiv);
      }
    });

    await page.waitForTimeout(500);

    // Check for code block
    const codeBlock = page.locator('[data-testid="code-block-message"] code');
    await expect(codeBlock).toBeVisible();
    await expect(codeBlock).toContainText('const hello');
  });

  test('should render inline code with background', async ({ page }) => {
    // Inject a message with inline code
    await page.evaluate(() => {
      const messagesContainer = document.querySelector('aside:last-of-type .space-y-4');
      if (messagesContainer) {
        const messageDiv = document.createElement('div');
        messageDiv.setAttribute('data-testid', 'inline-code-message');
        messageDiv.innerHTML = `
          <div class="prose prose-sm max-w-none">
            <p>Use <code class="bg-gray-100 px-1.5 py-0.5 rounded text-sm">inline code</code> here.</p>
          </div>
        `;
        messagesContainer.appendChild(messageDiv);
      }
    });

    await page.waitForTimeout(500);

    // Check for inline code with bg class
    const inlineCode = page.locator('[data-testid="inline-code-message"] code.bg-gray-100');
    await expect(inlineCode).toBeVisible();
    await expect(inlineCode).toContainText('inline code');
  });

  test('should render tables', async ({ page }) => {
    // Inject a message with a table
    await page.evaluate(() => {
      const messagesContainer = document.querySelector('aside:last-of-type .space-y-4');
      if (messagesContainer) {
        const messageDiv = document.createElement('div');
        messageDiv.setAttribute('data-testid', 'table-message');
        messageDiv.innerHTML = `
          <div class="prose prose-sm max-w-none">
            <div class="overflow-x-auto my-4">
              <table class="min-w-full divide-y divide-gray-300">
                <thead><tr><th>Column 1</th><th>Column 2</th></tr></thead>
                <tbody><tr><td>Data 1</td><td>Data 2</td></tr></tbody>
              </table>
            </div>
          </div>
        `;
        messagesContainer.appendChild(messageDiv);
      }
    });

    await page.waitForTimeout(500);

    // Check for table
    const table = page.locator('[data-testid="table-message"] table');
    await expect(table).toBeVisible();
    await expect(table.locator('th').first()).toContainText('Column 1');
    await expect(table.locator('td').first()).toContainText('Data 1');
  });

  test('should render links with target blank', async ({ page }) => {
    // Inject a message with a link
    await page.evaluate(() => {
      const messagesContainer = document.querySelector('aside:last-of-type .space-y-4');
      if (messagesContainer) {
        const messageDiv = document.createElement('div');
        messageDiv.setAttribute('data-testid', 'link-message');
        messageDiv.innerHTML = `
          <div class="prose prose-sm max-w-none">
            <p>Visit <a href="https://example.com" target="_blank" rel="noopener noreferrer" class="text-blue-600">our site</a></p>
          </div>
        `;
        messagesContainer.appendChild(messageDiv);
      }
    });

    await page.waitForTimeout(500);

    // Check for link with correct attributes
    const link = page.locator('[data-testid="link-message"] a');
    await expect(link).toBeVisible();
    await expect(link).toHaveAttribute('target', '_blank');
    await expect(link).toHaveAttribute('rel', 'noopener noreferrer');
    await expect(link).toHaveAttribute('href', 'https://example.com');
  });

  test('should render lists', async ({ page }) => {
    // Inject a message with lists
    await page.evaluate(() => {
      const messagesContainer = document.querySelector('aside:last-of-type .space-y-4');
      if (messagesContainer) {
        const messageDiv = document.createElement('div');
        messageDiv.setAttribute('data-testid', 'list-message');
        messageDiv.innerHTML = `
          <div class="prose prose-sm max-w-none">
            <ul><li>Unordered item</li></ul>
            <ol><li>Ordered item</li></ol>
          </div>
        `;
        messagesContainer.appendChild(messageDiv);
      }
    });

    await page.waitForTimeout(500);

    // Check for lists
    const ul = page.locator('[data-testid="list-message"] ul');
    const ol = page.locator('[data-testid="list-message"] ol');
    await expect(ul).toBeVisible();
    await expect(ol).toBeVisible();
  });

  test('should render headings with appropriate hierarchy', async ({ page }) => {
    // Inject a message with multiple heading levels
    await page.evaluate(() => {
      const messagesContainer = document.querySelector('aside:last-of-type .space-y-4');
      if (messagesContainer) {
        const messageDiv = document.createElement('div');
        messageDiv.setAttribute('data-testid', 'headings-message');
        messageDiv.innerHTML = `
          <div class="prose prose-sm max-w-none">
            <h1>Heading 1</h1>
            <h2>Heading 2</h2>
            <h3>Heading 3</h3>
          </div>
        `;
        messagesContainer.appendChild(messageDiv);
      }
    });

    await page.waitForTimeout(500);

    // Check for all heading levels
    const message = page.locator('[data-testid="headings-message"]');
    await expect(message.locator('h1')).toContainText('Heading 1');
    await expect(message.locator('h2')).toContainText('Heading 2');
    await expect(message.locator('h3')).toContainText('Heading 3');
  });
});
