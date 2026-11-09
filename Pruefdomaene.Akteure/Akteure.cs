using Abstractions;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Auftrag;
using Pruefdomaene.Import;
using Pruefdomaene.Kontingent;
using Pruefdomaene.Messung;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;

namespace Pruefdomaene.Akteure;

// Die Akteure der Prüfdomäne — wer von draußen hineingibt (IDarf) und was zu ihm kommt (IHoert).

/// <summary>Prüft große Rechnungen von Hand: auf eine vorgelegte Rechnung (<see cref="RechnungZurPruefungVorgelegt"/>) gibt er sie frei oder sperrt sie.</summary>
public sealed record Rechnungspruefer : IAkteur,
    IHoert<RechnungZurPruefungVorgelegt>,
    IDarf<GibRechnungFrei>, IDarf<SperreRechnung>;

/// <summary>Fordert Hochrechnungen für Messstellen an (ein Client-Eingang: über den Katalog Rechenwerk an die Messstelle, docs/04 §4.4).</summary>
public sealed record Messwart : IAkteur,
    IDarf<Pruefdomaene.Messung.HochrechnungAngefordert>;

/// <summary>
/// Die Sachbearbeitung (docs/04 §4.4: ein Decide gibt nur Fakten aus, was danach geschieht, stößt ein Client an): auf jedes Fakt, das einen
/// Ablauf beginnt oder fortsetzt, gibt sie die Nachricht der Domäne an einen Graphen des Katalogs (Freigabe, Mahnung, Messwert, Import) oder die
/// Commands an die Aggregate selbst (Gutschrift, Auftrag samt Gegenzügen, Verteilung und Zubuchung, Nachschub, und die Vermerke der Antworten).
/// </summary>
public sealed record Sachbearbeitung : IAkteur,
    IHoert<RechnungGestellt>, IHoert<RechnungBezahlt>, IHoert<ZahlungUeberfaellig>,
    IHoert<AuftragGestartet>, IHoert<PostenfreigabeAngefordert>, IHoert<KontingententlastungAngefordert>,
    IHoert<KontingentBelegt>, IHoert<BelegungAbgelehnt>,
    IHoert<VerteilungGestartet>, IHoert<QuelleReserviert>, IHoert<PostenZugebucht>,
    IHoert<PostenReserviert>, IHoert<ReservierungAbgelehnt>,
    IHoert<MesswertErfasst>,
    IHoert<ImportGestartet>,
    IDarf<Freizugeben>, IDarf<BucheGutschrift>, IDarf<Mahnfall>,
    IDarf<ReservierePosten>, IDarf<BelegeKontingent>, IDarf<GibPostenFrei>, IDarf<EntlasteKontingent>,
    IDarf<VermerkeReservierung>, IDarf<VermerkeBelegung>, IDarf<VermerkeAbsage>,
    IDarf<VermerkeQuellreservierung>, IDarf<VermerkeQuellabsage>, IDarf<BuchePostenZu>, IDarf<VermerkeZubuchung>,
    IDarf<NeuerWert>,
    IDarf<ImportZuLaufen>;
