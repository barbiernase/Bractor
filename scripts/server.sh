#!/usr/bin/env bash
# Steuert den Messserver über SSH. Der Code kommt nur über GitHub dorthin (der Commit muss gepusht sein), Ergebnisse kommen per rsync zurück.
#
#   scripts/server.sh einrichten              Server einrichten (idempotent, scripts/server-einrichten.sh): Checkout, .NET-SDK wie lokal,
#                                             Python der Worker, Infrastruktur-Container
#   scripts/server.sh lauf <befehl …>         HEAD auf dem Server auschecken, <befehl> dort in tmux ausführen (übersteht SSH-Abbrüche), die
#                                             Ausgabe mitlesen, neue Ordner aus vermessung/ergebnisse/ zurückholen; Exit-Code = der des Befehls
#   scripts/server.sh lauf --los <befehl …>   dasselbe, ohne zu warten (später: warte)
#   scripts/server.sh warte                   den laufenden bzw. letzten Lauf mitlesen, Ergebnisse holen
#   scripts/server.sh status                  läuft etwas? letzte Läufe, Last, Container
#   scripts/server.sh log [n]                 die letzten n Zeilen (Standard 50) des laufenden bzw. letzten Laufs
#
# Beispiele:
#   scripts/server.sh lauf dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj
#   scripts/server.sh lauf scripts/vermessung-e2e.sh --anlass server-baseline
#
# Ein Lauf zur Zeit (Messungen nie parallel, docs/12 §12.7). Ziel: CQRS_SERVER (Standard root@136.243.92.101).
# Auf dem Server: Checkout /opt/bractor (detached auf dem Commit), Läufe /opt/bractor-laeufe/<id>.{sh,log,exit}, Ergebnis-Archiv
# /opt/bractor-laeufe/ergebnisse/ (der Checkout wird vor jedem Lauf gesäubert).
set -euo pipefail

ZIEL="${CQRS_SERVER:-root@136.243.92.101}"
REPO_URL="https://github.com/barbiernase/Bractor.git"
CO=/opt/bractor
LAEUFE=/opt/bractor-laeufe
WURZEL="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
SSH_OPT=(-o BatchMode=yes -o ControlMaster=auto -o "ControlPath=$HOME/.ssh/cm-%r@%h:%p" -o ControlPersist=120)

fern() { ssh "${SSH_OPT[@]}" "$ZIEL" "$@"; }
frei_pruefen() {
  if fern "tmux has-session -t bractor 2>/dev/null"; then echo "Auf dem Server läuft schon ein Lauf (scripts/server.sh status)." >&2; exit 1; fi
}

# Der Server holt genau den lokalen HEAD — also muss er auf GitHub liegen.
commit_pruefen() {
  SHA="$(git -C "$WURZEL" rev-parse HEAD)"
  if [ -z "$(git -C "$WURZEL" branch -r --contains "$SHA" 2>/dev/null)" ]; then
    echo "HEAD ${SHA:0:7} liegt nicht auf origin — erst pushen." >&2; exit 1
  fi
  if [ -n "$(git -C "$WURZEL" status --porcelain)" ]; then
    echo "Hinweis: nicht committete Änderungen fehlen auf dem Server (er läuft auf ${SHA:0:7})." >&2
  fi
}

auschecken() {
  fern "set -e
    [ -d $CO/.git ] || git clone -q $REPO_URL $CO
    cd $CO && git fetch -q origin && git checkout -qf --detach $SHA && git clean -fdq"
}

ergebnisse_holen() {
  mkdir -p "$WURZEL/vermessung/ergebnisse"
  local neu
  neu=$(rsync -a --ignore-existing --out-format='%n' -e "ssh ${SSH_OPT[*]}" "$ZIEL:$LAEUFE/ergebnisse/" "$WURZEL/vermessung/ergebnisse/" \
    | grep -v '/$' | cut -d/ -f1 | sort -u || true)
  [ -n "$neu" ] && echo "Ergebnisse geholt: $(echo "$neu" | tr '\n' ' ')"
  return 0
}

