# 🧪 Run Playwright Tests - Quick Reference

## ✅ Markdown & Focus Mode Tests (19 tests - ALL PASSING)

### Quick Run
```bash
cd Hermes.Web
npm run test -- tests/e2e/markdown.spec.ts tests/e2e/focus-mode.spec.ts
```

**Expected Result:**
```
Running 19 tests using 1 worker
  19 passed (1.8m)
```

---

## 🎯 Individual Test Files

### Markdown Tests Only (9 tests)
```bash
npm run test -- tests/e2e/markdown.spec.ts
```

### Focus Mode Tests Only (10 tests)
```bash
npm run test -- tests/e2e/focus-mode.spec.ts
```

---

## 📊 All Tests

### Run All Tests (~85 tests)
```bash
npm run test
```

**Note:** Some tests require backend running (WebSocket, API)

---

## 🛠️ Other Test Commands

### Run Tests with UI
```bash
npm run test:ui
```

### Run Tests in Debug Mode
```bash
npm run test:debug
```

### Show Test Report
```bash
npx playwright show-report
```

---

## ✅ Expected Output

```
$ npm run test -- tests/e2e/markdown.spec.ts tests/e2e/focus-mode.spec.ts

> hermes-web@0.0.0 test
> playwright test tests/e2e/markdown.spec.ts tests/e2e/focus-mode.spec.ts


Running 19 tests using 1 worker

[1/19] [chromium] › tests\e2e\focus-mode.spec.ts:39:3 › Focus Mode › should show focus button in assistant message
[2/19] [chromium] › tests\e2e\focus-mode.spec.ts:49:3 › Focus Mode › should make focus button visible on hover
[3/19] [chromium] › tests\e2e\focus-mode.spec.ts:61:3 › Focus Mode › should have focus button with correct title attribute
[4/19] [chromium] › tests\e2e\focus-mode.spec.ts:67:3 › Focus Mode › should render markdown content in message
[5/19] [chromium] › tests\e2e\focus-mode.spec.ts:77:3 › Focus Mode › should use compact prose classes in chat message
[6/19] [chromium] › tests\e2e\focus-mode.spec.ts:89:3 › Focus Mode › focus mode should have FocusView component structure
[7/19] [chromium] › tests\e2e\focus-mode.spec.ts:106:3 › Focus Mode › should show focus mode with proper toolbar if manually activated
[8/19] [chromium] › tests\e2e\focus-mode.spec.ts:163:3 › Focus Mode › focus mode toolbar should have all required buttons
[9/19] [chromium] › tests\e2e\focus-mode.spec.ts:187:3 › Focus Mode › focus mode should use full typography classes
[10/19] [chromium] › tests\e2e\focus-mode.spec.ts:212:3 › Focus Mode › focus mode content should be in max-w-4xl container
[11/19] [chromium] › tests\e2e\markdown.spec.ts:9:3 › Markdown Rendering › should have MarkdownRenderer component available
[12/19] [chromium] › tests\e2e\markdown.spec.ts:19:3 › Markdown Rendering › should render assistant messages with markdown (manual message injection)
[13/19] [chromium] › tests\e2e\markdown.spec.ts:53:3 › Markdown Rendering › should use prose classes for markdown styling
[14/19] [chromium] › tests\e2e\markdown.spec.ts:81:3 › Markdown Rendering › should render code blocks with proper styling
[15/19] [chromium] › tests\e2e\markdown.spec.ts:105:3 › Markdown Rendering › should render inline code with background
[16/19] [chromium] › tests\e2e\markdown.spec.ts:129:3 › Markdown Rendering › should render tables
[17/19] [chromium] › tests\e2e\markdown.spec.ts:159:3 › Markdown Rendering › should render links with target blank
[18/19] [chromium] › tests\e2e\markdown.spec.ts:185:3 › Markdown Rendering › should render lists
[19/19] [chromium] › tests\e2e\markdown.spec.ts:211:3 › Markdown Rendering › should render headings with appropriate hierarchy

  19 passed (1.8m)
```

---

## 📚 Documentation

- **Full Test Summary:** `PLAYWRIGHT-TESTS-SUMMARY.md`
- **Final Summary:** `MARKDOWN-TESTS-FINAL-SUMMARY.md`
- **Implementation Guide:** `MARKDOWN-SUPPORT-IMPLEMENTATION.md`
- **Quick Start:** `MARKDOWN-IMPLEMENTATION-QUICK-START.md`
- **Test Examples:** `TEST-MARKDOWN.md`

---

## 🚀 Status

- ✅ 19/19 tests passing
- ✅ 100% pass rate
- ✅ ~1.8 minute execution time
- ✅ No backend required
- ✅ Ready for CI/CD

---

**Last Run:** February 14, 2026
**Status:** ✅ ALL PASSING
