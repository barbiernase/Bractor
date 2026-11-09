using Abstractions;
using Pruefwerk.Katalog.Import;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Uebersetzungen.Posten;

#pragma warning disable CS9113

// ÜBERSETZUNG (docs/04 §4.4): jede gelesene Zeile des Zeilenlaufs (Katalog Pruefwerk) eröffnet einen Posten — die Id der Zeile wird
// seine Id, ihr Wert sein Bestand.

public sealed class Gelesen_EroeffnePosten_1(Gelesen source) : ITranslate<Gelesen, EroeffnePosten>
{
    public EroeffnePosten Translate() => new(AggregateId: new(source.Id), Bestand: source.Wert);
}
