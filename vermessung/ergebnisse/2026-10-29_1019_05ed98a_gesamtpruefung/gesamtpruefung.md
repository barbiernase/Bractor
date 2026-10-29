# Gesamtprüfung `05ed98a` (2026-10-29 09:19 UTC)

Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Dauer: 51 min  
Vergleich: `4f43172` (2026-10-28_0536_4f43172_server-pubsub-phase6)

| Teil | Ergebnis |
|---|---|
| Korrektheit (volle Stufe) | gelb (wackelt) |
| Messung (Exactly-once unter Last) | grün |
| Störung (harte Abbrüche, Exactly-once, Vollständigkeit) | grün |
| Profil | ja |

## Korrektheit

- Build: grün
- Codegen-Drift: grün
- Infrastructure.Pruefstand.Tests: 558 grün, 0 rot, 1 übersprungen von 559
- Infrastructure.Integration.Tests: 67 grün, 0 rot, 0 übersprungen von 67
- Pruefdomaene.Integration.Tests: 64 grün, 0 rot, 0 übersprungen von 64
- Verhalten.Tests: 23 grün, 1 rot, 0 übersprungen von 24
- Client.Infrastructure.CollectionTests: 30 grün, 0 rot, 0 übersprungen von 30
- Python-SDK (pytest): grün
- Python-Worker (pytest): grün
- GraphExtractor --check: grün
- GraphExtractor --sonde: grün
- GraphExtractor --grammatik: grün
- Gate frischer Checkout: grün

| roter Test | Wiederholungen | Urteil | bekannt |
|---|---|---|---|
| `Verhalten.Tests.NeustartTests.Nach_dem_harten_Neustart_unter_Last_wirkt_jede_Funktion_genau_einmal` | grün, grün | wackelt | G-18 |

## Messung

Fremdlast vorher: Last 12.30 12.28 7.96 2/1098 3981864
 · CPU jetzt: 47 dotnet; 2 dotnet; 1 dockerd; 1 consul; 1 containerd-shim  
Fremdlast nachher: Last 2.93 3.31 5.08 2/1066 3991186
 · CPU jetzt: 2 containerd; 2 consul; 2 redis-server; 1 dockerd; 1 containerd-shim

