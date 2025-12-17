#!/bin/bash

# Integrated test script for Datra
# Runs all tests: .NET unit tests + Unity compilation + Unity tests

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Parse arguments
SKIP_BUILD=false
SKIP_DOTNET=false
SKIP_UNITY=false
UNITY_TESTS_ONLY=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --skip-dotnet)
            SKIP_DOTNET=true
            shift
            ;;
        --skip-unity)
            SKIP_UNITY=true
            shift
            ;;
        --unity-only)
            SKIP_BUILD=true
            SKIP_DOTNET=true
            shift
            ;;
        --help)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --skip-build    Skip DLL build step"
            echo "  --skip-dotnet   Skip .NET unit tests"
            echo "  --skip-unity    Skip Unity tests"
            echo "  --unity-only    Only run Unity tests (skip build and .NET)"
            echo "  --help          Show this help"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage"
            exit 1
            ;;
    esac
done

echo -e "${YELLOW}╔══════════════════════════════════════╗${NC}"
echo -e "${YELLOW}║       Datra Full Test Suite          ║${NC}"
echo -e "${YELLOW}╚══════════════════════════════════════╝${NC}"
echo ""

FAILED=false

# Step 1: Build DLLs
if [ "$SKIP_BUILD" = false ]; then
    echo -e "${BLUE}[1/3] Building Generator DLLs...${NC}"
    if "$SCRIPT_DIR/build-all.sh"; then
        echo -e "${GREEN}✓ Build successful${NC}"
    else
        echo -e "${RED}✗ Build failed${NC}"
        exit 1
    fi
    echo ""
else
    echo -e "${BLUE}[1/3] Skipping build${NC}"
    echo ""
fi

# Step 2: .NET Tests
if [ "$SKIP_DOTNET" = false ]; then
    echo -e "${BLUE}[2/3] Running .NET Unit Tests...${NC}"
    cd "$PROJECT_ROOT"
    if dotnet test Datra.Tests/Datra.Tests.csproj --verbosity minimal; then
        echo -e "${GREEN}✓ .NET tests passed${NC}"
    else
        echo -e "${RED}✗ .NET tests failed${NC}"
        FAILED=true
    fi
    echo ""
else
    echo -e "${BLUE}[2/3] Skipping .NET tests${NC}"
    echo ""
fi

# Step 3: Unity Tests
if [ "$SKIP_UNITY" = false ]; then
    echo -e "${BLUE}[3/3] Running Unity Tests...${NC}"
    "$SCRIPT_DIR/test-unity.sh" --skip-compile
    UNITY_RESULT=$?
    if [ $UNITY_RESULT -eq 0 ]; then
        echo -e "${GREEN}✓ Unity tests passed${NC}"
    elif [ $UNITY_RESULT -eq 2 ]; then
        echo -e "${YELLOW}⚠ Unity Editor is running - skipped batch tests${NC}"
        echo "  Run tests from Unity Editor's Test Runner window."
    else
        echo -e "${RED}✗ Unity tests failed${NC}"
        FAILED=true
    fi
    echo ""
else
    echo -e "${BLUE}[3/3] Skipping Unity tests${NC}"
    echo ""
fi

# Summary
echo -e "${YELLOW}╔══════════════════════════════════════╗${NC}"
if [ "$FAILED" = true ]; then
    echo -e "${YELLOW}║${NC}          ${RED}TESTS FAILED${NC}               ${YELLOW}║${NC}"
    echo -e "${YELLOW}╚══════════════════════════════════════╝${NC}"
    exit 1
else
    echo -e "${YELLOW}║${NC}       ${GREEN}ALL TESTS PASSED${NC}             ${YELLOW}║${NC}"
    echo -e "${YELLOW}╚══════════════════════════════════════╝${NC}"
    exit 0
fi
