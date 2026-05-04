#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════
# setup-server.sh — Einmalige Server-Einrichtung
#
# Installiert:
#   1. .NET 9 SDK
#   2. Native Dependencies (OpenCvSharp, etc.)
#   3. Deployment-Verzeichnisse
#   4. Docker Infrastruktur (PostgreSQL, Redis, Consul)
#   5. systemd Services
#   6. GitHub Actions Self-Hosted Runner
#
# Verwendung:
#   ssh wirksam@172.16.5.7
#   git clone https://github.com/<user>/<repo>.git
#   cd <repo>
#   bash deploy/setup-server.sh
# ═══════════════════════════════════════════════════════════════════

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

echo ""
echo -e "${CYAN}╔═══════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║  CQRS Server Setup                                   ║${NC}"
echo -e "${CYAN}╚═══════════════════════════════════════════════════════╝${NC}"
echo ""

# ═══════════════════════════════════════════════════════
# 1. .NET 9 SDK
# ═══════════════════════════════════════════════════════

echo -e "${YELLOW}[1/6] .NET 9 SDK${NC}"

if command -v dotnet &>/dev/null && dotnet --list-sdks | grep -q "^9\."; then
    echo -e "  ${GREEN}✔ Bereits installiert: $(dotnet --version)${NC}"
else
    echo "  Installiere .NET 9 SDK..."
    
    # Microsoft Package Repository
    wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
    sudo dpkg -i /tmp/packages-microsoft-prod.deb
    rm /tmp/packages-microsoft-prod.deb
    
    sudo apt-get update -qq
    sudo apt-get install -y -qq dotnet-sdk-9.0
    
    echo -e "  ${GREEN}✔ .NET $(dotnet --version) installiert${NC}"
fi

echo ""

# ═══════════════════════════════════════════════════════
# 2. Native Dependencies
# ═══════════════════════════════════════════════════════

echo -e "${YELLOW}[2/6] Native Dependencies${NC}"

PACKAGES="libgdiplus libc6-dev libgtk2.0-0"
MISSING=""

for pkg in $PACKAGES; do
    if ! dpkg -s "$pkg" &>/dev/null; then
        MISSING="$MISSING $pkg"
    fi
done

if [ -z "$MISSING" ]; then
    echo -e "  ${GREEN}✔ Alle vorhanden${NC}"
else
    echo "  Installiere:$MISSING"
    sudo apt-get install -y -qq $MISSING
    echo -e "  ${GREEN}✔ Installiert${NC}"
fi

echo ""

# ═══════════════════════════════════════════════════════
# 3. Verzeichnisse
# ═══════════════════════════════════════════════════════

echo -e "${YELLOW}[3/6] Verzeichnisse${NC}"

for DIR in /opt/cqrs/grpc /opt/cqrs/blazor /data/input /data/preprocessed; do
    if [ ! -d "$DIR" ]; then
        sudo mkdir -p "$DIR"
        sudo chown wirksam:wirksam "$DIR"
        echo -e "  ${GREEN}✔ $DIR erstellt${NC}"
    else
        echo -e "  ${GREEN}✔ $DIR existiert${NC}"
    fi
done

echo ""

# ═══════════════════════════════════════════════════════
# 4. Docker Infrastruktur
# ═══════════════════════════════════════════════════════

echo -e "${YELLOW}[4/6] Docker Infrastruktur${NC}"

# Finde docker-compose.infrastructure.yml
COMPOSE_FILE=""
for F in ./docker-compose.infrastructure.yml ../docker-compose.infrastructure.yml; do
    if [ -f "$F" ]; then
        COMPOSE_FILE="$F"
        break
    fi
done

if [ -z "$COMPOSE_FILE" ]; then
    echo -e "  ${RED}docker-compose.infrastructure.yml nicht gefunden!${NC}"
    echo "  Bitte aus dem Repo-Root ausführen."
else
    docker compose -f "$COMPOSE_FILE" up -d
    
    echo -n "  PostgreSQL..."
    for i in $(seq 1 30); do
        if docker exec cqrs-postgres pg_isready -U postgres -q 2>/dev/null; then break; fi
        sleep 1; echo -n "."
    done
    echo -e " ${GREEN}✔${NC}"
    
    echo -n "  Redis..."
    for i in $(seq 1 30); do
        if docker exec cqrs-redis redis-cli ping 2>/dev/null | grep -q PONG; then break; fi
        sleep 1; echo -n "."
    done
    echo -e " ${GREEN}✔${NC}"
    
    echo -n "  Consul..."
    for i in $(seq 1 30); do
        if docker exec cqrs-consul consul members 2>/dev/null | grep -q alive; then break; fi
        sleep 1; echo -n "."
    done
    echo -e " ${GREEN}✔${NC}"
fi

echo ""

# ═══════════════════════════════════════════════════════
# 5. systemd Services
# ═══════════════════════════════════════════════════════

echo -e "${YELLOW}[5/6] systemd Services${NC}"

