using Abstractions;
using Bildvorschau.Katalog;

namespace Domain.Akteure;

// Die ÜBERSETZUNGEN DER AKTEUR-SEITE (docs/04 §4.7): wie ein Akteur aus dem, was eine Quellart meldet, eine Nachricht macht, die er darf — Feld für Feld,
// ohne Logik, vom Editor geschrieben. Welche Quellarten einen Akteur verwirklichen können, folgt daraus; welche laufen, sagen die Eingänge der Clients.

#pragma warning disable CS9113

/// <summary>Das Kamerasystem gibt jede fertige Datei, die sein Dateiwächter findet, zum Lesen in die Bildaufbereitung.</summary>
public sealed class DateiGefunden_LiesBild_1(DateiGefunden source) : ITranslate<DateiGefunden, LiesBild>
{
    public LiesBild Translate() => new(
        Name: source.Name,
        Pfad: source.Pfad,
        Groesse: source.Groesse,
        ErkanntAm: source.ErkanntAm);
}