| Szenario | Kennzahl | jetzt | Vergleich | Urteil |
|---|---|---:|---:|---|
| L1 Leerlauf Beispieldomäne | CPU Host | 1,9 | 2,0 | im Rauschen |
| L1 Leerlauf Beispieldomäne | RSS Host | 1 160 | 1 242 | im Rauschen |
| L1 Leerlauf Beispieldomäne | Nachlauf | – | – | – |
| L1 Leerlauf Beispieldomäne | Queries/s (Leerlauf) | 7,1 | 7,1 | im Rauschen |
| L5 Leerlauf Prüfdomäne | CPU Host | 1,2 | 1,1 | im Rauschen |
| L5 Leerlauf Prüfdomäne | RSS Host | 793 | 797 | im Rauschen |
| L5 Leerlauf Prüfdomäne | Nachlauf | 58,3 | 26,1 | **auffällig (+123%)** |
| L5 Leerlauf Prüfdomäne | Queries/s (Leerlauf) | 6,9 | 5,9 | im Rauschen |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar | 156 | 158 | im Rauschen |
| S1 Bildeingang Host, Stoß | angenommen | 7 141 | 8 192 | im Rauschen |
| S1 Bildeingang Host, Stoß | Latenz p50 | 2 592 | 2 745 | im Rauschen |
| S1 Bildeingang Host, Stoß | Latenz p99 | 3 755 | 3 726 | im Rauschen |
| S1 Bildeingang Host, Stoß | Abbauzeit | 3,8 | 3,7 | im Rauschen |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang | 9,7 | 9,7 | im Rauschen |
| S1 Bildeingang Host, Stoß | Queries je Vorgang | 101 | 90,5 | **auffällig (+12%)** |
| S1 Bildeingang Host, Stoß | CPU Host | 817 | 855 | im Rauschen |
| S1 Bildeingang Host, Stoß | RSS Host | 847 | 1 103 | im Rauschen |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar | 55,1 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen | 7 801 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 | 2 662 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 | 3 511 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit | 3,6 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang | 11,3 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang | 120 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host | 187 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host | 1 012 | – | – |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar | 1 644 | 1 668 | im Rauschen |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen | 5 824 | 4 593 | besser (+27%) |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 | 527 | 513 | im Rauschen |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 | 872 | 752 | im Rauschen |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit | 0,9 | 0,8 | im Rauschen |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang | 0,6 | 0,6 | im Rauschen |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang | 10,9 | 10,8 | im Rauschen |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host | 429 | 246 | **auffällig (+74%)** |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host | 1 192 | 1 242 | im Rauschen |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar | 1 408 | 1 342 | im Rauschen |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen | 1 417 | 1 361 | im Rauschen |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 | 12,5 | 13,1 | im Rauschen |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 | 56,1 | 54,7 | im Rauschen |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit | 0,0 | 0,0 | im Rauschen |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang | 1,5 | 1,5 | im Rauschen |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang | 11,7 | 12,2 | im Rauschen |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host | 303 | 9,9 | **auffällig (30×)** |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host | 1 361 | 1 241 | im Rauschen |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar | 2 600 | – | – |
| S4 Datensatz einfrieren | Transaktionen je Vorgang | 0,0 | – | – |
| S4 Datensatz einfrieren | Queries je Vorgang | 3,1 | – | – |
| S4 Datensatz einfrieren | CPU Host | – | – | – |
| S4 Datensatz einfrieren | RSS Host | – | – | – |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar | 436 | 448 | im Rauschen |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen | 437 | 448 | im Rauschen |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 | 7,1 | 7,2 | im Rauschen |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 | 16,7 | 16,2 | im Rauschen |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit | 0,0 | 0,0 | im Rauschen |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang | 5,4 | 5,4 | im Rauschen |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang | 46,5 | 46,0 | im Rauschen |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host | 353 | 362 | im Rauschen |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host | 842 | 853 | im Rauschen |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar | 269 | 224 | im Rauschen |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen | 8 845 | 8 015 | im Rauschen |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 | 5 737 | 5 614 | im Rauschen |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 | 10 832 | 13 031 | im Rauschen |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit | 10,8 | 13,0 | im Rauschen |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang | 3,8 | 3,8 | im Rauschen |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang | 37,7 | 37,8 | im Rauschen |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host | 306 | 272 | im Rauschen |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host | 963 | 987 | im Rauschen |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar | 325 | 243 | im Rauschen |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen | 2 077 | 2 014 | im Rauschen |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 | 3 902 | 4 066 | im Rauschen |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 | 7 766 | 10 860 | im Rauschen |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit | 7,8 | 10,9 | im Rauschen |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang | 3,8 | 3,9 | im Rauschen |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang | 39,3 | 39,9 | im Rauschen |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host | 298 | 237 | **auffällig (+26%)** |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host | 900 | 903 | im Rauschen |
| S6-64 Clients über gRPC, 64 Sitzungen | Durchsatz bis alles sichtbar | 2 836 | – | – |
| S6-64 Clients über gRPC, 64 Sitzungen | angenommen | 2 836 | – | – |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p50 | 22,5 | – | – |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p99 | 32,4 | – | – |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p50 | 4,7 | – | – |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p99 | 15,6 | – | – |
| S6-64 Clients über gRPC, 64 Sitzungen | Abbauzeit | 0,0 | – | – |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen je Vorgang | 0,9 | – | – |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries je Vorgang | 12,6 | – | – |
| S6-64 Clients über gRPC, 64 Sitzungen | CPU Host | 152 | – | – |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host | 1 209 | – | – |
| S6-8 Clients über gRPC, 8 Sitzungen | Durchsatz bis alles sichtbar | 399 | – | – |
| S6-8 Clients über gRPC, 8 Sitzungen | angenommen | 399 | – | – |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p50 | 19,4 | – | – |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p99 | 28,3 | – | – |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p50 | 1,7 | – | – |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p99 | 7,4 | – | – |
| S6-8 Clients über gRPC, 8 Sitzungen | Abbauzeit | 0,0 | – | – |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen je Vorgang | 2,0 | – | – |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries je Vorgang | 15,9 | – | – |
| S6-8 Clients über gRPC, 8 Sitzungen | CPU Host | 216 | – | – |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host | 1 195 | – | – |

Auffällig (A/B-Nachmessung): L5, S1, S3-64, S3-8, S5-8

## Störung

Gültig: ja  
Exactly-once: grün  

## Profil

### S1-w1


