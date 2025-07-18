#!/bin/bash

# Script to build Datra.Data.Analyzers and copy to Unity project

# Color definitions
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check project root directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"

echo -e "${YELLOW}=== Starting Datra.Data.Analyzers build ===${NC}"

# Move to Datra.Data.Analyzers directory
cd "$PROJECT_ROOT/Datra.Data.Analyzers" || exit 1

# Execute build (Release mode)
echo -e "${GREEN}→ Building in Release mode...${NC}"
dotnet build -c Release

if [ $? -ne 0 ]; then
    echo -e "${RED}✗ Build failed!${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Build successful!${NC}"

# Build output path
SOURCE_DLL="$PROJECT_ROOT/../Output/Datra.Data.Analyzers/bin/Release/netstandard2.1/Datra.Data.Analyzers.dll"

# Datra.Data package Plugins directory
UNITY_PLUGINS_DIR="$PROJECT_ROOT/Datra.Data/Plugins"

# Create Plugins directory if it doesn't exist
if [ ! -d "$UNITY_PLUGINS_DIR" ]; then
    echo -e "${YELLOW}→ Creating Plugins directory...${NC}"
    mkdir -p "$UNITY_PLUGINS_DIR"
fi

# Check DLL file
if [ ! -f "$SOURCE_DLL" ]; then
    echo -e "${RED}✗ Cannot find built DLL file: $SOURCE_DLL${NC}"
    exit 1
fi

# Copy DLL
echo -e "${GREEN}→ Copying DLL to Datra.Data package...${NC}"
cp "$SOURCE_DLL" "$UNITY_PLUGINS_DIR/"

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Copy completed!${NC}"
    echo -e "${GREEN}  Source: $SOURCE_DLL${NC}"
    echo -e "${GREEN}  Target: $UNITY_PLUGINS_DIR/Datra.Data.Analyzers.dll${NC}"
else
    echo -e "${RED}✗ Copy failed!${NC}"
    exit 1
fi

echo -e "${GREEN}=== Complete! ===${NC}"
echo -e "${YELLOW}Please check the following in Unity Editor:${NC}"
echo -e "  1. Select Assets/Plugins/Datra.Data.Analyzers.dll"
echo -e "  2. Check if 'RoslynAnalyzer' label is set in Inspector"
echo -e "  3. Check if all platforms are disabled in Platform settings"