using Abstractions;
using Pruefwerk.Katalog.Import;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Uebersetzungen.Posten;

// ÜBERSETZUNG (docs/04 §4.4): jede gelesene Zeile des Zeilenlaufs (Katalog Pruefwerk) eröffnet einen Posten — die Id der Zeile wird
// seine Id, ihr Wert sein Bestand.

[Feld(nameof(EroeffnePosten.AggregateId), nameof(Gelesen.Id)), Feld(nameof(EroeffnePosten.Bestand), nameof(Gelesen.Wert))]
public sealed record PostenEroeffnen : IUebersetzung<Gelesen, EroeffnePosten>;
