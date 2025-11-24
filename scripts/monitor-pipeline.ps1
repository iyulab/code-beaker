# 파이프라인 실행 모니터링 스크립트
# simulate-pipeline.ps1과 함께 실행하여 실시간 모니터링

param(
    [int]$RefreshInterval = 1,
    [string]$QueuePath = "$env:TEMP\codebeaker-queue-sim",
    [string]$StoragePath = "$env:TEMP\codebeaker-storage-sim"
)

function Get-QueueStatus {
    $pending = (Get-ChildItem "$QueuePath\pending" -File -ErrorAction SilentlyContinue).Count
    $processing = (Get-ChildItem "$QueuePath\processing" -File -ErrorAction SilentlyContinue).Count
    $completed = (Get-ChildItem "$QueuePath\completed" -File -ErrorAction SilentlyContinue).Count

    return @{
        Pending = $pending
        Processing = $processing
        Completed = $completed
        Total = $pending + $processing + $completed
    }
}

function Get-StorageStatus {
    $files = Get-ChildItem $StoragePath -File -ErrorAction SilentlyContinue
    $results = @{
        Total = $files.Count
        Completed = 0
        Running = 0
        Failed = 0
    }

    foreach ($file in $files) {
        try {
            $content = Get-Content $file.FullName -Raw | ConvertFrom-Json
            switch ($content.status) {
                "completed" { $results.Completed++ }
                "running" { $results.Running++ }
                "failed" { $results.Failed++ }
            }
        }
        catch {
            # 읽기 실패 무시
        }
    }

    return $results
}

function Draw-Bar {
    param([int]$Value, [int]$Max, [int]$Width = 20, [string]$Color = "Green")

    if ($Max -eq 0) { $Max = 1 }
    $filled = [math]::Floor(($Value / $Max) * $Width)
    $bar = ("#" * $filled) + ("-" * ($Width - $filled))

    Write-Host "[" -NoNewline
    Write-Host $bar -ForegroundColor $Color -NoNewline
    Write-Host "] $Value/$Max" -NoNewline
}

Clear-Host
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║         CodeBeaker 파이프라인 모니터                      ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Queue Path: $QueuePath" -ForegroundColor Gray
Write-Host "Storage Path: $StoragePath" -ForegroundColor Gray
Write-Host "Press Ctrl+C to exit" -ForegroundColor Yellow
Write-Host ""

$startTime = Get-Date

while ($true) {
    $currentTime = Get-Date
    $elapsed = $currentTime - $startTime

    # 큐 상태
    $queue = Get-QueueStatus

    # 저장소 상태
    $storage = Get-StorageStatus

    # 화면 지우기 (커서 위치 유지)
    $cursorTop = [Console]::CursorTop - 15
    if ($cursorTop -lt 0) { $cursorTop = 0 }
    [Console]::SetCursorPosition(0, $cursorTop)

    # 헤더
    Write-Host ("=" * 60) -ForegroundColor Cyan
    Write-Host " 실행 시간: $($elapsed.ToString('hh\:mm\:ss'))" -ForegroundColor White
    Write-Host ("=" * 60) -ForegroundColor Cyan
    Write-Host ""

    # 큐 상태
    Write-Host "📦 작업 큐" -ForegroundColor Yellow
    Write-Host "  대기 중   : " -NoNewline
    Draw-Bar -Value $queue.Pending -Max $queue.Total -Color Yellow
    Write-Host ""
    Write-Host "  처리 중   : " -NoNewline
    Draw-Bar -Value $queue.Processing -Max $queue.Total -Color Cyan
    Write-Host ""
    Write-Host "  완료      : " -NoNewline
    Draw-Bar -Value $queue.Completed -Max $queue.Total -Color Green
    Write-Host ""
    Write-Host ""

    # 저장소 상태
    Write-Host "💾 실행 결과" -ForegroundColor Yellow
    Write-Host "  완료      : " -NoNewline
    Draw-Bar -Value $storage.Completed -Max $storage.Total -Color Green
    Write-Host ""
    Write-Host "  실행 중   : " -NoNewline
    Draw-Bar -Value $storage.Running -Max $storage.Total -Color Cyan
    Write-Host ""
    Write-Host "  실패      : " -NoNewline
    Draw-Bar -Value $storage.Failed -Max $storage.Total -Color Red
    Write-Host ""
    Write-Host ""

    # 통계
    $successRate = if ($storage.Total -gt 0) {
        [math]::Round(($storage.Completed / $storage.Total) * 100, 1)
    } else { 0 }

    Write-Host "📊 통계" -ForegroundColor Yellow
    Write-Host "  총 작업   : $($storage.Total)" -ForegroundColor White
    Write-Host "  성공률    : $successRate%" -ForegroundColor $(if ($successRate -ge 90) { "Green" } elseif ($successRate -ge 70) { "Yellow" } else { "Red" })
    Write-Host ""

    Start-Sleep -Seconds $RefreshInterval
}
