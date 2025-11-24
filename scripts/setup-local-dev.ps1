# CodeBeaker 로컬 개발 환경 설정 스크립트 (Windows)
# PowerShell 7+ 권장

param(
    [switch]$SkipDockerBuild,
    [switch]$SkipTests
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CodeBeaker 로컬 개발 환경 설정" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 사전 요구사항 확인
Write-Host "1. 사전 요구사항 확인 중..." -ForegroundColor Yellow

# .NET SDK 확인
Write-Host "   - .NET SDK 버전 확인..."
$dotnetVersion = dotnet --version
if ($LASTEXITCODE -ne 0) {
    Write-Host "   ❌ .NET SDK가 설치되어 있지 않습니다!" -ForegroundColor Red
    Write-Host "   다운로드: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    exit 1
}
Write-Host "   ✅ .NET SDK $dotnetVersion 설치됨" -ForegroundColor Green

# Docker 확인
Write-Host "   - Docker 실행 상태 확인..."
docker info > $null 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "   ❌ Docker가 실행되지 않고 있습니다!" -ForegroundColor Red
    Write-Host "   Docker Desktop을 실행해주세요." -ForegroundColor Yellow
    exit 1
}
Write-Host "   ✅ Docker 실행 중" -ForegroundColor Green

Write-Host ""

# 2. 프로젝트 빌드
Write-Host "2. 프로젝트 빌드 중..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "   ❌ NuGet 복원 실패!" -ForegroundColor Red
    exit 1
}

dotnet build -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "   ❌ 빌드 실패!" -ForegroundColor Red
    exit 1
}
Write-Host "   ✅ 빌드 성공" -ForegroundColor Green
Write-Host ""

# 3. 테스트 실행
if (-not $SkipTests) {
    Write-Host "3. 단위 테스트 실행 중..." -ForegroundColor Yellow
    dotnet test --no-build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   ⚠️ 일부 테스트 실패 (Integration 테스트는 Docker 이미지 필요)" -ForegroundColor Yellow
    } else {
        Write-Host "   ✅ 모든 테스트 통과" -ForegroundColor Green
    }
    Write-Host ""
}

# 4. Docker 런타임 이미지 빌드
if (-not $SkipDockerBuild) {
    Write-Host "4. Docker 런타임 이미지 빌드 중..." -ForegroundColor Yellow
    Write-Host "   이 작업은 5-10분 정도 소요될 수 있습니다..." -ForegroundColor Cyan

    $images = @(
        @{Name="python"; Path="docker/runtimes/python"},
        @{Name="nodejs"; Path="docker/runtimes/nodejs"},
        @{Name="golang"; Path="docker/runtimes/golang"},
        @{Name="csharp"; Path="docker/runtimes/csharp"}
    )

    foreach ($img in $images) {
        Write-Host "   - Building codebeaker-$($img.Name)..." -ForegroundColor Cyan
        docker build -t "codebeaker-$($img.Name):latest" $img.Path
        if ($LASTEXITCODE -ne 0) {
            Write-Host "   ❌ codebeaker-$($img.Name) 빌드 실패!" -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "   ✅ 모든 Docker 이미지 빌드 완료" -ForegroundColor Green
    Write-Host ""
}

# 5. 큐/저장소 디렉토리 생성
Write-Host "5. 큐/저장소 디렉토리 생성 중..." -ForegroundColor Yellow
$queuePath = "$env:TEMP\codebeaker-queue"
$storagePath = "$env:TEMP\codebeaker-storage"

if (Test-Path $queuePath) {
    Remove-Item -Recurse -Force $queuePath
}
if (Test-Path $storagePath) {
    Remove-Item -Recurse -Force $storagePath
}

New-Item -ItemType Directory -Path $queuePath -Force > $null
New-Item -ItemType Directory -Path "$queuePath\pending" -Force > $null
New-Item -ItemType Directory -Path "$queuePath\processing" -Force > $null
New-Item -ItemType Directory -Path "$queuePath\completed" -Force > $null
New-Item -ItemType Directory -Path $storagePath -Force > $null

Write-Host "   ✅ 디렉토리 생성 완료" -ForegroundColor Green
Write-Host "   - Queue: $queuePath" -ForegroundColor Cyan
Write-Host "   - Storage: $storagePath" -ForegroundColor Cyan
Write-Host ""

# 6. 환경 준비 완료
Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ 로컬 개발 환경 준비 완료!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

Write-Host "다음 단계:" -ForegroundColor Yellow
Write-Host "1. Terminal 1에서 API 실행:" -ForegroundColor Cyan
Write-Host "   cd src\CodeBeaker.API" -ForegroundColor White
Write-Host "   dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "2. Terminal 2에서 Worker 실행:" -ForegroundColor Cyan
Write-Host "   cd src\CodeBeaker.Worker" -ForegroundColor White
Write-Host "   dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "3. 브라우저에서 Swagger UI 접속:" -ForegroundColor Cyan
Write-Host "   http://localhost:5039" -ForegroundColor White
Write-Host ""
Write-Host "또는 빠른 시작 스크립트 사용:" -ForegroundColor Yellow
Write-Host "   .\scripts\start-dev.ps1" -ForegroundColor White
Write-Host ""