Stichproben 41303 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 19012 ≈ 190.1 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 41272, CPU_TIME 31

Die Anteile beziehen sich auf die arbeitenden Stichproben.

#### Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| Npgsql | 29.9 % |
| OpenCvSharp | 16.9 % |
| Laufzeit allein | 15.7 % |
| Infrastructure | 12.7 % |
| Marten | 7.8 % |
| Grpc.AspNetCore.Server | 3.7 % |
| Polly.Core | 2.4 % |
| Abstractions | 1.8 % |
| Google.Protobuf | 1.6 % |
| Proto.Actor | 1.4 % |
| JasperFx.RuntimeCompiler | 1.3 % |
| Domain.Laufzeit | 0.9 % |
| JasperFx.Events | 0.6 % |
| JasperFx | 0.6 % |
| Proto.Cluster | 0.4 % |
| Cqrs.Leitung | 0.4 % |
| Domain.Infrastructure | 0.3 % |
| Domain | 0.3 % |
| Weasel.Postgresql | 0.2 % |
| Domain.Leitung | 0.2 % |

#### Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `Interop+Sys.Send` | 15.4 % |
| `OpenCvSharp.Internal.NativeMethods.imgcodecs_imwrite` | 6.2 % |
| `OpenCvSharp.Cv2.Resize` | 5.2 % |
| `?` | 4.7 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 3.6 % |

### S5-64-w1


Stichproben 30493 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 14627 ≈ 146.3 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 30471, CPU_TIME 22

Die Anteile beziehen sich auf die arbeitenden Stichproben.

#### Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| Infrastructure | 51.4 % |
| Marten | 25.1 % |
| Npgsql | 15.2 % |
| Laufzeit allein | 4.3 % |
| Proto.Actor | 1.7 % |
| Polly.Core | 0.6 % |
| Weasel.Postgresql | 0.6 % |
| Abstractions | 0.5 % |
| Proto.Cluster | 0.4 % |
| JasperFx.Events | 0.1 % |
| Pruefdomaene.Laufzeit | 0.0 % |
| Consul | 0.0 % |
| Proto.Remote | 0.0 % |
| Google.Protobuf | 0.0 % |

#### Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__21.MoveNext` | 41.2 % |
| `System.Text.Json.ReadStack.Push` | 17.0 % |
| `System.Text.Json.ReadStack.Pop` | 7.0 % |
| `Interop+Sys.Receive` | 4.1 % |
| `Interop+Sys.Send` | 3.6 % |
| `?` | 2.6 % |
| `System.Linq.Enumerable+IteratorSelectIterator`2[System.__Canon,System.__Canon].ToArrayNoPresizing` | 2.3 % |
| `Interop+Kernel32.ReleaseSemaphore` | 1.8 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 1.7 % |
| `Npgsql.Internal.NpgsqlConnector.ResetCancellation` | 1.5 % |
| `System.Linq.Enumerable+ArrayWhereIterator`1[System.__Canon].ToList` | 1.3 % |

Engpass-Sonde je Abschnitt (CPU je Prozess, Postgres-Wartezeiten, Zähler): `vermessung/ergebnisse/2026-10-29_1038_05ed98a_gesamtpruefung-profil/sonde/auswertung.md`


## A/B-Nachmessung

A = `4f43172` (2026-10-29_1046_4f43172_gesamtpruefung-ab-a), B = `05ed98a` (2026-10-29_1053_05ed98a_gesamtpruefung-ab-b)

