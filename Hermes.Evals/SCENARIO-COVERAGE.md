# Hermes.Evals - Scenario Coverage

## Overview

This document tracks the evaluation scenarios covering all Hermes capabilities.

## Scenario Summary

| Scenario | Turns | Capabilities Tested | Status |
|----------|-------|---------------------|--------|
| newsletter-then-hierarchy.yml | 2 | GetWorkItemTree, GetParentHierarchy | ✅ Active |
| simple-tool-selection.yml | 1 | GetWorkItemTree | ✅ Active |
| sla-registration-workflow.yml | 3 | RegisterSlaNotifications, CheckSlaViolations, UnregisterSlaNotifications | ✅ Active |
| sla-violation-checks.yml | 3 | CheckSlaViolations (multiple contexts) | ✅ Active |
| user-activity-discovery.yml | 2 | DiscoverUserActivity | ✅ Active |
| area-path-queries.yml | 2 | GetWorkItemsByAreaPath (with pagination) | ✅ Active |
| full-hierarchy-validation.yml | 2 | GetFullHierarchy, GetParentHierarchy | ✅ Active |
| error-handling.yml | 3 | Error handling, ambiguous requests | ✅ Active |
| parameter-extraction-variations.yml | 4 | Natural language variations across tools | ✅ Active |

**Total:** 9 scenarios, 22 turns

## Capability Coverage

### Azure DevOps Tool
- ✅ **GetWorkItemTree** (Newsletter Generation)
  - Scenarios: newsletter-then-hierarchy, simple-tool-selection, error-handling, parameter-extraction-variations
  - Coverage: Explicit IDs, invalid IDs, various phrasings

- ✅ **GetParentHierarchy** (Parent Hierarchy Validation)
  - Scenarios: newsletter-then-hierarchy, full-hierarchy-validation, parameter-extraction-variations
  - Coverage: Context retention, explicit requests, hashtag format

- ✅ **GetFullHierarchy** (Full Hierarchy with Children)
  - Scenarios: full-hierarchy-validation
  - Coverage: Full validation with descendants

- ✅ **GetWorkItemsByAreaPath** (Area Path Queries)
  - Scenarios: area-path-queries
  - Coverage: Area path filtering, pagination, context retention

- ✅ **DiscoverUserActivity** (PR Activity Discovery)
  - Scenarios: user-activity-discovery, parameter-extraction-variations
  - Coverage: Time period extraction (days/weeks), user context retention

### User Management Tool
- ✅ **RegisterSlaNotifications**
  - Scenarios: sla-registration-workflow, parameter-extraction-variations
  - Coverage: Registration flow, userId extraction

- ✅ **UnregisterSlaNotifications**
  - Scenarios: sla-registration-workflow
  - Coverage: Unregistration flow

### Work Item SLA Tool
- ✅ **CheckSlaViolations**
  - Scenarios: sla-registration-workflow, sla-violation-checks
  - Coverage: Individual user checks, manager views, registered/unregistered users

## Test Coverage by Dimension

### Tool Selection (30% weight)
- All 9 capabilities tested across 9 scenarios
- Alias testing (GetTree, RegisterSLA, etc.)
- Operation name variations

### Parameter Extraction (30% weight)
- Work item IDs: explicit numbers, hashtags (#5753933)
- Email addresses: various formats
- Time periods: explicit days, relative terms ("2 weeks")
- Area paths: full paths with backslashes
- **Dedicated scenario:** parameter-extraction-variations.yml

### Context Retention (25% weight)
- Multi-turn conversations: 7 of 9 scenarios test context
- Work item ID memory (newsletter → hierarchy validation)
- Area path memory (first page → next page)
- User email memory (activity for user → "their" activity)

### Response Quality (15% weight)
- Content validation: required text, forbidden text
- Minimum length checks
- Error handling gracefully (no exceptions exposed)
- **Dedicated scenario:** error-handling.yml

## Edge Cases & Error Handling

### Invalid Inputs
- ✅ Invalid work item ID (99999999) - error-handling.yml
- ✅ Ambiguous requests without context - error-handling.yml

### Context Challenges
- ✅ Pagination without repeating parameters - area-path-queries.yml
- ✅ Context override (explicit user overrides remembered user) - sla-violation-checks.yml
- ✅ Cross-turn work item memory - newsletter-then-hierarchy.yml, full-hierarchy-validation.yml

### Natural Language Variations
- ✅ Formal: "generate newsletter for work item ID 5753933"
- ✅ Casual: "show me #5753933"
- ✅ Abbreviated: "PR activity last 2 weeks"
- ✅ Colloquial: "sign me up for those SLA alerts"

## Missing Coverage

### Capabilities Not Yet Tested
- None - all capabilities have at least one scenario

### Potential Additions
1. **Multi-user workflows** - Testing manager delegation scenarios
2. **Conversation branching** - User changes topic mid-conversation
3. **Complex filters** - Combining multiple query parameters
4. **Performance stress** - Large work item trees, many PR results
5. **Authentication edge cases** - Missing Graph access, invalid users

## Baseline Targets

Based on initial runs:

| Dimension | Target | Stretch Goal |
|-----------|--------|--------------|
| Tool Selection | ≥ 95% | 100% |
| Parameter Extraction | ≥ 95% | 100% |
| Context Retention | ≥ 80% | 95% |
| Response Quality | ≥ 75% | 90% |
| **Overall Score** | **≥ 90%** | **≥ 97%** |

## Maintenance

### Adding New Scenarios
1. Create YAML file in `Scenarios/Definitions/`
2. Follow naming pattern: `{capability}-{variant}.yml`
3. Include all 4 expectation types (tool, parameter, context, quality)
4. Add entry to this coverage document
5. Run evaluation: `dotnet run --project Hermes.Evals`

### Updating Existing Scenarios
- Keep work item IDs current (use existing features from Azure DevOps)
- Adjust expectations if capability instruction prompts change
- Update baseline scores after prompt improvements

### Regression Detection
- Run evaluations before merging PRs that change:
  - Agent instructions (`Resources/Instructions/`)
  - Tool/capability implementations
  - Orchestration logic
- Flag any score decreases > 5% as regressions
- Update scenarios if requirements intentionally change

---

**Last Updated:** 2026-01-30
**Total Scenarios:** 9
**Total Turns:** 22
**Coverage:** 100% of capabilities
