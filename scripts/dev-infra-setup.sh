#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════
# Dev-/CI-Infrastruktur für die Test-Ebenen in einer Umgebung OHNE
# Docker-Hub-Zugang und OHNE vorinstalliertes .NET-SDK.
#
# Baut auf:
#   - Ubuntu 24.04 (noble), apt verfügbar
#   - Ausgehendes HTTPS über den Agent-Proxy; packages.microsoft.com,
#     apt.releases.hashicorp.com und api.nuget.org sind erreichbar,
#     der Docker-Hub-Blob-CDN NICHT.
#
# Warum nativ statt Docker: der Docker-Hub-Blob-CDN
# (production.cloudfront.docker.com) ist per Egress-Policy geblockt
# (403), Image-Pulls schlagen mittendrin fehl. Postgres/Redis liegen
# in Ubuntu-apt, Consul in der HashiCorp-apt-Quelle — alle nativ auf
# localhost, exakt die Ports/Creds aus docker-compose.infrastructure.yml.
#
# Warum .NET 10 statt 9: .NET 9 ist (Stand 2026-08) EOL und aus allen
# apt-Feeds + dem CDN raus. Das 10er-SDK baut net9.0 (Targeting-Packs
# von nuget.org) und läuft die net9.0-Tests per Roll-Forward.
# ═══════════════════════════════════════════════════════════════════
set -euo pipefail

echo "── 1/5  .NET 10 SDK (baut net9.0) ────────────────────────────"
if ! command -v dotnet >/dev/null 2>&1 && [ ! -x /usr/bin/dotnet ]; then
  curl -fsSL https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb \
    -o /tmp/packages-microsoft-prod.deb
  sudo dpkg -i /tmp/packages-microsoft-prod.deb
  # Fremd-PPAs, die der Proxy blockt, aus dem apt-update nehmen (sonst bricht update)
  sudo rm -f /etc/apt/sources.list.d/deadsnakes-ubuntu-ppa-noble.sources \
             /etc/apt/sources.list.d/ondrej-ubuntu-php-noble.sources
  sudo apt-get update
  sudo apt-get install -y dotnet-sdk-10.0 aspnetcore-runtime-10.0
fi
dotnet --version

echo "── 2/5  HashiCorp-apt-Quelle (Consul) ────────────────────────"
if [ ! -f /etc/apt/sources.list.d/hashicorp.list ]; then
  curl -fsSL https://apt.releases.hashicorp.com/gpg \
    | sudo gpg --dearmor -o /usr/share/keyrings/hashicorp-archive-keyring.gpg
  echo "deb [signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] https://apt.releases.hashicorp.com noble main" \
    | sudo tee /etc/apt/sources.list.d/hashicorp.list >/dev/null
  sudo apt-get update
fi

echo "── 3/5  Postgres 16 / Redis 7 / Consul (nativ via apt) ───────"
sudo apt-get install -y postgresql redis-server consul

echo "── 4/5  Postgres starten + konfigurieren (localhost:5432) ────"
sudo pg_ctlcluster 16 main start || true
sleep 3
sudo -u postgres psql -c "ALTER USER postgres PASSWORD 'postgres';"
sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='cqrs_events'" | grep -q 1 \
  || sudo -u postgres createdb cqrs_events
PGPASSWORD=postgres psql -h localhost -p 5432 -U postgres -d cqrs_events -tc "SELECT 'postgres ok';"

echo "── 5/5  Redis + Consul (dev) starten ─────────────────────────"
redis-cli -h 127.0.0.1 ping >/dev/null 2>&1 \
  || redis-server --daemonize yes --bind 127.0.0.1 --port 6379
redis-cli -h 127.0.0.1 ping

if ! curl -sf http://localhost:8500/v1/status/leader >/dev/null 2>&1; then
  nohup consul agent -dev -client=0.0.0.0 > /tmp/consul.log 2>&1 &
  sleep 5
fi
curl -sS http://localhost:8500/v1/status/leader && echo " ← consul leader"

echo ""
echo "✅ Infra bereit. Tests laufen mit:"
echo "   export DOTNET_ROLL_FORWARD=LatestMajor"
echo "   dotnet test Infrastructure.Pruefstand.Tests/...      # Ebene 1 (in-memory)"
echo "   dotnet test Infrastructure.Integration.Tests/...     # Ebene 2 (sequentiell!)"