# Service-Dateien kopieren
for SVC in cqrs-grpc cqrs-blazor; do
    SVC_FILE="deploy/${SVC}.service"
    ENV_FILE="deploy/${SVC}.env"
    
    if [ -f "$SVC_FILE" ]; then
        sudo cp "$SVC_FILE" /etc/systemd/system/
        echo -e "  ${GREEN}✔ ${SVC}.service installiert${NC}"
    fi
    
    if [ -f "$ENV_FILE" ]; then
        sudo cp "$ENV_FILE" /opt/cqrs/
        echo -e "  ${GREEN}✔ ${SVC}.env kopiert${NC}"
    fi
done

sudo systemctl daemon-reload
sudo systemctl enable cqrs-grpc cqrs-blazor 2>/dev/null || true
echo -e "  ${GREEN}✔ Services aktiviert (starten beim nächsten Deploy)${NC}"

# sudoers: wirksam darf die Services ohne Passwort steuern
SUDOERS_LINE="wirksam ALL=(ALL) NOPASSWD: /bin/systemctl start cqrs-grpc, /bin/systemctl stop cqrs-grpc, /bin/systemctl restart cqrs-grpc, /bin/systemctl start cqrs-blazor, /bin/systemctl stop cqrs-blazor, /bin/systemctl restart cqrs-blazor, /bin/systemctl daemon-reload, /bin/systemctl enable cqrs-grpc, /bin/systemctl enable cqrs-blazor, /bin/systemctl is-active cqrs-grpc, /bin/systemctl is-active cqrs-blazor, /bin/cp deploy/cqrs-grpc.service /etc/systemd/system/, /bin/cp deploy/cqrs-blazor.service /etc/systemd/system/, /bin/cp deploy/cqrs-grpc.env /opt/cqrs/, /bin/cp deploy/cqrs-blazor.env /opt/cqrs/"

if ! sudo grep -q "cqrs-grpc" /etc/sudoers.d/cqrs 2>/dev/null; then
    echo "$SUDOERS_LINE" | sudo tee /etc/sudoers.d/cqrs > /dev/null
    sudo chmod 440 /etc/sudoers.d/cqrs
    echo -e "  ${GREEN}✔ sudoers konfiguriert${NC}"
else
    echo -e "  ${GREEN}✔ sudoers bereits konfiguriert${NC}"
fi

echo ""

# ═══════════════════════════════════════════════════════
# 6. GitHub Actions Self-Hosted Runner
# ═══════════════════════════════════════════════════════

echo -e "${YELLOW}[6/6] GitHub Actions Runner${NC}"

RUNNER_DIR="$HOME/actions-runner"

if [ -f "$RUNNER_DIR/.runner" ]; then
    echo -e "  ${GREEN}✔ Runner bereits installiert${NC}"
    echo ""
else
    echo ""
    echo -e "  ${CYAN}Der Runner muss manuell konfiguriert werden:${NC}"
    echo ""
    echo "  1. Gehe zu: GitHub Repo → Settings → Actions → Runners"
    echo "  2. Klicke 'New self-hosted runner' → Linux x64"
    echo "  3. Führe die angezeigten Befehle hier aus (Download + Configure)"
    echo ""
    echo "  Kurzform (Token von GitHub ersetzen!):"
    echo ""
    echo -e "  ${YELLOW}mkdir -p ~/actions-runner && cd ~/actions-runner${NC}"
    echo -e "  ${YELLOW}curl -o actions-runner-linux-x64-2.322.0.tar.gz -L https://github.com/actions/runner/releases/download/v2.322.0/actions-runner-linux-x64-2.322.0.tar.gz${NC}"
    echo -e "  ${YELLOW}tar xzf ./actions-runner-linux-x64-2.322.0.tar.gz${NC}"
    echo -e "  ${YELLOW}./config.sh --url https://github.com/<USER>/<REPO> --token <TOKEN>${NC}"
    echo ""
    echo "  4. Danach als Service installieren:"
    echo ""
    echo -e "  ${YELLOW}sudo ./svc.sh install${NC}"
    echo -e "  ${YELLOW}sudo ./svc.sh start${NC}"
    echo ""
    echo -e "  ${CYAN}Hinweis: Die genaue Runner-Version und den Token${NC}"
    echo -e "  ${CYAN}findest du auf der GitHub-Seite.${NC}"
fi

# ═══════════════════════════════════════════════════════
# Zusammenfassung
# ═══════════════════════════════════════════════════════

echo ""
echo -e "${CYAN}═══════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}  Setup abgeschlossen!${NC}"
echo -e "${CYAN}═══════════════════════════════════════════════════════${NC}"
echo ""
echo "  .NET SDK:        $(dotnet --version 2>/dev/null || echo 'nicht installiert')"
echo "  Docker:          $(docker --version 2>/dev/null | cut -d' ' -f3 | tr -d ',')"
echo "  PostgreSQL:      $(docker exec cqrs-postgres psql -V 2>/dev/null | cut -d' ' -f3 || echo 'nicht bereit')"
echo "  Redis:           $(docker exec cqrs-redis redis-server --version 2>/dev/null | cut -d' ' -f3 | tr -d 'v=' || echo 'nicht bereit')"
echo "  Consul:          $(docker exec cqrs-consul consul version 2>/dev/null | head -1 | cut -d' ' -f2 || echo 'nicht bereit')"
echo ""
echo "  Nächster Schritt:"
echo "    → GitHub Runner einrichten (falls noch nicht geschehen)"
echo "    → Dann einfach auf main pushen — Deploy läuft automatisch"
echo ""
