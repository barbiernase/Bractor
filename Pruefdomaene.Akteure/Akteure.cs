using Abstractions;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Auftrag;
using Pruefdomaene.Import;
using Pruefdomaene.Messung;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;
using Pruefdomaene.Pipeline.Abrechnung;
using Pruefdomaene.Pipeline.Auftrag;
using Pruefdomaene.Pipeline.Import;
using Pruefdomaene.Pipeline.Messung;
using Pruefdomaene.Pipeline.Posten;
using Pruefdomaene.Pipeline.Verteilung;

namespace Pruefdomaene.Akteure;

// Die Akteure der Prüfdomäne — wer von draußen hineingibt (IDarf) und was zu ihm kommt (IHoert).

/// <summary>Prüft große Rechnungen von Hand: auf eine vorgelegte Rechnung (<see cref="RechnungZurPruefungVorgelegt"/>) gibt er sie frei oder sperrt sie.</summary>
public sealed record Rechnungspruefer : IAkteur,
    IHoert<RechnungZurPruefungVorgelegt>,
    IDarf<GibRechnungFrei>, IDarf<SperreRechnung>;

/// <summary>Fordert Hochrechnungen für Messstellen an (ein Client-Eingang: über den Katalog Rechenwerk an die Messstelle, docs/04 §4.4).</summary>
public sealed record Messwart : IAkteur,
    IDarf<Pruefdomaene.Pipeline.Skalierung.HochrechnungAngefordert>;

/// <summary>
/// Die Sachbearbeitung (Schritt 6: ein Decide gibt nur Fakten aus, was danach geschieht, stößt ein Client an): auf jedes Fakt, das einen Ablauf
/// beginnt oder fortsetzt, gibt sie den Command an den Kopf seines Graphen — die Rechnung zur Freigabe, Gutschrift und Mahnung, den Auftrag zur
/// Abwicklung samt Gegenzügen, die Verteilung zur Abwicklung und Zubuchung, den unterschrittenen Posten zum Nachschub, den Messwert zur
/// Merkmalsbildung, den Import zum Lauf.
/// </summary>
public sealed record Sachbearbeitung : IAkteur,
    IHoert<RechnungGestellt>, IHoert<RechnungBezahlt>, IHoert<ZahlungUeberfaellig>,
    IHoert<AuftragGestartet>, IHoert<PostenfreigabeAngefordert>, IHoert<KontingententlastungAngefordert>,
    IHoert<VerteilungGestartet>, IHoert<QuelleReserviert>,
    IHoert<PostenReserviert>,
    IHoert<MesswertErfasst>,
    IHoert<ImportGestartet>,
    IDarf<Freizugeben>, IDarf<Gutschreibung>, IDarf<Mahnfall>,
    IDarf<AuftragAbzuwickeln>, IDarf<PostenFreizugeben>, IDarf<KontingentZuEntlasten>,
    IDarf<VerteilungAbzuwickeln>, IDarf<Zubuchen>,
    IDarf<Unterschritten>,
    IDarf<NeuerWert>,
    IDarf<ImportZuLaufen>;
