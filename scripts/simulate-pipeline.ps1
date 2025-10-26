# CodeBeaker 전체 파이프라인 로컬 시뮬레이션
# API + Worker를 실행하고 실제 코드 실행을 테스트합니다

param(
    [int]$TestCount = 10,
    [int]$Timeout = 60,
    [switch]$SkipBuild,
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

# 색상 출력 함수
function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "▶ $Message" -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor Gray
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Fail {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ️  $Message" -ForegroundColor Yellow
}

function Write-Progress-Custom {
    param([string]$Message, [int]$Current, [int]$Total)
    $percent = [math]::Round(($Current / $Total) * 100)
    $bar = "#" * [math]::Floor($percent / 5)
    $space = " " * (20 - $bar.Length)
    Write-Host "`r[$bar$space] $percent% - $Message" -NoNewline -ForegroundColor Cyan
}

# 전역 통계
$script:Stats = @{
    TotalTests = 0
    Passed = 0
    Failed = 0
    StartTime = Get-Date
    Results = @()
}

Write-Host @"
╔════════════════════════════════════════════════════════════╗
║         CodeBeaker 파이프라인 시뮬레이션                  ║
╚════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Step 1: 환경 확인
Write-Step "1. 환경 검증"

# .NET SDK 확인
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Fail ".NET SDK가 설치되지 않았습니다"
    exit 1
}
Write-Success ".NET SDK $dotnetVersion 설치됨"

# Docker 확인
docker info 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Docker가 실행되지 않습니다"
    exit 1
}
Write-Success "Docker 실행 중"

# Docker 이미지 확인
$images = @("codebeaker-python", "codebeaker-nodejs", "codebeaker-golang", "codebeaker-dotnet")
$missingImages = @()
foreach ($img in $images) {
    $exists = docker images -q "${img}:latest"
    if (-not $exists) {
        $missingImages += $img
    }
}

if ($missingImages.Count -gt 0) {
    Write-Fail "Docker 이미지 누락: $($missingImages -join ', ')"
    Write-Info "이미지 빌드: .\scripts\build-runtime-images.ps1"
    exit 1
}
Write-Success "모든 Docker 런타임 이미지 존재"

# Step 2: 빌드
if (-not $SkipBuild) {
    Write-Step "2. 프로젝트 빌드"

    dotnet build -c Release --nologo -v minimal 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "빌드 실패"
        exit 1
    }
    Write-Success "빌드 완료"
} else {
    Write-Info "빌드 스킵"
}

# Step 3: 기존 프로세스 정리
Write-Step "3. 기존 프로세스 정리"

# CodeBeaker 관련 dotnet 프로세스 종료
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like "*CodeBeaker*" } |
    Stop-Process -Force -ErrorAction SilentlyContinue

# 포트 5039, 5050 사용 중인 프로세스 종료
$portsToCheck = @(5039, 5050)
foreach ($port in $portsToCheck) {
    $connections = netstat -ano | Select-String ":$port\s" | ForEach-Object {
        if ($_ -match "\s+(\d+)\s*$") {
            $matches[1]
        }
    }
    $connections | Select-Object -Unique | ForEach-Object {
        try {
            Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
        } catch {}
    }
}

Start-Sleep -Seconds 2
Write-Success "기존 프로세스 정리 완료"

# Step 4: 큐/저장소 디렉토리 초기화
Write-Step "4. 큐/저장소 초기화"

$queuePath = "$env:TEMP\codebeaker-queue-sim"
$storagePath = "$env:TEMP\codebeaker-storage-sim"

if (Test-Path $queuePath) { Remove-Item -Recurse -Force $queuePath }
if (Test-Path $storagePath) { Remove-Item -Recurse -Force $storagePath }

New-Item -ItemType Directory -Path "$queuePath\pending" -Force | Out-Null
New-Item -ItemType Directory -Path "$queuePath\processing" -Force | Out-Null
New-Item -ItemType Directory -Path "$queuePath\completed" -Force | Out-Null
New-Item -ItemType Directory -Path $storagePath -Force | Out-Null

