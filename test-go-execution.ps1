# Go 코드 실행 테스트
$apiUrl = "http://localhost:5050"

# API 서버 확인
try {
    $health = Invoke-RestMethod -Uri "$apiUrl/health" -Method Get
    Write-Host "✅ API 서버 응답: $health" -ForegroundColor Green
} catch {
    Write-Host "❌ API 서버에 연결할 수 없습니다" -ForegroundColor Red
    exit 1
}

# Go 코드 제출
$code = @"
package main
import "fmt"

func main() {
    fmt.Println("Hello from Go!")
}
"@

$requestBody = @{
    code = $code
    language = "go"
} | ConvertTo-Json

Write-Host "`n📤 Go 코드 실행 요청 중..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "$apiUrl/api/execution" `
        -Method Post `
        -ContentType "application/json" `
        -Body $requestBody `
        -TimeoutSec 5

    $executionId = $response.executionId
    Write-Host "✅ 실행 ID: $executionId" -ForegroundColor Green

    # 결과 대기
    Write-Host "`n⏳ 결과 대기 중..." -ForegroundColor Yellow
    $maxWait = 30
    $waited = 0

    while ($waited -lt $maxWait) {
        Start-Sleep -Seconds 2
        $waited += 2

        try {
            $result = Invoke-RestMethod -Uri "$apiUrl/api/execution/$executionId" `
                -Method Get `
                -TimeoutSec 2

            Write-Host "  [$waited초] 상태: $($result.status)" -ForegroundColor Gray

            if ($result.status -eq "completed") {
                Write-Host "`n✅ 실행 완료!" -ForegroundColor Green
                Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
                Write-Host "Exit Code: $($result.exitCode)" -ForegroundColor White
                Write-Host "Duration: $($result.durationMs)ms" -ForegroundColor White
                Write-Host "`nStdout:" -ForegroundColor Yellow
                Write-Host $result.stdout
                if ($result.stderr) {
                    Write-Host "`nStderr:" -ForegroundColor Red
                    Write-Host $result.stderr
                }
                Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
                break
            }
            elseif ($result.status -eq "failed") {
                Write-Host "`n❌ 실행 실패" -ForegroundColor Red
                Write-Host "Error Type: $($result.errorType)" -ForegroundColor Red
                if ($result.stderr) {
                    Write-Host "Stderr: $($result.stderr)" -ForegroundColor Red
                }
                break
            }
        } catch {
            # 아직 결과 없음
        }
    }

    if ($waited -ge $maxWait) {
        Write-Host "`n⏱️  타임아웃: $maxWait초 초과" -ForegroundColor Red
        Write-Host "Worker가 작업을 처리하지 못했을 수 있습니다" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ 오류 발생: $($_.Exception.Message)" -ForegroundColor Red
}
