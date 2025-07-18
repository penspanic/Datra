#!/bin/bash

# Script to build both Datra.Data.Generators and Datra.Data.Analyzers

# Color definitions
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check project root directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"

echo -e "${BLUE}=== Starting build for all Datra components ===${NC}"
echo ""

# Build Source Generator
echo -e "${YELLOW}[1/2] Building Source Generator...${NC}"
"$SCRIPT_DIR/build-source-generator.sh"
if [ $? -ne 0 ]; then
    echo -e "${RED}✗ Source Generator build failed!${NC}"
    exit 1
fi
echo ""

# Build Analyzer
echo -e "${YELLOW}[2/2] Building Analyzer...${NC}"
"$SCRIPT_DIR/build-analyzer.sh"
if [ $? -ne 0 ]; then
    echo -e "${RED}✗ Analyzer build failed!${NC}"
    exit 1
fi
echo ""

echo -e "${GREEN}=== All builds completed successfully! ===${NC}"
echo -e "${BLUE}Summary:${NC}"
echo -e "  ${GREEN}✓${NC} Datra.Data.Generators.dll"
echo -e "  ${GREEN}✓${NC} Datra.Data.Analyzers.dll"
echo ""
echo -e "${YELLOW}Both DLLs have been copied to: $PROJECT_ROOT/Datra.Data/Plugins${NC}"