Write-Success "큐/저장소 초기화 완료"

# Step 5: API 서버 시작
Write-Step "5. API 서버 시작"

$apiPort = 5050
$env:ASPNETCORE_URLS = "http://localhost:$apiPort"
$env:Queue__Path = $queuePath
$env:Storage__Path = $storagePath

$apiJob = Start-Job -ScriptBlock {
    param($apiPort, $queuePath, $storagePath, $apiDir)
    $env:ASPNETCORE_URLS = "http://localhost:$apiPort"
    $env:Queue__Path = $queuePath
    $env:Storage__Path = $storagePath
    Set-Location $apiDir
    dotnet run --no-build --no-launch-profile -c Release 2>&1
} -ArgumentList $apiPort, $queuePath, $storagePath, "$PSScriptRoot\..\src\CodeBeaker.API"

Write-Info "API 서버 시작 중... (Job ID: $($apiJob.Id))"
Start-Sleep -Seconds 8

# API Health Check
$maxRetries = 10
$retryCount = 0
$apiReady = $false

while ($retryCount -lt $maxRetries -and -not $apiReady) {
    try {
        $health = Invoke-RestMethod -Uri "http://localhost:$apiPort/health" -Method Get -TimeoutSec 2
        $apiReady = $true
        Write-Success "API 서버 준비 완료 (http://localhost:$apiPort)"
    }
    catch {
        $retryCount++
        Write-Host "." -NoNewline
        Start-Sleep -Seconds 1
    }
}

if (-not $apiReady) {
    Write-Fail "API 서버 시작 실패"

    # Job 출력 확인
    if ($apiJob.State -eq "Failed" -or $apiJob.State -eq "Completed") {
        $jobOutput = Receive-Job $apiJob -ErrorAction SilentlyContinue
        Write-Host "API Job Output:" -ForegroundColor Yellow
        $jobOutput | ForEach-Object { Write-Host $_ }
    }

    Stop-Job $apiJob -ErrorAction SilentlyContinue
    Remove-Job $apiJob -Force -ErrorAction SilentlyContinue
    exit 1
}

# Step 6: Worker 서비스 시작
Write-Step "6. Worker 서비스 시작"

$workerJob = Start-Job -ScriptBlock {
    param($queuePath, $storagePath, $workerDir)
    $env:Queue__Path = $queuePath
    $env:Storage__Path = $storagePath
    Set-Location $workerDir
    dotnet run --no-build --no-launch-profile -c Release 2>&1
} -ArgumentList $queuePath, $storagePath, "$PSScriptRoot\..\src\CodeBeaker.Worker"

Write-Info "Worker 서비스 시작 중... (Job ID: $($workerJob.Id))"
Start-Sleep -Seconds 3
Write-Success "Worker 서비스 시작 완료"

# Step 7: 테스트 시나리오 실행
Write-Step "7. 코드 실행 테스트 ($TestCount개)"