warte() {
  local id gelesen=0 rc stueck
  id="$(fern "cat $LAEUFE/letzter 2>/dev/null")" || { echo "Noch kein Lauf." >&2; exit 1; }
  stueck="$(mktemp)"
  echo "== Lauf $id"
  while :; do
    # Ein abgerissenes SSH unterbricht nur das Mitlesen, nicht den Lauf.
    rc="$(fern "cat $LAEUFE/$id.exit 2>/dev/null || echo LAEUFT")" || rc=LAEUFT
    fern "tail -c +$((gelesen + 1)) $LAEUFE/$id.log 2>/dev/null" > "$stueck" || : > "$stueck"
    cat "$stueck"; gelesen=$((gelesen + $(wc -c < "$stueck")))
    [ "$rc" != LAEUFT ] && break
    sleep 10
  done
  rm -f "$stueck"
  ergebnisse_holen
  return "$rc"
}

case "${1:-}" in
  einrichten)
    frei_pruefen; commit_pruefen; auschecken
    fern "bash $CO/scripts/server-einrichten.sh $(dotnet --version)"
    ;;
  lauf)
    shift
    los=0; [ "${1:-}" = --los ] && { los=1; shift; }
    [ $# -gt 0 ] || { echo "Aufruf: scripts/server.sh lauf [--los] <befehl …>" >&2; exit 2; }
    frei_pruefen; commit_pruefen; auschecken
    ID="$(date +%Y%m%d-%H%M%S)-${SHA:0:7}"
    BEFEHL="$(printf '%q ' "$@")"
    # Das Lauf-Skript: Befehl ausführen, neue (nicht committete) Ergebnis-Ordner ins Archiv kopieren (der nächste Lauf säubert den Checkout), Exit-Code ablegen.
    fern "mkdir -p $LAEUFE/ergebnisse && cat > $LAEUFE/$ID.sh && echo $ID > $LAEUFE/letzter" <<EOF
#!/usr/bin/env bash
set +e
cd $CO && . /etc/profile.d/bractor.sh
{
  echo "== \$(date -Is) · Commit ${SHA:0:7} · \$(nproc) Kerne · Last \$(cut -d' ' -f1-3 /proc/loadavg)"
  echo "\$ $BEFEHL"
  $BEFEHL
  rc=\$?
  for d in \$(git ls-files --others --exclude-standard vermessung/ergebnisse | cut -d/ -f3 | sort -u); do cp -r vermessung/ergebnisse/\$d $LAEUFE/ergebnisse/; done
  echo "== \$(date -Is) · Exit \$rc"
} > $LAEUFE/$ID.log 2>&1
echo \$rc > $LAEUFE/$ID.exit
EOF
    fern "tmux new-session -d -s bractor 'bash $LAEUFE/$ID.sh'"
    if [ $los = 1 ]; then echo "Lauf $ID gestartet (scripts/server.sh warte)."; exit 0; fi
    warte
    ;;
  warte) warte ;;
  status)
    fern "if tmux has-session -t bractor 2>/dev/null; then echo \"läuft: \$(cat $LAEUFE/letzter)\"; else echo 'kein Lauf aktiv'; fi
      echo '== letzte Läufe (id → exit)'; for f in \$(ls -t $LAEUFE/*.log 2>/dev/null | head -5); do i=\$(basename \$f .log); echo \"\$i → \$(cat $LAEUFE/\$i.exit 2>/dev/null || echo läuft)\"; done
      echo \"== Last \$(cut -d' ' -f1-3 /proc/loadavg) · \$(free -g | awk '/Mem/{print \$3\" von \"\$2\" GiB belegt\"}')\"
      docker ps --filter name=cqrs- --format '{{.Names}}: {{.Status}}'"
    ;;
  log) fern "tail -n ${2:-50} $LAEUFE/\$(cat $LAEUFE/letzter).log" ;;
  *) sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'; exit 2 ;;
esac
