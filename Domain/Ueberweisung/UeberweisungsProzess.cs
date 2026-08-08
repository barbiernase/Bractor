using Abstractions;
using Domain.Auftrag;
using Domain.Konto;

namespace Domain.Ueberweisung;

/// <summary>
/// Der Überweisungs-Prozess als typisierte Event→Command-Regeln (Spec §3) — ersetzt den alten
/// <c>UeberweisungsPlan</c> (Schrittliste). Ein Petri-Netz: reservieren → gutschreiben → buchen, mit
/// Join und Datenfluss. Der Prozess-Name ist der Klassenname (<c>UeberweisungsProzess</c>) — daraus leitet
/// der Korrelations-Router die deterministische Korrelation ab.
///
/// Der Betrag reist mengenbasiert durch den Auslöser; die Buchung feuert erst nach Reservieren UND
/// Gutschreiben (Join). Idempotenz/Kompensation sichert das Framework über die deterministische CommandId —
/// die Aggregate bleiben rein. Kein Handle, keine Nummer — die Abhängigkeit IST der Event-Typ.
/// </summary>
public sealed class UeberweisungsProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<UeberweisungBeauftragt>.Definiere(p =>
    {
        p.Auf<UeberweisungBeauftragt>()
            .Sende<ReserviereBetrag>(t => new ReserviereBetrag(t.Quelle, t.Betrag))
            .RückgängigDurch<GebeReservierungFrei>(t => new GebeReservierungFrei(t.Quelle, t.Betrag));

        p.Auf<UeberweisungBeauftragt>().Und<BetragReserviert>()
            .Sende<SchreibeGut>((t, r) => new SchreibeGut(t.Ziel, t.Betrag))
            .RückgängigDurch<StorniereGutschrift>((t, r) => new StorniereGutschrift(t.Ziel, t.Betrag));

        // Join: buchen erst nach Reservieren UND Gutschreiben (der Betrag kommt aus dem Auslöser).
        p.Auf<UeberweisungBeauftragt>().Und<BetragReserviert>().Und<Gutgeschrieben>()
            .Sende<BucheReservierung>((t, r, g) => new BucheReservierung(t.Quelle, t.Betrag));
    });
}