| Szenario | Kennzahl | A | B | Urteil |
|---|---|---:|---:|---|
| L1 | CPU Host | 2,1 | 2,0 | im Rauschen |
| L1 | RSS Host | 1 035 | 1 210 | im Rauschen |
| L1 | Nachlauf | – | – | – |
| L1 | Queries/s (Leerlauf) | 7,1 | 7,1 | im Rauschen |
| L5 | CPU Host | 1,2 | 1,1 | im Rauschen |
| L5 | RSS Host | 871 | 890 | im Rauschen |
| L5 | Nachlauf | 3,0 | 55,3 | **auffällig (18×)** |
| L5 | Queries/s (Leerlauf) | 5,9 | 6,9 | im Rauschen |
| S1 | Durchsatz bis alles sichtbar | 158 | 150 | im Rauschen |
| S1 | angenommen | 9 514 | 7 212 | im Rauschen |
| S1 | Latenz p50 | 2 637 | 2 896 | im Rauschen |
| S1 | Latenz p99 | 3 725 | 3 930 | im Rauschen |
| S1 | Abbauzeit | 3,7 | 3,9 | im Rauschen |
| S1 | Transaktionen je Vorgang | 9,8 | 9,8 | im Rauschen |
| S1 | Queries je Vorgang | 89,9 | 105 | **auffällig (+17%)** |
| S1 | CPU Host | 793 | 766 | im Rauschen |
| S1 | RSS Host | 979 | 971 | im Rauschen |
| S3-64 | Durchsatz bis alles sichtbar | 1 432 | 1 621 | im Rauschen |
| S3-64 | angenommen | 4 716 | 4 656 | im Rauschen |
| S3-64 | Latenz p50 | 568 | 518 | im Rauschen |
| S3-64 | Latenz p99 | 965 | 793 | im Rauschen |
| S3-64 | Abbauzeit | 1,0 | 0,8 | im Rauschen |
| S3-64 | Transaktionen je Vorgang | 0,6 | 0,6 | im Rauschen |
| S3-64 | Queries je Vorgang | 10,9 | 10,9 | im Rauschen |
| S3-64 | CPU Host | 382 | 331 | im Rauschen |
| S3-64 | RSS Host | 1 149 | 1 210 | im Rauschen |
| S3-8 | Durchsatz bis alles sichtbar | 1 253 | 1 383 | im Rauschen |
| S3-8 | angenommen | 1 259 | 1 393 | im Rauschen |
| S3-8 | Latenz p50 | 14,7 | 11,9 | im Rauschen |
| S3-8 | Latenz p99 | 84,6 | 45,2 | im Rauschen |
| S3-8 | Abbauzeit | 0,0 | 0,0 | im Rauschen |
| S3-8 | Transaktionen je Vorgang | 1,5 | 1,5 | im Rauschen |
| S3-8 | Queries je Vorgang | 11,7 | 11,7 | im Rauschen |
| S3-8 | CPU Host | 408 | 285 | besser (-30%) |
| S3-8 | RSS Host | 1 160 | 1 196 | im Rauschen |
| S5-1 | Durchsatz bis alles sichtbar | 428 | 425 | im Rauschen |
| S5-1 | angenommen | 429 | 426 | im Rauschen |
| S5-1 | Latenz p50 | 7,3 | 7,1 | im Rauschen |
| S5-1 | Latenz p99 | 18,1 | 20,2 | im Rauschen |
| S5-1 | Abbauzeit | 0,0 | 0,0 | im Rauschen |
| S5-1 | Transaktionen je Vorgang | 5,4 | 5,4 | im Rauschen |
| S5-1 | Queries je Vorgang | 46,7 | 46,5 | im Rauschen |
| S5-1 | CPU Host | 357 | 319 | im Rauschen |
| S5-1 | RSS Host | 865 | 872 | im Rauschen |
| S5-64 | Durchsatz bis alles sichtbar | 334 | 247 | im Rauschen |
| S5-64 | angenommen | 7 868 | 8 525 | im Rauschen |
| S5-64 | Latenz p50 | 5 348 | 5 136 | im Rauschen |
| S5-64 | Latenz p99 | 8 439 | 11 808 | im Rauschen |
| S5-64 | Abbauzeit | 8,6 | 11,8 | im Rauschen |
| S5-64 | Transaktionen je Vorgang | 3,8 | 3,9 | im Rauschen |
| S5-64 | Queries je Vorgang | 37,5 | 37,9 | im Rauschen |
| S5-64 | CPU Host | 372 | 267 | besser (-28%) |
| S5-64 | RSS Host | 975 | 992 | im Rauschen |
| S5-8 | Durchsatz bis alles sichtbar | 314 | 262 | im Rauschen |
| S5-8 | angenommen | 1 451 | 2 138 | besser (+47%) |
| S5-8 | Latenz p50 | 4 334 | 3 461 | im Rauschen |
| S5-8 | Latenz p99 | 7 435 | 10 149 | im Rauschen |
| S5-8 | Abbauzeit | 7,5 | 10,0 | im Rauschen |
| S5-8 | Transaktionen je Vorgang | 3,8 | 3,8 | im Rauschen |
| S5-8 | Queries je Vorgang | 39,4 | 39,1 | im Rauschen |
| S5-8 | CPU Host | 346 | 266 | besser (-23%) |
| S5-8 | RSS Host | 897 | 925 | im Rauschen |
