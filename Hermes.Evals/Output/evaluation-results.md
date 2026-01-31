# Hermes Evaluation Report

**Generated:** 2026-01-31 05:45:06 UTC

## Executive Summary

**Status:** ❌ **FAILING**

- **Scenarios Passed:** 0/1 (0.0%)
- **Overall Score:** 0.900
- **Total Turns Executed:** 3
- **Average Execution Time:** 18008ms per turn

## Evaluation Metrics

| Dimension | Score | Grade | Target |
|-----------|-------|-------|--------|
| Tool Selection | 1.000 | Excellent ✅ | 0.95 |
| Parameter Extraction | 0.667 | Poor ⚠️ | 0.98 |
| Context Retention | 1.000 | Excellent ✅ | 0.80 |
| Response Quality | 1.000 | Excellent ✅ | 0.75 |

## Performance Metrics

| Metric | Value | Target |
|--------|-------|--------|
| Average Execution Time | 18008ms | <1500ms |
| P95 Execution Time | 28190ms | <2500ms |
| P99 Execution Time | 28190ms | <3000ms |

## Detailed Scenario Results

### ❌ SLA Violation Checks

- **Status:** FAILED
- **Overall Score:** 0.900
- **Execution Mode:** RestApi
- **Data Mode:** Real
- **Execution Time:** 54026ms
- **Turns:** 3 (2 passed, 1 failed)

**Dimension Scores:**

- Tool Selection: 1.000
- Parameter Extraction: 0.667
- Context Retention: 1.000
- Response Quality: 1.000

**Failed Turns:**

- **Turn 2:** Score 0.700
  - ❌ ParameterExtraction_Parameter_teamsUserId: Expected: "user@microsoft.com", Actual: "visomasu@microsoft.com"


## Recommendations

- 🔧 **Parameter Extraction:** Improve parameter extraction prompts or add more examples to capability instructions.
- ⚡ **Performance:** Average execution time exceeds target. Consider optimization or caching strategies.
- ⚠️ **Overall Success Rate:** Less than 80% of scenarios passing. Investigate failed scenarios for systemic issues.

---

🤖 *Generated with [Hermes.Evals](https://github.com/your-org/hermes)*

