# CodeBeaker Runtime Images Build Script for Windows
# Builds all 4 runtime Docker images: Python, Node.js, Go, C#

param(
    [switch]$NoPull,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

Write-Host "🐳 CodeBeaker Runtime Images Builder" -ForegroundColor Cyan
Write-Host ""

$images = @(
    @{Name="Python 3.12"; Tag="codebeaker-python:latest"; Path="docker/runtimes/python"},
    @{Name="Node.js 20"; Tag="codebeaker-nodejs:latest"; Path="docker/runtimes/nodejs"},
    @{Name="Go 1.21"; Tag="codebeaker-golang:latest"; Path="docker/runtimes/golang"},
    @{Name=".NET 8"; Tag="codebeaker-dotnet:latest"; Path="docker/runtimes/csharp"}
)

$rootDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Push-Location $rootDir

try {
    foreach ($img in $images) {
        Write-Host "📦 Building $($img.Name)..." -ForegroundColor Yellow

        $buildArgs = @("build", "-t", $img.Tag)
        if (-not $NoPull) {
            $buildArgs += "--pull"
        }
        if ($Quiet) {
            $buildArgs += "-q"
        }
        $buildArgs += $img.Path

        & docker @buildArgs

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build $($img.Name)"
        }

        Write-Host "✅ $($img.Name) built successfully" -ForegroundColor Green
        Write-Host ""
    }

    Write-Host "🎉 All runtime images built successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📋 Built images:" -ForegroundColor Cyan
    foreach ($img in $images) {
        Write-Host "   - $($img.Tag)" -ForegroundColor Gray
    }
}
finally {
    Pop-Location
}
