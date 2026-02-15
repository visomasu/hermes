# Test Markdown for Hermes Chat

Use this markdown content to test the new rendering features in the Hermes web chat interface.

---

## How to Test

1. Start the backend: `cd Hermes && dotnet run`
2. Frontend is already running at http://localhost:5175/
3. Open the chat pane
4. Send a message that triggers a markdown response, or manually copy sections below to test

---

## Test Cases

### 1. Basic Formatting

This is a paragraph with **bold text**, *italic text*, and ***bold italic text***.

You can also use `inline code` like this.

### 2. Headings

# Heading 1
## Heading 2
### Heading 3
#### Heading 4
##### Heading 5
###### Heading 6

### 3. Lists

#### Unordered List
- First item
- Second item
  - Nested item 1
  - Nested item 2
- Third item

#### Ordered List
1. First step
2. Second step
   1. Sub-step A
   2. Sub-step B
3. Third step

#### Task List (GFM)
- [x] Completed task
- [ ] Pending task
- [ ] Another pending task

### 4. Code Blocks

#### JavaScript
```javascript
function greet(name) {
  console.log(`Hello, ${name}!`);
  return true;
}

greet("Hermes");
```

#### Python
```python
def calculate_metrics(data):
    """Calculate performance metrics."""
    total = sum(data)
    average = total / len(data)
    return {
        'total': total,
        'average': average,
        'count': len(data)
    }
```

#### TypeScript
```typescript
interface User {
  id: string;
  name: string;
  email: string;
}

const fetchUser = async (id: string): Promise<User> => {
  const response = await fetch(`/api/users/${id}`);
  return response.json();
};
```

#### C#
```csharp
public class NewsletterService
{
    private readonly ILogger<NewsletterService> _logger;

    public async Task<string> GenerateNewsletterAsync(int featureId)
    {
        _logger.LogInformation("Generating newsletter for feature {FeatureId}", featureId);
        var result = await _processor.ProcessAsync(featureId);
        return result.Content;
    }
}
```

#### SQL
```sql
SELECT
    wi.Id,
    wi.Title,
    wi.State,
    COUNT(children.Id) as ChildCount
FROM WorkItems wi
LEFT JOIN WorkItems children ON children.ParentId = wi.Id
WHERE wi.Type = 'Feature'
GROUP BY wi.Id, wi.Title, wi.State
ORDER BY wi.CreatedDate DESC;
```

### 5. Tables

#### Simple Table
| Feature | Status | Priority |
|---------|--------|----------|
| Markdown Rendering | ✅ Complete | High |
| Focus Mode | ✅ Complete | High |
| Syntax Highlighting | ✅ Complete | Medium |
| Export Feature | ✅ Complete | Low |

#### Complex Table
| Metric | Before | After | Improvement | Notes |
|--------|--------|-------|-------------|-------|
| Load Time | 2.5s | 1.2s | 52% faster | After optimization |
| Bundle Size | 1.8MB | 1.1MB | 39% smaller | With code splitting |
| Memory Usage | 150MB | 95MB | 37% reduction | Better caching |
| API Calls | 15 | 8 | 47% fewer | Request batching |

### 6. Blockquotes

> "The best way to predict the future is to create it."
> — Alan Kay

> **Note:** This is a multi-line blockquote.
> It can span multiple lines and include **formatting**.
> - Even lists!
> - Like this one

### 7. Horizontal Rules

Use three dashes to create a horizontal rule:

---

Content after the rule.

### 8. Links

- [Hermes Repository](https://github.com/your-org/hermes)
- [Azure DevOps Documentation](https://docs.microsoft.com/azure/devops)
- [React Markdown](https://github.com/remarkjs/react-markdown)

### 9. Images (if supported)

![Hermes Logo](https://via.placeholder.com/150?text=Hermes)

### 10. Strikethrough (GFM)

This is ~~incorrect~~ correct information.

### 11. Combination Example

# Sprint 2024-Q1 Summary

## 📊 Key Metrics

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Story Points | 50 | 48 | ✅ On Track |
| Bugs Fixed | 20 | 25 | ✅ Exceeded |
| Code Coverage | 80% | 85% | ✅ Exceeded |

## ✅ Completed Features

1. **User Authentication**
   - OAuth 2.0 integration
   - Multi-factor authentication
   - Session management

2. **Notification System**
   - Real-time Teams notifications
   - Email digest support
   - Customizable preferences

3. **Performance Improvements**
   - Reduced API latency by 40%
   - Optimized database queries
   - Implemented L1/L2 caching

## 🔧 Technical Improvements

### Backend
```csharp
// New capability pattern implementation
public async Task<string> ExecuteAsync(GenerateNewsletterInput input)
{
    var workItem = await _client.GetWorkItemAsync(input.FeatureId);
    var children = await _client.GetChildrenAsync(input.FeatureId);
    var newsletter = await _generator.GenerateAsync(workItem, children);
    return JsonSerializer.Serialize(newsletter);
}
```

### Frontend
```typescript
// New markdown rendering
<MarkdownRenderer
  content={message.content}
  mode="compact"
/>
```

## 📝 Next Sprint Goals

- [ ] Implement advanced filtering
- [ ] Add chart visualizations
- [ ] Enhance mobile experience
- [ ] Performance monitoring dashboard

## 💡 Key Learnings

> "We discovered that the L1/L2 caching pattern reduced our database load by 70%, which was a game-changer for performance."

The team successfully delivered **95% of planned features** and exceeded quality targets.

---

**Sprint Duration:** 2 weeks
**Team Size:** 5 developers
**Deployment Date:** 2026-02-14

### 12. Nested Content

#### Complex Nested Example

Here's a more complex example with multiple levels:

1. **Phase 1: Planning**
   - Define requirements
   - Create user stories
   - Estimate effort

   ```typescript
   const plan = {
     duration: "2 weeks",
     resources: 5,
     scope: "MVP"
   };
   ```

2. **Phase 2: Development**
   - Backend implementation
     - API endpoints
     - Database schema
     - Business logic
   - Frontend implementation
     - UI components
     - State management
     - API integration

3. **Phase 3: Testing**

   | Test Type | Coverage | Status |
   |-----------|----------|--------|
   | Unit Tests | 85% | ✅ |
   | Integration Tests | 70% | ✅ |
   | E2E Tests | 60% | 🔄 |

---

## Quick Copy-Paste Tests

### Minimal Test
```
# Hello Hermes!
This is a **test** with *some* formatting and `code`.
```

### Medium Test
```
## Feature Summary
- Implemented **markdown support**
- Added *focus mode*
- Included `syntax highlighting`

| Component | Status |
|-----------|--------|
| MarkdownRenderer | ✅ |
| FocusView | ✅ |
```

### Full Test
Copy the entire document above for a comprehensive test!

---

**Testing Date:** 2026-02-14
**Status:** Ready for testing
