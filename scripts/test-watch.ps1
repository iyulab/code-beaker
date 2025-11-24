# 파일 변경 감지 시 자동으로 테스트를 실행하는 스크립트

param(
    [ValidateSet("Core", "Runtime", "Integration", "All")]
    [string]$Target = "All"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CodeBeaker Test Watch 모드" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "타겟: $Target" -ForegroundColor Yellow
Write-Host "파일 변경 감지 시 자동으로 테스트가 실행됩니다." -ForegroundColor Cyan
Write-Host "종료하려면 Ctrl+C를 누르세요." -ForegroundColor Yellow
Write-Host ""

# 타겟별 테스트 프로젝트 경로 설정
$testProjects = switch ($Target) {
    "Core" { @("tests/CodeBeaker.Core.Tests/") }
    "Runtime" { @("tests/CodeBeaker.Runtimes.Tests/") }
    "Integration" { @("tests/CodeBeaker.Integration.Tests/") }
    "All" { @("tests/CodeBeaker.Core.Tests/", "tests/CodeBeaker.Runtimes.Tests/") }
}

# FileSystemWatcher 설정
$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = (Get-Location).Path
$watcher.IncludeSubdirectories = $true
$watcher.Filter = "*.cs"
$watcher.NotifyFilter = [System.IO.NotifyFilters]::LastWrite -bor [System.IO.NotifyFilters]::FileName

# 마지막 실행 시간 추적 (중복 실행 방지)
$script:lastRunTime = [DateTime]::MinValue
$script:debounceSeconds = 2

# 테스트 실행 함수
function Run-Tests {
    param([string]$ChangedFile)

    $now = [DateTime]::Now
    if (($now - $script:lastRunTime).TotalSeconds -lt $script:debounceSeconds) {
        return
    }
    $script:lastRunTime = $now

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "변경 감지: $ChangedFile" -ForegroundColor Yellow
    Write-Host "$(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Gray
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    foreach ($project in $testProjects) {
        $projectName = Split-Path $project -Leaf
        Write-Host "🧪 $projectName 테스트 실행 중..." -ForegroundColor Cyan

        dotnet test $project --no-build --logger "console;verbosity=minimal" 2>&1 | Out-Host

        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✅ $projectName 통과" -ForegroundColor Green
        } else {
            Write-Host "   ❌ $projectName 실패" -ForegroundColor Red
        }
    }

    Write-Host ""
    Write-Host "대기 중... (파일 변경 감지)" -ForegroundColor Gray
}

# 이벤트 핸들러 등록
$onChange = {
    param($sender, $e)
    Run-Tests -ChangedFile $e.FullPath
}

$onRename = {
    param($sender, $e)
    Run-Tests -ChangedFile $e.FullPath
}

Register-ObjectEvent -InputObject $watcher -EventName Changed -Action $onChange | Out-Null
Register-ObjectEvent -InputObject $watcher -EventName Renamed -Action $onRename | Out-Null

# Watcher 시작
$watcher.EnableRaisingEvents = $true

# 초기 테스트 실행
Write-Host "초기 테스트 실행 중..." -ForegroundColor Cyan
Write-Host ""
foreach ($project in $testProjects) {
    dotnet build $project --configuration Debug
}
Run-Tests -ChangedFile "Initial run"

# Ctrl+C 대기
try {
    while ($true) {
        Start-Sleep -Seconds 1
    }
}
finally {
    # 정리
    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()
    Get-EventSubscriber | Unregister-Event
    Write-Host ""
    Write-Host "Test Watch 모드 종료" -ForegroundColor Yellow
}
