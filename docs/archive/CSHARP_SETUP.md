# C# 프로젝트 초기 설정 가이드

**Day 1-2 실행 계획**

---

## 1. .NET 솔루션 생성

### 1.1 기본 솔루션 구조

```bash
# 솔루션 생성
dotnet new sln -n CodeBeaker

# 프로젝트 생성
dotnet new classlib -n CodeBeaker.Core -f net8.0
dotnet new classlib -n CodeBeaker.Runtimes -f net8.0
dotnet new webapi -n CodeBeaker.API -f net8.0
dotnet new worker -n CodeBeaker.Worker -f net8.0

# 테스트 프로젝트
dotnet new xunit -n CodeBeaker.Core.Tests -f net8.0
dotnet new xunit -n CodeBeaker.Runtimes.Tests -f net8.0
dotnet new xunit -n CodeBeaker.Integration.Tests -f net8.0

# 벤치마크 프로젝트
dotnet new console -n CodeBeaker.Benchmarks -f net8.0

# src/ 디렉토리로 이동
mkdir src
mv CodeBeaker.* src/

# tests/ 디렉토리로 이동
mkdir tests
mv src/CodeBeaker.*.Tests tests/

# benchmarks/ 디렉토리로 이동
mkdir benchmarks
mv src/CodeBeaker.Benchmarks benchmarks/

# 솔루션에 프로젝트 추가
dotnet sln add src/CodeBeaker.Core/CodeBeaker.Core.csproj
dotnet sln add src/CodeBeaker.Runtimes/CodeBeaker.Runtimes.csproj
dotnet sln add src/CodeBeaker.API/CodeBeaker.API.csproj
dotnet sln add src/CodeBeaker.Worker/CodeBeaker.Worker.csproj
dotnet sln add tests/CodeBeaker.Core.Tests/CodeBeaker.Core.Tests.csproj
dotnet sln add tests/CodeBeaker.Runtimes.Tests/CodeBeaker.Runtimes.Tests.csproj
dotnet sln add tests/CodeBeaker.Integration.Tests/CodeBeaker.Integration.Tests.csproj
dotnet sln add benchmarks/CodeBeaker.Benchmarks/CodeBeaker.Benchmarks.csproj
```

---

## 2. 프로젝트 의존성 설정

### 2.1 CodeBeaker.Core

```bash
cd src/CodeBeaker.Core

# Docker SDK
dotnet add package Docker.DotNet --version 3.125.15

# JSON 직렬화
# (System.Text.Json은 .NET 8에 기본 포함)

# 로깅
dotnet add package Serilog --version 3.1.1
dotnet add package Serilog.Sinks.Console --version 5.0.1

cd ../..
```

**CodeBeaker.Core.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Docker.DotNet" Version="3.125.15" />
    <PackageReference Include="Serilog" Version="3.1.1" />
    <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
  </ItemGroup>
</Project>
```

---

### 2.2 CodeBeaker.Runtimes

```bash
cd src/CodeBeaker.Runtimes

# Core 프로젝트 참조
dotnet add reference ../CodeBeaker.Core/CodeBeaker.Core.csproj

cd ../..
```

---

### 2.3 CodeBeaker.API

```bash
cd src/CodeBeaker.API

# Core & Runtimes 참조
dotnet add reference ../CodeBeaker.Core/CodeBeaker.Core.csproj
dotnet add reference ../CodeBeaker.Runtimes/CodeBeaker.Runtimes.csproj

# API 패키지
dotnet add package Swashbuckle.AspNetCore --version 6.5.0
dotnet add package Serilog.AspNetCore --version 8.0.0

cd ../..
```

**CodeBeaker.API.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\CodeBeaker.Core\CodeBeaker.Core.csproj" />
    <ProjectReference Include="..\CodeBeaker.Runtimes\CodeBeaker.Runtimes.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
  </ItemGroup>
</Project>
```

---

### 2.4 CodeBeaker.Worker

```bash
cd src/CodeBeaker.Worker

# Core & Runtimes 참조
dotnet add reference ../CodeBeaker.Core/CodeBeaker.Core.csproj
dotnet add reference ../CodeBeaker.Runtimes/CodeBeaker.Runtimes.csproj

# Worker 패키지
dotnet add package Microsoft.Extensions.Hosting --version 8.0.0
dotnet add package Serilog.Extensions.Hosting --version 8.0.0

cd ../..
```

---

### 2.5 테스트 프로젝트

```bash
# CodeBeaker.Core.Tests
cd tests/CodeBeaker.Core.Tests
dotnet add reference ../../src/CodeBeaker.Core/CodeBeaker.Core.csproj
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Moq --version 4.20.70
cd ../..

# CodeBeaker.Runtimes.Tests
cd tests/CodeBeaker.Runtimes.Tests
dotnet add reference ../../src/CodeBeaker.Runtimes/CodeBeaker.Runtimes.csproj
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Moq --version 4.20.70
cd ../..

# CodeBeaker.Integration.Tests
cd tests/CodeBeaker.Integration.Tests
dotnet add reference ../../src/CodeBeaker.API/CodeBeaker.API.csproj
dotnet add reference ../../src/CodeBeaker.Worker/CodeBeaker.Worker.csproj
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Microsoft.AspNetCore.Mvc.Testing --version 8.0.0
cd ../..
```

