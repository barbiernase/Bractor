using Abstractions;
using Domain.Lager;
using Domain.Zahlung;
using Domain.Versand;

namespace Domain.Bestellung;

/// <summary>
/// Die Bestell-Saga als typisierte Event→Command-Regeln (Spec §3). Ein echter DIAMANT: aus EINEM Auslöser
/// laufen ZWEI unabhängige Zweige (Bestand reservieren im Lager, Konto belasten in der Zahlung), die sich am
/// Versand wieder VEREINEN (Join: versenden erst, wenn BEIDE fertig sind).
///
/// Das ist ALLES, was der Entwickler für die Orchestrierung schreibt — kein Manager, kein Treiber, keine
/// Zustandsmaschine, kein Wecken, keine Korrelation von Hand. Scheitert ein Zweig (z.B. Konto ungedeckt),
/// gleicht das Framework die bereits erfolgreichen Zweige über ihr <c>RückgängigDurch</c> aus (reverse
/// Kausalität) und beendet den Prozess als Fehlgeschlagen — der Versand läuft nie.
/// </summary>
public sealed class BestellProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<BestellungAufgegeben>.Definiere(p =>
    {
        // Zweig A — Bestand reservieren; der Gegenzug gibt die Menge wieder frei.
        p.Auf<BestellungAufgegeben>()
            .Sende<ReserviereBestand>(e => new ReserviereBestand(e.Artikel, e.Menge))
            .RückgängigDurch<GebeBestandFrei>(e => new GebeBestandFrei(e.Artikel, e.Menge));

        // Zweig B — Konto belasten; der Gegenzug erstattet.
        p.Auf<BestellungAufgegeben>()
            .Sende<BelasteKonto>(e => new BelasteKonto(e.Kunde, e.Betrag))
            .RückgängigDurch<ErstatteKonto>(e => new ErstatteKonto(e.Kunde, e.Betrag));

        // Vereinigung (Diamant-Join) — versenden erst, wenn Bestand reserviert UND Konto belastet ist.
        p.Auf<BestellungAufgegeben>().Und<BestandReserviert>().Und<KontoBelastet>()
            .Sende<Versende>((e, b, k) => new Versende(e.Versand, e.Kunde));
    });
}
