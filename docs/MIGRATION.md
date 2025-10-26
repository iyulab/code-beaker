# Python to C# Migration Roadmap

**목표**: 4주 내 C# 기반 고성능 코드 실행 플랫폼 구축

---

## Week 1: 기반 구축 (Foundation)

### Day 1-2: 프로젝트 설정
```bash
# .NET 솔루션 생성
dotnet new sln -n CodeBeaker

# 프로젝트 생성
dotnet new classlib -n CodeBeaker.Core -f net8.0
dotnet new classlib -n CodeBeaker.Runtimes -f net8.0
dotnet new webapi -n CodeBeaker.API -f net8.0
dotnet new worker -n CodeBeaker.Worker -f net8.0

# 테스트 프로젝트
dotnet new xunit -n CodeBeaker.Core.Tests -f net8.0
dotnet new xunit -n CodeBeaker.Integration.Tests -f net8.0

# 솔루션에 추가
dotnet sln add **/*.csproj

# 필수 패키지 설치
cd CodeBeaker.Core
dotnet add package Docker.DotNet
dotnet add package System.Text.Json

cd ../CodeBeaker.API
dotnet add package Swashbuckle.AspNetCore

cd ../CodeBeaker.Core.Tests
dotnet add package FluentAssertions
dotnet add package Moq
```

**체크포인트**: 빌드 성공, 테스트 프로젝트 실행

---

### Day 3-4: Core 라이브러리

**구현 순서**:
1. `Models/` - ExecutionConfig, ExecutionResult, TaskItem
2. `Interfaces/` - IQueue, IStorage, IRuntime
3. `Queue/FileQueue.cs` - 파일 기반 큐 (Python 코드 이식)
4. `Storage/FileStorage.cs` - 파일 기반 저장소
5. 단위 테스트 작성 (80% 커버리지)

**테스트 예시**:
```csharp
[Fact]
public async Task FileQueue_SubmitAndGet_WorksCorrectly()
{
    var queue = new FileQueue(tempDir);

    var id = await queue.SubmitTaskAsync(
        "print('test')",
        "python",
        new ExecutionConfig());

    var task = await queue.GetTaskAsync(TimeSpan.FromSeconds(1));

    task.Should().NotBeNull();
    task.ExecutionId.Should().Be(id);
}
```

**체크포인트**: Core 테스트 100% 통과

---

### Day 5-7: Docker 실행기

**구현**:
```csharp
// CodeBeaker.Core/Docker/DockerExecutor.cs
public class DockerExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(
        string image,
        string[] command,
        string workspaceDir,
        ExecutionConfig config,
        CancellationToken ct)
    {
        // 1. 컨테이너 생성
        // 2. 시작 + 타임아웃 처리
        // 3. 로그 수집
        // 4. 정리
    }
}
```

**테스트**:
- Python "Hello World" 실행
- 타임아웃 처리
- 메모리 제한
- 네트워크 격리

**체크포인트**: Docker 실행 테스트 통과

---

## Week 2: 런타임 & API (Runtimes & API)

### Day 8-10: 런타임 어댑터

**구현 순서**:
1. `BaseRuntime.cs` - 추상 클래스
2. `PythonRuntime.cs` - Python 실행
3. `JavaScriptRuntime.cs` - Node.js 실행
4. `GoRuntime.cs` - Go 실행
5. `RuntimeRegistry.cs` - 런타임 관리

**각 런타임 테스트**:
```csharp
[Theory]
[InlineData("python", "print('test')", "test")]
[InlineData("javascript", "console.log('test')", "test")]
[InlineData("go", "package main\nfunc main() { println(\"test\") }", "test")]
public async Task Runtime_ExecutesCode_Correctly(
    string language,
    string code,
    string expected)
{
    var runtime = RuntimeRegistry.Get(language);
    var result = await runtime.ExecuteAsync(code, new ExecutionConfig());

    result.ExitCode.Should().Be(0);
    result.Stdout.Should().Contain(expected);
}
```

