#!/bin/bash

# Unity test runner script for Datra.Unity.Sample
# Runs Unity Test Framework tests in batch mode

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
UNITY_PROJECT="$PROJECT_ROOT/Datra.Unity.Sample"

# Unity installation path - auto-detect from ProjectVersion.txt
UNITY_VERSION=$(grep "m_EditorVersion:" "$UNITY_PROJECT/ProjectSettings/ProjectVersion.txt" | cut -d' ' -f2)
UNITY_PATH="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"

# Test results
RESULTS_DIR="$PROJECT_ROOT/TestResults"
RESULTS_FILE="$RESULTS_DIR/unity-test-results.xml"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Parse arguments
RUN_COMPILE_CHECK=true
TEST_PLATFORM="EditMode"  # EditMode or PlayMode

while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-compile)
            RUN_COMPILE_CHECK=false
            shift
            ;;
        --playmode)
            TEST_PLATFORM="PlayMode"
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--skip-compile] [--playmode]"
            exit 1
            ;;
    esac
done

echo -e "${YELLOW}=== Unity Test Runner ===${NC}"
echo "Unity Version: $UNITY_VERSION"
echo "Test Platform: $TEST_PLATFORM"
echo ""

# Check if Unity exists
if [ ! -f "$UNITY_PATH" ]; then
    echo -e "${RED}Error: Unity not found at $UNITY_PATH${NC}"
    exit 1
fi

# Create results directory
mkdir -p "$RESULTS_DIR"

# Optional compile check first
if [ "$RUN_COMPILE_CHECK" = true ]; then
    echo -e "${BLUE}Step 1: Compile Check${NC}"
    "$SCRIPT_DIR/test-unity-compile.sh"
    COMPILE_RESULT=$?
    if [ $COMPILE_RESULT -eq 2 ]; then
        echo -e "${YELLOW}Unity is running. Skipping batch mode tests.${NC}"
        echo "Run tests from Unity Editor's Test Runner window instead."
        exit 2
    elif [ $COMPILE_RESULT -ne 0 ]; then
        echo -e "${RED}Compile check failed. Aborting tests.${NC}"
        exit 1
    fi
    echo ""
fi

echo -e "${BLUE}Step 2: Running Unity Tests${NC}"
echo "Results will be saved to: $RESULTS_FILE"
echo ""

# Create temp log file
LOG_FILE=$(mktemp)
trap "rm -f $LOG_FILE" EXIT

# Run Unity tests
"$UNITY_PATH" \
    -batchmode \
    -nographics \
    -projectPath "$UNITY_PROJECT" \
    -runTests \
    -testPlatform "$TEST_PLATFORM" \
    -testResults "$RESULTS_FILE" \
    -logFile "$LOG_FILE" \
    2>&1 || true

# Parse test results
if [ -f "$RESULTS_FILE" ]; then
    # Extract test counts from NUnit XML
    TOTAL=$(grep -o 'total="[0-9]*"' "$RESULTS_FILE" | head -1 | grep -o '[0-9]*')
    PASSED=$(grep -o 'passed="[0-9]*"' "$RESULTS_FILE" | head -1 | grep -o '[0-9]*')
    FAILED=$(grep -o 'failed="[0-9]*"' "$RESULTS_FILE" | head -1 | grep -o '[0-9]*')
    SKIPPED=$(grep -o 'skipped="[0-9]*"' "$RESULTS_FILE" | head -1 | grep -o '[0-9]*')

    echo ""
    echo -e "${YELLOW}=== Test Results ===${NC}"
    echo "Total:   ${TOTAL:-0}"
    echo -e "Passed:  ${GREEN}${PASSED:-0}${NC}"
    echo -e "Failed:  ${RED}${FAILED:-0}${NC}"
    echo "Skipped: ${SKIPPED:-0}"
    echo ""

    # Show failed test details
    if [ "${FAILED:-0}" -gt 0 ]; then
        echo -e "${RED}=== Failed Tests ===${NC}"
        # Extract failed test names and messages from XML
        grep -B2 -A5 'result="Failed"' "$RESULTS_FILE" | head -50
        echo ""
        echo -e "${RED}=== TESTS FAILED ===${NC}"
        exit 1
    else
        echo -e "${GREEN}=== ALL TESTS PASSED ===${NC}"
        exit 0
    fi
else
    echo -e "${RED}Error: Test results file not found${NC}"
    echo "Check the log file for errors:"
    tail -50 "$LOG_FILE"
    exit 1
fi