$testCases = @(
    @{
        Name = "Python Hello World"
        Language = "python"
        Code = "print('Hello from Python!')"
        ExpectedOutput = "Hello from Python!"
    },
    @{
        Name = "Python Loop"
        Language = "python"
        Code = @"
for i in range(3):
    print(f'Count: {i}')
"@
        ExpectedOutput = "Count: 0"
    },
    @{
        Name = "JavaScript Console"
        Language = "javascript"
        Code = "console.log('Hello from Node.js!');"
        ExpectedOutput = "Hello from Node.js!"
    },
    @{
        Name = "JavaScript Array"
        Language = "javascript"
        Code = @"
const arr = [1, 2, 3];
const sum = arr.reduce((a, b) => a + b, 0);
console.log('Sum:', sum);
"@
        ExpectedOutput = "Sum: 6"
    },
    @{
        Name = "Go Hello World"
        Language = "go"
        Code = @"
package main
import "fmt"
func main() {
    fmt.Println("Hello from Go!")
}
"@
        ExpectedOutput = "Hello from Go!"
    },
    @{
        Name = "Go Math"
        Language = "go"
        Code = @"
package main
import "fmt"
func main() {
    result := 10 + 20
    fmt.Printf("Result: %d\n", result)
}
"@
        ExpectedOutput = "Result: 30"
    },
    @{
        Name = "C# Hello World"
        Language = "csharp"
        Code = "Console.WriteLine(""Hello from C#!"");"
        ExpectedOutput = "Hello from C#!"
    },
    @{
        Name = "C# LINQ"
        Language = "csharp"
        Code = @"
var numbers = new[] { 1, 2, 3, 4, 5 };
var sum = numbers.Sum();
Console.WriteLine($"Sum: {sum}");
"@
        ExpectedOutput = "Sum: 15"
    },
    @{
        Name = "Python Error Handling"
        Language = "python"
        Code = @"
try:
    result = 10 / 2
    print(f'Result: {result}')
except Exception as e:
    print(f'Error: {e}')
"@
        ExpectedOutput = "Result: 5"
    },
    @{
        Name = "JavaScript Timeout"
        Language = "javascript"
        Code = @"
console.log('Starting...');
setTimeout(() => console.log('This should not appear'), 10000);
console.log('Immediate output');
"@
        ExpectedOutput = "Starting..."
    }
)

# 실행할 테스트 케이스 선택
$selectedTests = $testCases | Get-Random -Count ([Math]::Min($TestCount, $testCases.Count))

Write-Host ""
foreach ($i in 0..($selectedTests.Count - 1)) {
    $test = $selectedTests[$i]
    $script:Stats.TotalTests++

    Write-Progress-Custom -Message $test.Name -Current ($i + 1) -Total $selectedTests.Count

    try {
        # 코드 실행 요청
        $requestBody = @{
            code = $test.Code
            language = $test.Language
        } | ConvertTo-Json

        $submitResponse = Invoke-RestMethod -Uri "http://localhost:$apiPort/api/execution" `
            -Method Post `
            -ContentType "application/json" `
            -Body $requestBody `
            -TimeoutSec 5

        $executionId = $submitResponse.executionId

        # 결과 대기 (초 단위)
        $maxWaitSeconds = $Timeout
        $waitedSeconds = 0
        $result = $null

        while ($waitedSeconds -lt $maxWaitSeconds) {
            Start-Sleep -Seconds 1
            $waitedSeconds++

            try {
                $statusResponse = Invoke-RestMethod -Uri "http://localhost:$apiPort/api/execution/$executionId" `
                    -Method Get `
                    -TimeoutSec 2

                if ($statusResponse.status -eq "completed") {
                    $result = $statusResponse
                    break
                }
                elseif ($statusResponse.status -eq "failed") {
                    $result = $statusResponse
                    break
                }
            }
            catch {
                # 아직 결과 없음
            }
        }

        # 결과 검증
        if ($result -and $result.status -eq "completed" -and $result.exitCode -eq 0) {
            $outputMatch = $result.stdout -match [regex]::Escape($test.ExpectedOutput)

            if ($outputMatch) {
                $script:Stats.Passed++
                $script:Stats.Results += @{
                    Name = $test.Name
                    Status = "PASS"
                    Duration = $result.durationMs
                    Language = $test.Language
                }

                if ($Verbose) {
                    Write-Host ""
                    Write-Success "$($test.Name) - $($result.durationMs)ms"
                }
            }
            else {
                $script:Stats.Failed++
                $script:Stats.Results += @{
                    Name = $test.Name
                    Status = "FAIL"
                    Reason = "Output mismatch"
                    Expected = $test.ExpectedOutput
                    Actual = $result.stdout
                }

                Write-Host ""
                Write-Fail "$($test.Name) - Output mismatch"
            }
        }
        else {
            $script:Stats.Failed++
            $reason = if ($result) {
                if ($result.errorType) { $result.errorType }
                elseif ($result.status -eq "failed") { "execution_failed" }
                else { $result.status }
            } else {
                "timeout_error"
            }

            $script:Stats.Results += @{
                Name = $test.Name
                Status = "FAIL"
                Reason = $reason
                ExecutionId = $executionId
                ResultStatus = if ($result) { $result.status } else { "none" }
            }

            Write-Host ""
            Write-Fail "$($test.Name) - $reason"
            if ($Verbose -and $result) {
                Write-Host "  Status: $($result.status)" -ForegroundColor Gray
                if ($result.stderr) {
                    Write-Host "  Stderr: $($result.stderr)" -ForegroundColor Gray
                }
            }
        }
    }
    catch {
        $script:Stats.Failed++
        $script:Stats.Results += @{
            Name = $test.Name
            Status = "ERROR"
            Reason = $_.Exception.Message
        }

        Write-Host ""
        Write-Fail "$($test.Name) - $($_.Exception.Message)"
    }
}

