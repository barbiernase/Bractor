using Abstractions;
using Domain.Konto;

namespace Domain.Willkommensbonus;

/// <summary>
/// Der Willkommensbonus als ECHTE, aggregat-übergreifende Überweisung: der Bonus wird vom
/// Promo-Konto der Bank auf das neue Konto gebucht — reservieren auf dem Promo-Konto, gutschreiben
/// auf dem neuen, dann buchen (Join + Kompensation, Petri-Netz wie bei der Überweisung).
///
/// Die Konten-Ids kommen als DOMÄNEN-Referenzen aus dem Auslöser (<c>NeuesKonto</c>/<c>PromoKonto</c>),
/// nicht aus der Korrelation — genau deshalb ist hier ein Prozess das richtige Werkzeug (mehrere
/// Aggregate, mit Kompensation), und nicht der Decider eines einzelnen Aggregats. Deckt das
/// Promo-Konto den Bonus nicht, hängt die Saga am Join und macht die bereits gutgeschriebene Seite rückgängig.
/// </summary>
public sealed class WillkommensbonusProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<WillkommensbonusBeauftragt>.Definiere(p =>
    {
        p.Auf<WillkommensbonusBeauftragt>()
            .Sende<ReserviereBetrag>(t => new ReserviereBetrag(t.PromoKonto, t.Betrag))
            .RückgängigDurch<GebeReservierungFrei>(t => new GebeReservierungFrei(t.PromoKonto, t.Betrag));

        p.Auf<WillkommensbonusBeauftragt>().Und<BetragReserviert>()
            .Sende<SchreibeGut>((t, r) => new SchreibeGut(t.NeuesKonto, t.Betrag))
            .RückgängigDurch<StorniereGutschrift>((t, r) => new StorniereGutschrift(t.NeuesKonto, t.Betrag));

        // Join: buchen erst nach Reservieren UND Gutschreiben.
        p.Auf<WillkommensbonusBeauftragt>().Und<BetragReserviert>().Und<Gutgeschrieben>()
            .Sende<BucheReservierung>((t, r, g) => new BucheReservierung(t.PromoKonto, t.Betrag));
    });
}
