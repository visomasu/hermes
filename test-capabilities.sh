#!/bin/bash
# Comprehensive integration test suite for CapabilityMatcher
# Tests all 8 capabilities with various matching strategies

BASE_URL="http://localhost:3978/api/hermes/v1.0/chat"
USER_ID="integrationtest@microsoft.com"
LOG_FILE="integration-test-results.log"

echo "=== CapabilityMatcher Integration Test Suite ===" > $LOG_FILE
echo "Started: $(date)" >> $LOG_FILE
echo "" >> $LOG_FILE

# Function to test a capability
test_capability() {
    local test_name=$1
    local prompt=$2
    local correlation_id=$3

    echo "Testing: $test_name" | tee -a $LOG_FILE

    curl -X POST "$BASE_URL" \
        -H "Content-Type: application/json" \
        -H "x-ms-correlation-id: $correlation_id" \
        -d "{\"text\": \"$prompt\", \"userId\": \"$USER_ID\"}" \
        --max-time 45 --silent --show-error \
        > /dev/null 2>&1

    if [ $? -eq 0 ]; then
        echo "  ✅ Request completed" | tee -a $LOG_FILE
    else
        echo "  ❌ Request failed" | tee -a $LOG_FILE
    fi

    sleep 2
}

echo "=== AzureDevOpsTool Capabilities ===" | tee -a $LOG_FILE
echo "" >> $LOG_FILE

# 1. GetWorkItemTree tests
echo "1. GetWorkItemTree" | tee -a $LOG_FILE
test_capability "GetWorkItemTree (exact)" "get work item tree for id 12345" "test-tree-exact-001"
test_capability "GetTree (alias)" "get tree for work item 12345" "test-tree-alias-002"
test_capability "WorkItemTree (alias)" "show me the work item tree 12345" "test-tree-alias-003"

# 2. GetWorkItemsByAreaPath tests
echo "" >> $LOG_FILE
echo "2. GetWorkItemsByAreaPath" | tee -a $LOG_FILE
test_capability "GetWorkItemsByAreaPath (exact)" "get work items by area path Project/Team" "test-area-exact-001"
test_capability "GetByAreaPath (alias)" "get by area path Project/Team" "test-area-alias-002"

# 3. GetParentHierarchy tests
echo "" >> $LOG_FILE
echo "3. GetParentHierarchy" | tee -a $LOG_FILE
test_capability "GetParentHierarchy (exact)" "get parent hierarchy for work item 12345" "test-parent-exact-001"
test_capability "ParentHierarchy (alias)" "show parent hierarchy for 12345" "test-parent-alias-002"
test_capability "GetParents (alias)" "get parents for work item 12345" "test-parent-alias-003"

# 4. GetFullHierarchy tests
echo "" >> $LOG_FILE
echo "4. GetFullHierarchy" | tee -a $LOG_FILE
test_capability "GetFullHierarchy (exact)" "get full hierarchy for work item 12345" "test-full-exact-001"
test_capability "FullHierarchy (alias)" "show full hierarchy 12345" "test-full-alias-002"
test_capability "CompleteHierarchy (alias)" "get complete hierarchy for 12345" "test-full-alias-003"

# 5. DiscoverUserActivity tests
echo "" >> $LOG_FILE
echo "5. DiscoverUserActivity" | tee -a $LOG_FILE
test_capability "DiscoverUserActivity (exact)" "discover user activity for johndoe@example.com" "test-discover-exact-001"
test_capability "UserActivity (alias)" "show user activity for johndoe@example.com" "test-discover-alias-002"

echo "" >> $LOG_FILE
echo "=== UserManagementTool Capabilities ===" | tee -a $LOG_FILE
echo "" >> $LOG_FILE

# 6. RegisterSlaNotifications tests
echo "6. RegisterSlaNotifications" | tee -a $LOG_FILE
test_capability "RegisterSlaNotifications (exact)" "register sla notifications" "test-reg-exact-001"
test_capability "RegisterSLA (alias)" "register sla" "test-reg-alias-002"
test_capability "RegisterForSLA (alias)" "register for sla" "test-reg-alias-003"
test_capability "Register (alias)" "register me for notifications" "test-reg-alias-004"

# 7. UnregisterSlaNotifications tests
echo "" >> $LOG_FILE
echo "7. UnregisterSlaNotifications" | tee -a $LOG_FILE
test_capability "UnregisterSlaNotifications (exact)" "unregister sla notifications" "test-unreg-exact-001"
test_capability "UnregisterSLA (alias)" "unregister sla" "test-unreg-alias-002"
test_capability "UnregisterForSLA (alias)" "unregister for sla" "test-unreg-alias-003"

echo "" >> $LOG_FILE
echo "=== WorkItemSlaTool Capabilities ===" | tee -a $LOG_FILE
echo "" >> $LOG_FILE

# 8. CheckSlaViolations tests
echo "8. CheckSlaViolations" | tee -a $LOG_FILE
test_capability "CheckSlaViolations (exact)" "check sla violations" "test-check-exact-001"
test_capability "CheckViolations (alias)" "check violations" "test-check-alias-002"
test_capability "CheckSLA (alias)" "check sla" "test-check-alias-003"
test_capability "SLACheck (alias)" "sla check" "test-check-alias-004"

echo "" >> $LOG_FILE
echo "=== Error Cases ===" | tee -a $LOG_FILE
echo "" >> $LOG_FILE

echo "9. Invalid Operations" | tee -a $LOG_FILE
test_capability "NonExistentOperation" "perform some unknown operation" "test-error-001"

echo "" >> $LOG_FILE
echo "=== Test Suite Completed ===" | tee -a $LOG_FILE
echo "Completed: $(date)" >> $LOG_FILE
echo "" >> $LOG_FILE
echo "Waiting for all requests to process (30 seconds)..." | tee -a $LOG_FILE
sleep 30
echo "Done!" | tee -a $LOG_FILE
