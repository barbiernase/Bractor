# Nachtrag (2026-10-15)

Ungültig wegen der Prüfung, nicht wegen des Systems: „S3 ×8: je Teil ein erfolgreich beendeter Vorgang — soll 2000, ist 1669“ lief direkt
nach dem Abbau der Teile. Eine Nachprüfung im Log wenige Minuten später: alle 4 000 Vorgänge des `TeilFertigProzess` beendet, keiner doppelt,
der Offen-Index leer — das Prozess-Ende kommt unter Last über den Backstop nach (Selbst-Weckung im Zeitlimit, docs/13 PF-9). Der Treiber wartet
seither auf das Prozess-Ende und misst diesen Nachlauf (`prozessende_nachlauf_s`).