**체크포인트**: 모든 런타임 테스트 통과

---

### Day 11-14: API 서버

**구현**:
```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IQueue, FileQueue>();
builder.Services.AddSingleton<IStorage, FileStorage>();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapControllers();
app.Run();
```

**엔드포인트**:
- `POST /api/execute` - 동기 실행
- `POST /api/execute/async` - 비동기 실행
- `GET /api/execute/status/{id}` - 상태 조회
- `GET /api/languages` - 지원 언어 목록

**API 테스트**:
```csharp
[Fact]
public async Task Execute_ReturnsCorrectResult()
{
    var client = _factory.CreateClient();

    var response = await client.PostAsJsonAsync("/api/execute", new
    {
        Code = "print('Hello')",
        Language = "python",
        Config = new { Timeout = 10 }
    });

    var result = await response.Content
        .ReadFromJsonAsync<ExecutionResult>();

    result.Stdout.Should().Contain("Hello");
}
```

**체크포인트**: API 통합 테스트 통과, Swagger UI 확인

---

## Week 3: Worker & 최적화 (Worker & Optimization)

### Day 15-17: Worker 서비스

**구현**:
```csharp
// CodeBeaker.Worker/WorkerService.cs
public class CodeExecutionWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var workers = Enumerable
            .Range(0, _concurrency)
            .Select(i => ProcessTasksAsync(i, ct));

        await Task.WhenAll(workers);
    }

    private async Task ProcessTasksAsync(int workerId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var task = await _queue.GetTaskAsync(TimeSpan.FromSeconds(1), ct);
            if (task == null) continue;

            // 실행 & 결과 저장
        }
    }
}
```

**Worker 테스트**:
- 다중 워커 동시 실행
- 작업 중복 처리 방지
- 정상 종료 (Graceful Shutdown)

**체크포인트**: Worker 부하 테스트 (100 req/s)

---

### Day 18-21: 성능 최적화

**벤치마크 작성**:
```csharp
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
public class ExecutionBenchmarks
{
    [Benchmark]
    public async Task PythonExecution()
    {
        var runtime = RuntimeRegistry.Get("python");
        await runtime.ExecuteAsync("print('test')", new ExecutionConfig());
    }

    [Benchmark]
    public async Task FileQueueSubmit()
    {
        await _queue.SubmitTaskAsync("print('test')", "python", new ExecutionConfig());
    }
}
```

**최적화 항목**:
1. 파일 I/O 병렬화
2. Docker 컨테이너 재사용 (풀링)
3. JSON 직렬화 최적화 (Source Generator)
4. 메모리 할당 최소화 (Span<T>)

**성능 목표**:
- API 응답 시간: < 5ms
- 워커 처리량: > 200 req/s
- 메모리 사용: < 100MB

**체크포인트**: 벤치마크 결과 문서화

---

## Week 4: 통합 & 배포 (Integration & Deployment)

### Day 22-24: 통합 테스트

**End-to-End 테스트**:
```csharp
[Fact]
public async Task FullWorkflow_WorksCorrectly()
{
    // 1. API로 작업 제출
    var response = await _client.PostAsJsonAsync("/api/execute/async", request);
    var submitResult = await response.Content.ReadFromJsonAsync<SubmitResult>();

    // 2. 워커가 처리할 때까지 대기
    await Task.Delay(2000);

    // 3. 상태 조회
    var statusResponse = await _client.GetAsync($"/api/execute/status/{submitResult.ExecutionId}");
    var result = await statusResponse.Content.ReadFromJsonAsync<ExecutionResult>();

    // 4. 검증
    result.Status.Should().Be("completed");
    result.Stdout.Should().Contain(expectedOutput);
}
```

**파일시스템 통합 테스트**:
- Python 기존 코드 (FileQueue, FileStorage) 호환성 확인
- 데이터 마이그레이션 불필요 (파일 형식 동일)

