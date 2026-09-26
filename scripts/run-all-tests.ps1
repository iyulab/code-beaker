# 모든 테스트를 실행하고 리포트를 생성하는 스크립트

param(
    [switch]$SkipIntegration,
    [switch]$WithCoverage,
    [switch]$GenerateReport
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CodeBeaker 테스트 자동화" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Continue"
$testsFailed = $false
$testResults = @()

# 1. 단위 테스트 - Core
Write-Host "1. Core 단위 테스트 실행 중..." -ForegroundColor Yellow

$coreTestCmd = "dotnet test tests/CodeBeaker.Core.Tests/ --configuration Release --logger 'console;verbosity=normal' --logger 'trx;LogFileName=core-test-results.trx' --results-directory ./TestResults"

if ($WithCoverage) {
    $coreTestCmd += " --collect:'XPlat Code Coverage'"
}

Invoke-Expression $coreTestCmd

if ($LASTEXITCODE -ne 0) {
    Write-Host "   ⚠️ Core 테스트 실패" -ForegroundColor Yellow
    $testsFailed = $true
    $testResults += @{ Name = "Core Tests"; Status = "Failed" }
} else {
    Write-Host "   ✅ Core 테스트 통과" -ForegroundColor Green
    $testResults += @{ Name = "Core Tests"; Status = "Passed" }
}
Write-Host ""

# 1b. 단위 테스트 - Commands
Write-Host "1b. Commands 단위 테스트 실행 중..." -ForegroundColor Yellow

$commandsTestCmd = "dotnet test tests/CodeBeaker.Commands.Tests/ --configuration Release --logger 'console;verbosity=normal' --logger 'trx;LogFileName=commands-test-results.trx' --results-directory ./TestResults"

if ($WithCoverage) {
    $commandsTestCmd += " --collect:'XPlat Code Coverage'"
}

Invoke-Expression $commandsTestCmd

if ($LASTEXITCODE -ne 0) {
    Write-Host "   ❌ Commands 테스트 실패" -ForegroundColor Red
    $testsFailed = $true
    $testResults += @{ Name = "Commands Tests"; Status = "Failed" }
} else {
    Write-Host "   ✅ Commands 테스트 통과" -ForegroundColor Green
    $testResults += @{ Name = "Commands Tests"; Status = "Passed" }
}
Write-Host ""

# 2. 단위 테스트 - Runtimes
Write-Host "2. Runtime 단위 테스트 실행 중..." -ForegroundColor Yellow

$runtimeTestCmd = "dotnet test tests/CodeBeaker.Runtimes.Tests/ --configuration Release --logger 'console;verbosity=normal' --logger 'trx;LogFileName=runtime-test-results.trx' --results-directory ./TestResults"

if ($WithCoverage) {
    $runtimeTestCmd += " --collect:'XPlat Code Coverage'"
}

Invoke-Expression $runtimeTestCmd

if ($LASTEXITCODE -ne 0) {
    Write-Host "   ❌ Runtime 테스트 실패" -ForegroundColor Red
    $testsFailed = $true
    $testResults += @{ Name = "Runtime Tests"; Status = "Failed" }
} else {
    Write-Host "   ✅ Runtime 테스트 통과" -ForegroundColor Green
    $testResults += @{ Name = "Runtime Tests"; Status = "Passed" }
}
Write-Host ""

# 3. 통합 테스트 - Integration
if (-not $SkipIntegration) {
    Write-Host "3. Integration 테스트 실행 중..." -ForegroundColor Yellow
    Write-Host "   Docker 이미지 확인 중..." -ForegroundColor Cyan

    $images = @("codebeaker-python", "codebeaker-nodejs", "codebeaker-golang", "codebeaker-dotnet")
    $allImagesExist = $true

    foreach ($image in $images) {
        $imageExists = docker images -q "$image:latest"
        if (-not $imageExists) {
            Write-Host "   ⚠️ $image 이미지가 없습니다" -ForegroundColor Yellow
            $allImagesExist = $false
        }
    }

    if ($allImagesExist) {
        $integrationTestCmd = "dotnet test tests/CodeBeaker.Integration.Tests/ --configuration Release --logger 'console;verbosity=normal' --logger 'trx;LogFileName=integration-test-results.trx' --results-directory ./TestResults"

        Invoke-Expression $integrationTestCmd

        if ($LASTEXITCODE -ne 0) {
            Write-Host "   ⚠️ Integration 테스트 실패" -ForegroundColor Yellow
            $testsFailed = $true
            $testResults += @{ Name = "Integration Tests"; Status = "Failed" }
        } else {
            Write-Host "   ✅ Integration 테스트 통과" -ForegroundColor Green
            $testResults += @{ Name = "Integration Tests"; Status = "Passed" }
        }
    } else {
        Write-Host "   ⏭️ Integration 테스트 스킵 (Docker 이미지 필요)" -ForegroundColor Yellow
        Write-Host "   이미지 빌드: .\scripts\build-runtime-images.ps1" -ForegroundColor Cyan
        $testResults += @{ Name = "Integration Tests"; Status = "Skipped" }
    }
    Write-Host ""
}

# 4. 커버리지 리포트 생성
if ($WithCoverage -and $GenerateReport) {
    Write-Host "4. 커버리지 리포트 생성 중..." -ForegroundColor Yellow

    # ReportGenerator 도구 설치 확인
    $reportGenInstalled = dotnet tool list -g | Select-String "reportgenerator"

    if (-not $reportGenInstalled) {
        Write-Host "   ReportGenerator 설치 중..." -ForegroundColor Cyan
        dotnet tool install -g dotnet-reportgenerator-globaltool
    }

    # 리포트 생성
    $coverageFiles = Get-ChildItem -Path ./TestResults -Filter "coverage.cobertura.xml" -Recurse

    if ($coverageFiles.Count -gt 0) {
        $reports = ($coverageFiles | ForEach-Object { $_.FullName }) -join ";"

        reportgenerator `
            -reports:"$reports" `
            -targetdir:"./TestResults/CoverageReport" `
            -reporttypes:"Html;Cobertura"

        Write-Host "   ✅ 커버리지 리포트 생성 완료" -ForegroundColor Green
        Write-Host "   리포트 위치: ./TestResults/CoverageReport/index.html" -ForegroundColor Cyan
        $testResults += @{ Name = "Coverage Report"; Status = "Generated" }
    } else {
        Write-Host "   ⚠️ 커버리지 파일을 찾을 수 없습니다" -ForegroundColor Yellow
    }
    Write-Host ""
}

# 5. 결과 요약
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "테스트 결과 요약" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

foreach ($result in $testResults) {
    $statusColor = switch ($result.Status) {
        "Passed" { "Green" }
        "Failed" { "Red" }
        "Skipped" { "Yellow" }
        "Generated" { "Cyan" }
        default { "White" }
    }

    $statusIcon = switch ($result.Status) {
        "Passed" { "✅" }
        "Failed" { "❌" }
        "Skipped" { "⏭️" }
        "Generated" { "📊" }
        default { "❓" }
    }

    Write-Host "$statusIcon $($result.Name): " -NoNewline
    Write-Host $result.Status -ForegroundColor $statusColor
}

Write-Host ""

# 6. 테스트 결과 파일 정보
$trxFiles = Get-ChildItem -Path ./TestResults -Filter "*.trx" -ErrorAction SilentlyContinue
if ($trxFiles.Count -gt 0) {
    Write-Host "📄 테스트 결과 파일:" -ForegroundColor Yellow
    foreach ($file in $trxFiles) {
        Write-Host "   - $($file.FullName)" -ForegroundColor Cyan
    }
    Write-Host ""
}

# 7. 종료 상태
if ($testsFailed) {
    Write-Host "❌ 일부 테스트가 실패했습니다!" -ForegroundColor Red
    exit 1
} else {
    Write-Host "✅ 모든 테스트 통과!" -ForegroundColor Green

    if ($WithCoverage) {
        Write-Host ""
        Write-Host "💡 커버리지 리포트를 보려면:" -ForegroundColor Yellow
        Write-Host "   .\scripts\run-all-tests.ps1 -WithCoverage -GenerateReport" -ForegroundColor White
    }

    exit 0
}
