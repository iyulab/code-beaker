@echo off
REM Runtime Docker 이미지 빌드 스크립트 (Windows)

echo === Building CodeBeaker Runtime Images ===
echo.

REM Python 런타임 빌드
echo Building Python runtime...
docker build -t codebeaker-python:latest docker/runtimes/python/
if %errorlevel% neq 0 exit /b %errorlevel%
echo . Python runtime built successfully
echo.

REM JavaScript/Node.js 런타임 빌드
echo Building Node.js runtime...
docker build -t codebeaker-nodejs:latest docker/runtimes/nodejs/
if %errorlevel% neq 0 exit /b %errorlevel%
echo . Node.js runtime built successfully
echo.

REM C# 런타임 빌드
echo Building C# runtime...
docker build -t codebeaker-csharp:latest docker/runtimes/csharp/
if %errorlevel% neq 0 exit /b %errorlevel%
echo . C# runtime built successfully
echo.

REM Go 런타임 빌드
echo Building Go runtime...
docker build -t codebeaker-golang:latest docker/runtimes/golang/
if %errorlevel% neq 0 exit /b %errorlevel%
echo . Go runtime built successfully
echo.

echo === All runtime images built successfully ===
echo.
echo Images:
docker images | findstr codebeaker