Write-Host "" # 프로그레스 바 줄바꿈
Write-Host ""

# Step 8: 프로세스 종료
Write-Step "8. 프로세스 정리"

Stop-Job $apiJob -ErrorAction SilentlyContinue
Stop-Job $workerJob -ErrorAction SilentlyContinue
Remove-Job $apiJob -Force -ErrorAction SilentlyContinue
Remove-Job $workerJob -Force -ErrorAction SilentlyContinue

Start-Sleep -Seconds 2
Write-Success "모든 프로세스 종료 완료"

# Step 9: 결과 요약
Write-Step "9. 테스트 결과 요약"

$endTime = Get-Date
$duration = $endTime - $script:Stats.StartTime

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                    최종 결과                               ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

Write-Host "총 테스트: " -NoNewline
Write-Host $script:Stats.TotalTests -ForegroundColor White

Write-Host "통과: " -NoNewline
Write-Host $script:Stats.Passed -ForegroundColor Green

Write-Host "실패: " -NoNewline
Write-Host $script:Stats.Failed -ForegroundColor Red

$passRate = if ($script:Stats.TotalTests -gt 0) {
    [math]::Round(($script:Stats.Passed / $script:Stats.TotalTests) * 100, 2)
} else { 0 }

Write-Host "통과율: " -NoNewline
$rateColor = if ($passRate -ge 90) { "Green" } elseif ($passRate -ge 70) { "Yellow" } else { "Red" }
Write-Host "$passRate%" -ForegroundColor $rateColor

Write-Host "총 소요 시간: " -NoNewline
Write-Host "$([math]::Round($duration.TotalSeconds, 2))초" -ForegroundColor Cyan

Write-Host ""
Write-Host "언어별 통계:" -ForegroundColor Yellow
$languageStats = $script:Stats.Results | Group-Object -Property Language | ForEach-Object {
    $passed = ($_.Group | Where-Object { $_.Status -eq "PASS" }).Count
    $total = $_.Count
    [PSCustomObject]@{
        Language = $_.Name
        Passed = $passed
        Total = $total
        Rate = if ($total -gt 0) { "$([math]::Round(($passed/$total)*100))%" } else { "0%" }
    }
}

$languageStats | Format-Table -AutoSize

if ($script:Stats.Failed -gt 0) {
    Write-Host ""
    Write-Host "실패한 테스트:" -ForegroundColor Red
    $script:Stats.Results | Where-Object { $_.Status -ne "PASS" } | ForEach-Object {
        Write-Host "  ❌ $($_.Name): $($_.Reason)" -ForegroundColor Red
    }
}

Write-Host ""
if ($script:Stats.Failed -eq 0) {
    Write-Host "🎉 모든 테스트 통과!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "⚠️  일부 테스트 실패" -ForegroundColor Yellow
    exit 1
}