**체크포인트**: 통합 테스트 100% 통과

---

### Day 25-26: Docker 이미지

**Dockerfile (API)**:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["CodeBeaker.API/", "CodeBeaker.API/"]
COPY ["CodeBeaker.Core/", "CodeBeaker.Core/"]
COPY ["CodeBeaker.Runtimes/", "CodeBeaker.Runtimes/"]
RUN dotnet publish "CodeBeaker.API/CodeBeaker.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CodeBeaker.API.dll"]
```

**docker-compose.yml**:
```yaml
version: '3.8'
services:
  api:
    build: .
    ports:
      - "5000:8080"
    volumes:
      - ./data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock
    environment:
      - ASPNETCORE_ENVIRONMENT=Production

  worker:
    build:
      context: .
      dockerfile: Dockerfile.worker
    volumes:
      - ./data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock
    deploy:
      replicas: 3
```

**체크포인트**: Docker Compose로 전체 시스템 실행

---

### Day 27-28: 문서 & 정리

**문서 작성**:
- README.md - 프로젝트 소개, 빠른 시작
- CSHARP_ARCHITECTURE.md - 아키텍처 설계 (완료)
- API_REFERENCE.md - API 문서 (Swagger 기반)
- DEPLOYMENT.md - 배포 가이드

**Python 코드 아카이브**:
```bash
mkdir -p archive/python-legacy
mv src/ archive/python-legacy/
mv tests/ archive/python-legacy/
```

**최종 검증**:
- [ ] 모든 테스트 통과
- [ ] 벤치마크 성능 달성
- [ ] Docker 이미지 빌드 성공
- [ ] API 문서 완성
- [ ] README 업데이트

---

## 롤백 계획

**Python 코드 복구**:
```bash
# 아카이브에서 복구
cp -r archive/python-legacy/src .
cp -r archive/python-legacy/tests .

# 의존성 재설치
pip install -r requirements.txt

# 테스트 실행
pytest tests/ -v
```

**언제 롤백?**:
- C# 성능이 Python보다 느린 경우
- 3주 차까지 핵심 기능 미완성
- 치명적 버그 발견 & 해결 불가

---

## 성공 지표

| 항목 | Python (현재) | C# (목표) | 개선율 |
|------|---------------|-----------|--------|
| API 응답 (p99) | 10ms | < 5ms | 2배 |
| 처리량 | 50 req/s | 200+ req/s | 4배 |
| 메모리 (API) | 200MB | < 100MB | 2배 |
| 워커 동시성 | 10개 | 50개 | 5배 |
| 테스트 커버리지 | 84% | 80%+ | 유지 |

---

## 마일스톤

**Week 1 완료**: Core 라이브러리 + Docker 실행
- Core 테스트 100% 통과
- Docker 격리 실행 확인

**Week 2 완료**: 런타임 + API
- 모든 언어 런타임 작동
- API 통합 테스트 통과

**Week 3 완료**: Worker + 최적화
- Worker 부하 테스트 통과
- 벤치마크 목표 달성

**Week 4 완료**: 배포 준비
- Docker 이미지 빌드
- 문서 완성
- Python 코드 아카이브

---

## 위험 요소 & 대응

| 위험 | 영향 | 확률 | 대응 |
|------|------|------|------|
| Docker SDK 호환성 | 높음 | 낮음 | 사전 PoC 테스트 |
| 성능 목표 미달 | 높음 | 중간 | 벤치마크 우선 실행 |
| 일정 지연 | 중간 | 중간 | 스코프 축소 (C# 우선) |
| 테스트 누락 | 중간 | 낮음 | TDD 방식 적용 |

---

## 다음 단계

1. **즉시**: Python 벤치마크 작성 (baseline)
2. **Day 1**: .NET 솔루션 생성
3. **Day 2**: Core Models 구현
4. **Daily**: 진행 상황 트래킹 & 체크포인트 검증