---

### 2.6 벤치마크 프로젝트

```bash
cd benchmarks/CodeBeaker.Benchmarks

dotnet add reference ../../src/CodeBeaker.Core/CodeBeaker.Core.csproj
dotnet add reference ../../src/CodeBeaker.Runtimes/CodeBeaker.Runtimes.csproj
dotnet add package BenchmarkDotNet --version 0.13.12

cd ../..
```

---

## 3. 프로젝트 구조 생성

### 3.1 CodeBeaker.Core 구조

```bash
cd src/CodeBeaker.Core

mkdir Models
mkdir Interfaces
mkdir Queue
mkdir Storage
mkdir Docker

# 기본 파일 생성
touch Models/ExecutionConfig.cs
touch Models/ExecutionResult.cs
touch Models/TaskItem.cs
touch Interfaces/IQueue.cs
touch Interfaces/IStorage.cs
touch Interfaces/IRuntime.cs
touch Queue/FileQueue.cs
touch Storage/FileStorage.cs
touch Docker/DockerExecutor.cs

cd ../..
```

### 3.2 CodeBeaker.Runtimes 구조

```bash
cd src/CodeBeaker.Runtimes

mkdir Runtimes

touch Interfaces/IRuntime.cs
touch Runtimes/BaseRuntime.cs
touch Runtimes/PythonRuntime.cs
touch Runtimes/JavaScriptRuntime.cs
touch Runtimes/GoRuntime.cs
touch Runtimes/CSharpRuntime.cs
touch RuntimeRegistry.cs

cd ../..
```

### 3.3 CodeBeaker.API 구조

```bash
cd src/CodeBeaker.API

mkdir Controllers
mkdir Middleware
mkdir Models

touch Controllers/ExecuteController.cs
touch Middleware/ErrorHandlingMiddleware.cs
touch Middleware/RequestLoggingMiddleware.cs
touch Models/ExecuteRequest.cs

cd ../..
```

---

## 4. 빌드 및 검증

```bash
# 전체 솔루션 빌드
dotnet build

# 모든 테스트 실행
dotnet test

# API 실행
cd src/CodeBeaker.API
dotnet run

# Worker 실행 (별도 터미널)
cd src/CodeBeaker.Worker
dotnet run
```

---

## 5. 디렉토리 구조 최종 확인

```
CodeBeaker/
├── CodeBeaker.sln
├── src/
│   ├── CodeBeaker.Core/
│   │   ├── Models/
│   │   ├── Interfaces/
│   │   ├── Queue/
│   │   ├── Storage/
│   │   └── Docker/
│   ├── CodeBeaker.Runtimes/
│   │   ├── Interfaces/
│   │   ├── Runtimes/
│   │   └── RuntimeRegistry.cs
│   ├── CodeBeaker.API/
│   │   ├── Controllers/
│   │   ├── Middleware/
│   │   └── Models/
│   └── CodeBeaker.Worker/
│       └── WorkerService.cs
├── tests/
│   ├── CodeBeaker.Core.Tests/
│   ├── CodeBeaker.Runtimes.Tests/
│   └── CodeBeaker.Integration.Tests/
├── benchmarks/
│   └── CodeBeaker.Benchmarks/
├── docker/
│   └── runtimes/           # 기존 Python 런타임 유지
├── docs/
└── README.md
```

---

## 6. Git 설정

**.gitignore 업데이트**:
```gitignore
# .NET
bin/
obj/
*.user
*.suo
*.cache
.vs/

# Python (legacy)
__pycache__/
*.py[cod]
*$py.class
.Python
venv/
.pytest_cache/

# Data
data/

# IDE
.idea/
.vscode/

# OS
.DS_Store
Thumbs.db
```

---

## 7. EditorConfig (.editorconfig)

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true

[*.cs]
indent_style = space
indent_size = 4

# C# 코드 스타일
csharp_new_line_before_open_brace = all
csharp_prefer_braces = true
dotnet_sort_system_directives_first = true
```

---

## 8. 다음 단계

1. ✅ 솔루션 생성 완료
2. ✅ 프로젝트 의존성 설정
3. ✅ 디렉토리 구조 생성
4. ⬜ Models 구현 (ExecutionConfig, ExecutionResult)
5. ⬜ FileQueue 구현
6. ⬜ FileStorage 구현
7. ⬜ DockerExecutor 구현
8. ⬜ 단위 테스트 작성

---

## 체크리스트

- [ ] .NET 8 SDK 설치 확인
- [ ] Docker Desktop 실행 확인
- [ ] 솔루션 생성
- [ ] 모든 프로젝트 생성
- [ ] 패키지 참조 추가
- [ ] 프로젝트 참조 설정
- [ ] 디렉토리 구조 생성
- [ ] `dotnet build` 성공
- [ ] `dotnet test` 성공
- [ ] Git 커밋: "chore: C# project structure setup"
