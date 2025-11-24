# CodeBeaker API 테스트 예제 스크립트

param(
    [string]$ApiUrl = "http://localhost:5039"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CodeBeaker API 테스트" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# API 서버 확인
Write-Host "1. API 서버 Health Check..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/health" -Method Get
    Write-Host "   ✅ API 서버 정상 동작" -ForegroundColor Green
} catch {
    Write-Host "   ❌ API 서버에 연결할 수 없습니다!" -ForegroundColor Red
    Write-Host "   먼저 API 서버를 시작하세요: .\scripts\start-dev.ps1" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# 지원 언어 조회
Write-Host "2. 지원 언어 조회..." -ForegroundColor Yellow
try {
    $languages = Invoke-RestMethod -Uri "$ApiUrl/api/language" -Method Get
    Write-Host "   ✅ 지원 언어:" -ForegroundColor Green
    foreach ($lang in $languages) {
        Write-Host "      - $($lang.displayName) ($($lang.version))" -ForegroundColor Cyan
    }
} catch {
    Write-Host "   ❌ 언어 조회 실패: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 테스트 예제 함수
function Test-CodeExecution {
    param(
        [string]$Language,
        [string]$Code,
        [string]$Description
    )

    Write-Host "   테스트: $Description" -ForegroundColor Cyan

    # 코드 실행 요청
    $body = @{
        code = $Code
        language = $Language
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$ApiUrl/api/execution" `
            -Method Post `
            -ContentType "application/json" `
            -Body $body

        $executionId = $response.executionId
        Write-Host "      Execution ID: $executionId" -ForegroundColor Gray

        # 결과 대기 (최대 10초)
        $maxWait = 10
        $waited = 0
        $result = $null

        while ($waited -lt $maxWait) {
            Start-Sleep -Seconds 1
            $waited++

            try {
                $result = Invoke-RestMethod -Uri "$ApiUrl/api/execution/$executionId" -Method Get

                if ($result.status -eq "completed") {
                    Write-Host "      ✅ 실행 완료 ($($result.durationMs)ms)" -ForegroundColor Green
                    Write-Host "      📤 출력:" -ForegroundColor Yellow
                    Write-Host "         $($result.stdout)" -ForegroundColor White
                    if ($result.stderr) {
                        Write-Host "      ⚠️ 에러:" -ForegroundColor Yellow
                        Write-Host "         $($result.stderr)" -ForegroundColor Red
                    }
                    return $true
                }
                elseif ($result.status -eq "failed") {
                    Write-Host "      ❌ 실행 실패: $($result.errorType)" -ForegroundColor Red
                    if ($result.stderr) {
                        Write-Host "      에러 메시지: $($result.stderr)" -ForegroundColor Red
                    }
                    return $false
                }
            } catch {
                # 아직 결과가 없을 수 있음
            }
        }

        Write-Host "      ⏱️ 타임아웃 (10초 초과)" -ForegroundColor Yellow
        return $false
    }
    catch {
        Write-Host "      ❌ 실행 요청 실패: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Python 테스트
Write-Host "3. Python 코드 실행 테스트..." -ForegroundColor Yellow
$pythonCode = @"
for i in range(3):
    print(f"Count: {i}")
print("Python test complete!")
"@
Test-CodeExecution -Language "python" -Code $pythonCode -Description "Python 반복문"
Write-Host ""

# JavaScript 테스트
Write-Host "4. JavaScript 코드 실행 테스트..." -ForegroundColor Yellow
$jsCode = @"
const numbers = [1, 2, 3, 4, 5];
const sum = numbers.reduce((a, b) => a + b, 0);
console.log(`Sum: ${sum}`);
console.log('JavaScript test complete!');
"@
Test-CodeExecution -Language "javascript" -Code $jsCode -Description "JavaScript 배열 연산"
Write-Host ""

# Go 테스트
Write-Host "5. Go 코드 실행 테스트..." -ForegroundColor Yellow
$goCode = @"
package main
import "fmt"
func main() {
    message := "Hello from Go!"
    fmt.Println(message)
    fmt.Println("Go test complete!")
}
"@
Test-CodeExecution -Language "go" -Code $goCode -Description "Go Hello World"
Write-Host ""

# C# 테스트
Write-Host "6. C# 코드 실행 테스트..." -ForegroundColor Yellow
$csharpCode = @"
var numbers = new[] { 1, 2, 3, 4, 5 };
var sum = numbers.Sum();
Console.WriteLine($"Sum: {sum}");
Console.WriteLine("C# test complete!");
"@
Test-CodeExecution -Language "csharp" -Code $csharpCode -Description "C# LINQ 연산"
Write-Host ""

# 에러 처리 테스트
Write-Host "7. 에러 처리 테스트..." -ForegroundColor Yellow
$errorCode = @"
# 의도적인 에러
print(undefined_variable)
"@
Test-CodeExecution -Language "python" -Code $errorCode -Description "Python 런타임 에러"
Write-Host ""

# 완료
Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ 모든 테스트 완료!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

Write-Host "💡 추가 테스트:" -ForegroundColor Yellow
Write-Host "   - Swagger UI에서 대화형 테스트: $ApiUrl" -ForegroundColor Cyan
Write-Host "   - curl 명령어 예제는 USAGE.md 참조" -ForegroundColor Cyan
Write-Host ""
