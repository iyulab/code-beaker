#!/bin/bash
# CodeBeaker 로컬 개발 환경 설정 스크립트 (Linux/Mac)

set -e  # 에러 발생 시 스크립트 중단

SKIP_DOCKER_BUILD=false
SKIP_TESTS=false

# 인자 파싱
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-docker-build)
            SKIP_DOCKER_BUILD=true
            shift
            ;;
        --skip-tests)
            SKIP_TESTS=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo "========================================"
echo "CodeBeaker 로컬 개발 환경 설정"
echo "========================================"
echo ""

# 1. 사전 요구사항 확인
echo "1. 사전 요구사항 확인 중..."

# .NET SDK 확인
echo "   - .NET SDK 버전 확인..."
if ! command -v dotnet &> /dev/null; then
    echo "   ❌ .NET SDK가 설치되어 있지 않습니다!"
    echo "   다운로드: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi
DOTNET_VERSION=$(dotnet --version)
echo "   ✅ .NET SDK $DOTNET_VERSION 설치됨"

# Docker 확인
echo "   - Docker 실행 상태 확인..."
if ! docker info &> /dev/null; then
    echo "   ❌ Docker가 실행되지 않고 있습니다!"
    echo "   Docker를 시작해주세요: sudo systemctl start docker"
    exit 1
fi
echo "   ✅ Docker 실행 중"

echo ""

# 2. 프로젝트 빌드
echo "2. 프로젝트 빌드 중..."
dotnet restore
dotnet build -c Debug
echo "   ✅ 빌드 성공"
echo ""

# 3. 테스트 실행
if [ "$SKIP_TESTS" = false ]; then
    echo "3. 단위 테스트 실행 중..."
    if dotnet test --no-build; then
        echo "   ✅ 모든 테스트 통과"
    else
        echo "   ⚠️ 일부 테스트 실패 (Integration 테스트는 Docker 이미지 필요)"
    fi
    echo ""
fi

# 4. Docker 런타임 이미지 빌드
if [ "$SKIP_DOCKER_BUILD" = false ]; then
    echo "4. Docker 런타임 이미지 빌드 중..."
    echo "   이 작업은 5-10분 정도 소요될 수 있습니다..."

    declare -A images=(
        ["python"]="docker/runtimes/python"
        ["nodejs"]="docker/runtimes/nodejs"
        ["golang"]="docker/runtimes/golang"
        ["csharp"]="docker/runtimes/csharp"
    )

    for name in "${!images[@]}"; do
        path="${images[$name]}"
        echo "   - Building codebeaker-$name..."
        docker build -t "codebeaker-$name:latest" "$path"
    done
    echo "   ✅ 모든 Docker 이미지 빌드 완료"
    echo ""
fi

# 5. 큐/저장소 디렉토리 생성
echo "5. 큐/저장소 디렉토리 생성 중..."
QUEUE_PATH="/tmp/codebeaker-queue"
STORAGE_PATH="/tmp/codebeaker-storage"

rm -rf "$QUEUE_PATH" "$STORAGE_PATH"
mkdir -p "$QUEUE_PATH/pending"
mkdir -p "$QUEUE_PATH/processing"
mkdir -p "$QUEUE_PATH/completed"
mkdir -p "$STORAGE_PATH"

echo "   ✅ 디렉토리 생성 완료"
echo "   - Queue: $QUEUE_PATH"
echo "   - Storage: $STORAGE_PATH"
echo ""

# 6. 환경 준비 완료
echo "========================================"
echo "✅ 로컬 개발 환경 준비 완료!"
echo "========================================"
echo ""

echo "다음 단계:"
echo "1. Terminal 1에서 API 실행:"
echo "   cd src/CodeBeaker.API"
echo "   dotnet run"
echo ""
echo "2. Terminal 2에서 Worker 실행:"
echo "   cd src/CodeBeaker.Worker"
echo "   dotnet run"
echo ""
echo "3. 브라우저에서 Swagger UI 접속:"
echo "   http://localhost:5039"
echo ""
echo "또는 빠른 시작 스크립트 사용:"
echo "   ./scripts/start-dev.sh"
echo ""
