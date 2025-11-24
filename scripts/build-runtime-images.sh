#!/bin/bash
# CodeBeaker Runtime Images Build Script for Linux/Mac
# Builds all 4 runtime Docker images: Python, Node.js, Go, C#

set -e

NO_PULL=false
QUIET=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --no-pull)
            NO_PULL=true
            shift
            ;;
        --quiet)
            QUIET=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--no-pull] [--quiet]"
            exit 1
            ;;
    esac
done

echo "🐳 CodeBeaker Runtime Images Builder"
echo ""

declare -a images=(
    "Python 3.12|codebeaker-python:latest|docker/runtimes/python"
    "Node.js 20|codebeaker-nodejs:latest|docker/runtimes/nodejs"
    "Go 1.21|codebeaker-golang:latest|docker/runtimes/golang"
    ".NET 8|codebeaker-dotnet:latest|docker/runtimes/csharp"
)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
cd "$ROOT_DIR"

for img in "${images[@]}"; do
    IFS='|' read -r NAME TAG PATH <<< "$img"

    echo "📦 Building $NAME..."

    BUILD_ARGS="build -t $TAG"
    if [ "$NO_PULL" = false ]; then
        BUILD_ARGS="$BUILD_ARGS --pull"
    fi
    if [ "$QUIET" = true ]; then
        BUILD_ARGS="$BUILD_ARGS -q"
    fi
    BUILD_ARGS="$BUILD_ARGS $PATH"

    if ! docker $BUILD_ARGS; then
        echo "❌ Failed to build $NAME"
        exit 1
    fi

    echo "✅ $NAME built successfully"
    echo ""
done

echo "🎉 All runtime images built successfully!"
echo ""
echo "📋 Built images:"
for img in "${images[@]}"; do
    IFS='|' read -r NAME TAG PATH <<< "$img"
    echo "   - $TAG"
done
