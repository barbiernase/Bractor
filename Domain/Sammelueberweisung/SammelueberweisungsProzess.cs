using Abstractions;
using Domain.Konto;
using Domain.Sammelauftrag;

namespace Domain.Sammelueberweisung;

/// <summary>
/// Der Sammelüberweisungs-Prozess (Spec §6/§8) — beweist FAN-OUT / DYNAMISCHE BREITE: EIN Auslöser mit
/// N Zielen, N parallele Gutschriften, gebucht erst wenn ALLE da sind. Ersetzt den alten
/// <c>SammelueberweisungsPlan</c>.
///
/// - reservieren: der Gesamtbetrag; Gegenzug gibt GENAU diese Reservierung frei.
/// - gutschreiben: <c>SendeJe</c> erzeugt aus EINEM Match N Commands (je Ziel eins) — jeder mit eigenem
///   deterministischen Vorgang (Diskriminator = Ziel-AggregateId), Fan-out ohne Zähler.
/// - buchen: <c>UndAlle&lt;Gutgeschrieben&gt;(N)</c> — Count-Join, feuert erst, wenn alle N Gutschriften da sind;
///   bucht die reservierte Gesamtsumme. Die Breite N steht im Auslöser (Ziele.Count) — kein Zähler.
/// </summary>
public sealed class SammelueberweisungsProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<SammelUeberweisungBeauftragt>.Definiere(p =>
    {
        p.Auf<SammelUeberweisungBeauftragt>()
            .Sende<ReserviereBetrag>(t => new ReserviereBetrag(t.Quelle, t.BetragProZiel * t.Ziele.Count))
            .RückgängigDurch<GebeReservierungFrei>(t => new GebeReservierungFrei(t.Quelle, t.BetragProZiel * t.Ziele.Count));

        p.Auf<SammelUeberweisungBeauftragt>().Und<BetragReserviert>()
            .SendeJe<SchreibeGut>((t, r) => t.Ziele.Select(z => new SchreibeGut(z, t.BetragProZiel)));

        p.Auf<SammelUeberweisungBeauftragt>().Und<BetragReserviert>().UndAlle<Gutgeschrieben>(t => t.Ziele.Count)
            .Sende<BucheReservierung>((t, r, gutschriften) => new BucheReservierung(t.Quelle, t.BetragProZiel * t.Ziele.Count));
    });
}
