#!/bin/bash
# Runtime Docker 이미지 빌드 스크립트

set -e  # 오류 발생 시 중단

echo "=== Building CodeBeaker Runtime Images ==="
echo ""

# Python 런타임 빌드
echo "Building Python runtime..."
docker build -t codebeaker-python:latest docker/runtimes/python/
echo "✓ Python runtime built successfully"
echo ""

# JavaScript/Node.js 런타임 빌드
echo "Building Node.js runtime..."
docker build -t codebeaker-nodejs:latest docker/runtimes/nodejs/
echo "✓ Node.js runtime built successfully"
echo ""

# C# 런타임 빌드
echo "Building C# runtime..."
docker build -t codebeaker-csharp:latest docker/runtimes/csharp/
echo "✓ C# runtime built successfully"
echo ""

# Go 런타임 빌드
echo "Building Go runtime..."
docker build -t codebeaker-golang:latest docker/runtimes/golang/
echo "✓ Go runtime built successfully"
echo ""

echo "=== All runtime images built successfully ==="
echo ""
echo "Images:"
docker images | grep codebeaker
