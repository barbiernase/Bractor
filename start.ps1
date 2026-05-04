# ═══════════════════════════════════════════════════════════════════
# start.ps1 — Startet alles auf dem Windows-PC zum Testen
#
# Voraussetzung: Docker Desktop laeuft (mit WSL2-Backend)
#
# Verwendung:
#   .\start.ps1              Alles starten
#   .\start.ps1 -Stop        Alles stoppen
#   .\start.ps1 -Status      Status anzeigen
# ═══════════════════════════════════════════════════════════════════

param(
    [switch]$Stop,
    [switch]$Status
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

# ─── Konfiguration ───

$WatchPath = "C:\cqrs-data\input"
$GrpcPort  = 5001
$BlazorPort = 5010

# ═══════════════════════════════════════════════════════
# STOP
# ═══════════════════════════════════════════════════════

if ($Stop) {
    Write-Host ""
    Write-Host "Stoppe Prozesse..." -ForegroundColor Yellow

    Get-Process -Name "Host.Grpc" -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host "  Host.Grpc gestoppt" -ForegroundColor Green

    Get-Process -Name "Host.Blazor" -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host "  Host.Blazor gestoppt" -ForegroundColor Green

    Write-Host "Stoppe Docker-Container..." -ForegroundColor Yellow
    docker compose -f docker-compose.infrastructure.yml down 2>$null
    Write-Host "  Docker gestoppt" -ForegroundColor Green
    exit 0
}

# ═══════════════════════════════════════════════════════
# STATUS
# ═══════════════════════════════════════════════════════

if ($Status) {
    Write-Host ""
    Write-Host "=== Prozesse ===" -ForegroundColor Cyan
    $grpc = Get-Process -Name "Host.Grpc" -ErrorAction SilentlyContinue
    $blazor = Get-Process -Name "Host.Blazor" -ErrorAction SilentlyContinue
    if ($grpc)   { Write-Host "  Host.Grpc:   running (PID $($grpc.Id))" -ForegroundColor Green }
    else         { Write-Host "  Host.Grpc:   stopped" -ForegroundColor Red }
    if ($blazor) { Write-Host "  Host.Blazor: running (PID $($blazor.Id))" -ForegroundColor Green }
    else         { Write-Host "  Host.Blazor: stopped" -ForegroundColor Red }
    Write-Host ""
    Write-Host "=== Docker ===" -ForegroundColor Cyan
    docker compose -f docker-compose.infrastructure.yml ps
    exit 0
}

# ═══════════════════════════════════════════════════════
# START
# ═══════════════════════════════════════════════════════

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  CQRS/ES — Windows Test-Deployment"
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

# ─── 0. Voraussetzungen prüfen ───

Write-Host "[0] Pruefe Voraussetzungen..." -ForegroundColor Yellow

$dockerRunning = docker info 2>$null
if (-not $dockerRunning) {
    Write-Host ""
    Write-Host "  Docker Desktop laeuft nicht!" -ForegroundColor Red
    Write-Host "  Bitte Docker Desktop starten und warten bis es bereit ist."
    Write-Host ""
    exit 1
}
Write-Host "  Docker Desktop laeuft" -ForegroundColor Green

# Daten-Verzeichnis erstellen
if (-not (Test-Path $WatchPath)) {
    New-Item -ItemType Directory -Path $WatchPath -Force | Out-Null
    Write-Host "  Erstellt: $WatchPath" -ForegroundColor Green
}

# ─── 1. Infrastruktur starten ───

Write-Host ""
Write-Host "[1] Starte Infrastruktur (PostgreSQL + Redis + Consul)..." -ForegroundColor Yellow
docker compose -f docker-compose.infrastructure.yml up -d

# ─── 2. Warten bis alles bereit ist ───

Write-Host ""
Write-Host "[2] Warte auf Infrastruktur..." -ForegroundColor Yellow

Write-Host -NoNewline "  PostgreSQL..."
for ($i = 0; $i -lt 30; $i++) {
    $ready = docker exec cqrs-postgres pg_isready -U postgres 2>$null
    if ($LASTEXITCODE -eq 0) { break }
    Write-Host -NoNewline "."
    Start-Sleep 1
}
Write-Host " OK" -ForegroundColor Green

Write-Host -NoNewline "  Redis..."
for ($i = 0; $i -lt 30; $i++) {
    $pong = docker exec cqrs-redis redis-cli ping 2>$null
    if ($pong -match "PONG") { break }
    Write-Host -NoNewline "."
    Start-Sleep 1
}
Write-Host " OK" -ForegroundColor Green

Write-Host -NoNewline "  Consul..."
for ($i = 0; $i -lt 30; $i++) {
    $members = docker exec cqrs-consul consul members 2>$null
    if ($LASTEXITCODE -eq 0) { break }
    Write-Host -NoNewline "."
    Start-Sleep 1
}
Write-Host " OK" -ForegroundColor Green

# ─── 3. Host.Grpc starten ───

Write-Host ""
Write-Host "[3] Starte Host.Grpc..." -ForegroundColor Yellow

$env:Pipeline__WatchPath = $WatchPath
$grpcProcess = Start-Process -FilePath ".\grpc\Host.Grpc.exe" `
    -WorkingDirectory ".\grpc" `
    -PassThru `
    -RedirectStandardOutput ".\grpc.log" `
    -RedirectStandardError ".\grpc-error.log" `
    -WindowStyle Hidden

Write-Host "  PID: $($grpcProcess.Id)" -ForegroundColor Green

Write-Host -NoNewline "  Warte auf Port $GrpcPort..."
for ($i = 0; $i -lt 30; $i++) {
    $conn = Test-NetConnection -ComputerName localhost -Port $GrpcPort -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
    if ($conn.TcpTestSucceeded) { break }
    Write-Host -NoNewline "."
    Start-Sleep 1
}
Write-Host " OK" -ForegroundColor Green

# ─── 4. Host.Blazor starten ───

Write-Host ""
Write-Host "[4] Starte Host.Blazor..." -ForegroundColor Yellow

$env:Pipeline__WatchPath = $WatchPath
$env:Blazor__Urls = "http://0.0.0.0:$BlazorPort"
$blazorProcess = Start-Process -FilePath ".\blazor\Host.Blazor.exe" `
    -WorkingDirectory ".\blazor" `
    -PassThru `
    -RedirectStandardOutput ".\blazor.log" `
    -RedirectStandardError ".\blazor-error.log" `
    -WindowStyle Hidden

Write-Host "  PID: $($blazorProcess.Id)" -ForegroundColor Green

Start-Sleep 3

# ─── 5. Fertig! ───

Write-Host ""
Write-Host "======================================================" -ForegroundColor Green
Write-Host "  Alles laeuft!" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Blazor UI:   http://localhost:$BlazorPort"
Write-Host "  gRPC Server: http://localhost:$GrpcPort"
Write-Host "  WatchPath:   $WatchPath"
Write-Host ""
Write-Host "  Logs:"
Write-Host "    Get-Content .\grpc.log -Tail 20 -Wait"
Write-Host "    Get-Content .\blazor.log -Tail 20 -Wait"
Write-Host ""
Write-Host "  Stoppen:"
Write-Host "    .\start.ps1 -Stop"
Write-Host ""

# Browser öffnen
Start-Process "http://localhost:$BlazorPort"
