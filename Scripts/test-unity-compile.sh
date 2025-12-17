#!/bin/bash

# Unity compile check script for Datra.Unity.Sample
# Runs Unity in batch mode to check for compilation errors

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
UNITY_PROJECT="$PROJECT_ROOT/Datra.Unity.Sample"

# Unity installation path - auto-detect from ProjectVersion.txt
UNITY_VERSION=$(grep "m_EditorVersion:" "$UNITY_PROJECT/ProjectSettings/ProjectVersion.txt" | cut -d' ' -f2)
UNITY_PATH="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}=== Unity Compile Check ===${NC}"
echo "Unity Version: $UNITY_VERSION"
echo "Unity Path: $UNITY_PATH"
echo "Project: $UNITY_PROJECT"
echo ""

# Check if Unity exists
if [ ! -f "$UNITY_PATH" ]; then
    echo -e "${RED}Error: Unity not found at $UNITY_PATH${NC}"
    echo "Please install Unity $UNITY_VERSION or update the script."
    exit 1
fi

# Create temp log file
LOG_FILE=$(mktemp)
trap "rm -f $LOG_FILE" EXIT

echo "Running Unity compile check..."
echo "(This may take a while on first run)"
echo ""

# Run Unity in batch mode
# -quit: Exit after done
# -batchmode: No GUI
# -nographics: No GPU (faster)
# -logFile: Output log to file
# -projectPath: Project to open
"$UNITY_PATH" \
    -batchmode \
    -nographics \
    -quit \
    -projectPath "$UNITY_PROJECT" \
    -logFile "$LOG_FILE" \
    2>&1 || true

# Check for Unity already running
if grep -q "another Unity instance is running\|Multiple Unity instances" "$LOG_FILE"; then
    echo -e "${YELLOW}=== Unity Already Running ===${NC}"
    echo ""
    echo "Unity Editor is currently open with this project."
    echo "Please close Unity Editor and run this script again,"
    echo "or run tests directly from Unity Test Runner."
    echo ""
    exit 2  # Special exit code for "Unity running"
fi

# Check for compilation errors in log
if grep -q "Compilation failed\|error CS" "$LOG_FILE"; then
    echo -e "${RED}=== Compilation FAILED ===${NC}"
    echo ""
    echo "Errors found:"
    grep -E "error CS[0-9]+:|Compilation failed" "$LOG_FILE" | head -30
    echo ""
    echo "Full log: $LOG_FILE"
    # Don't delete log file on error
    trap - EXIT
    cp "$LOG_FILE" "$PROJECT_ROOT/unity-compile-error.log"
    echo "Log saved to: $PROJECT_ROOT/unity-compile-error.log"
    exit 1
else
    echo -e "${GREEN}=== Compilation SUCCESS ===${NC}"
    echo "No compilation errors found."
    exit 0
fi
