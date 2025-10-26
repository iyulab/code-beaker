# CodeBeaker 빠른 시작 스크립트 (Windows)
# API와 Worker를 동시에 실행합니다

param(
    [switch]$ApiOnly,
    [switch]$WorkerOnly
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CodeBeaker 로컬 개발 서버 시작" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 기존 프로세스 종료
Write-Host "기존 dotnet 프로세스 확인 중..." -ForegroundColor Yellow
$processes = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "*CodeBeaker*" }
if ($processes) {
    Write-Host "기존 CodeBeaker 프로세스 $($processes.Count)개 발견. 종료 중..." -ForegroundColor Yellow
    $processes | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# 큐/저장소 디렉토리 확인
$queuePath = "$env:TEMP\codebeaker-queue"
$storagePath = "$env:TEMP\codebeaker-storage"

if (-not (Test-Path "$queuePath\pending")) {
    Write-Host "⚠️ 큐 디렉토리가 없습니다. setup-local-dev.ps1을 먼저 실행하세요." -ForegroundColor Yellow
    Write-Host ""
    $response = Read-Host "지금 설정을 실행하시겠습니까? (y/n)"
    if ($response -eq 'y' -or $response -eq 'Y') {
        & "$PSScriptRoot\setup-local-dev.ps1"
    } else {
        exit 1
    }
}

Write-Host ""

# API 서버 시작
if (-not $WorkerOnly) {
    Write-Host "🚀 API 서버 시작 중..." -ForegroundColor Green
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot\..\src\CodeBeaker.API'; Write-Host '=== CodeBeaker API Server ===' -ForegroundColor Cyan; dotnet run"
    Write-Host "   API 서버가 새 창에서 실행됩니다." -ForegroundColor Cyan
    Write-Host "   Swagger UI: http://localhost:5039" -ForegroundColor Cyan
    Start-Sleep -Seconds 3
}

# Worker 서비스 시작
if (-not $ApiOnly) {
    Write-Host "⚙️ Worker 서비스 시작 중..." -ForegroundColor Green
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot\..\src\CodeBeaker.Worker'; Write-Host '=== CodeBeaker Worker Service ===' -ForegroundColor Cyan; dotnet run"
    Write-Host "   Worker 서비스가 새 창에서 실행됩니다." -ForegroundColor Cyan
    Start-Sleep -Seconds 2
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ CodeBeaker 로컬 서버 실행 중!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

if (-not $WorkerOnly) {
    Write-Host "📡 API Endpoints:" -ForegroundColor Yellow
    Write-Host "   Swagger UI:     http://localhost:5039" -ForegroundColor White
    Write-Host "   Health Check:   http://localhost:5039/health" -ForegroundColor White
    Write-Host "   Languages:      http://localhost:5039/api/language" -ForegroundColor White
    Write-Host "   Execute Code:   POST http://localhost:5039/api/execution" -ForegroundColor White
    Write-Host ""
}

Write-Host "💡 테스트 가이드:" -ForegroundColor Yellow
Write-Host "   .\scripts\test-examples.ps1" -ForegroundColor White
Write-Host ""

Write-Host "🛑 종료하려면:" -ForegroundColor Yellow
Write-Host "   열린 PowerShell 창에서 Ctrl+C를 누르세요" -ForegroundColor White
Write-Host ""
