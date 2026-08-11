using System.Text.Json.Serialization;
using Abstractions;
using Domain.Antrag;
using Domain.Auftrag;
using Domain.Bestellung;
using Domain.Erinnerung;
using Domain.Flug;
using Domain.Hotel;
using Domain.ImagePair;
using Domain.Konto;
using Domain.Lager;
using Domain.Projections;
using Domain.Reaktion;
using Domain.Reise;
using Domain.Reiseauftrag;
using Domain.Sammelauftrag;
using Domain.Versand;
using Domain.Vorgang;
using Domain.Zahlung;
using Infrastructure.Aggregate;
using Infrastructure.Prozess;
using Infrastructure.Projections;

namespace Infrastructure.Serialization;

/// <summary>
/// STJ-Source-Gen-Manifest für den <b>internen Actor-Plane (Wire)</b> — reflexionsfreie
/// <c>JsonTypeInfo</c> (Invariante 4) für alle cross-node serialisierten Typen. Basis des
/// <see cref="CqrsWireSerializer"/> (Proto.Remote-<c>ISerializer</c>).
///
/// ── Abgrenzung ──
/// NICHT zu verwechseln mit <see cref="EventJsonSerializerContext"/> (Marten-STORAGE, nur
/// persistierbare Events). Dieser Context ist der WIRE-Context und deshalb <b>breiter</b>:
/// die Transport-Hüllen + ALLE <c>ICommand</c> + ALLE <c>IEvent</c> (inkl. <c>ITransientEvent</c>,
/// weil <c>CommandResult.RejectionEvent</c> ein Transient-Event ist).
///
/// ── Warum HAND-geschrieben (analog EventJsonSerializerContext) ──
/// Roslyn-Generatoren sehen die Ausgabe des STJ-Generators NICHT. Ein Generator, der diese
/// <c>[JsonSerializable]</c>-Liste emittiert, würde vom STJ-Generator nicht verarbeitet → keine
/// JsonTypeInfo. Der Drift-Schutz ist COMPILE-ZEIT: der <c>WireSerializerGenerator</c> erzeugt
/// <c>GeneratedWirePoly</c> über dieselbe Command-/Event-Menge und referenziert je Typ
/// <c>Default.{Typ}</c>. Fehlt hier ein Typ, existiert die Property nicht → der Build bricht.
/// Also: neuer Command/Event → EINE <c>[JsonSerializable]</c>-Zeile hier ergänzen.
///
/// Die polymorphen Interface-Payloads (<c>ICommand</c>/<c>IEvent</c>) und der Summentyp
/// <c>CommandModus</c> werden über die eingebackenen Converter (<see cref="IEventJsonConverter"/>
/// etc.) behandelt — deshalb brauchen die Interfaces selbst KEINEN Manifest-Eintrag.
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    Converters = new[]
    {
        typeof(IEventJsonConverter),
        typeof(ICommandJsonConverter),
        typeof(CommandModusJsonConverter),
    })]
// ── Top-Level Wire-Hüllen (IWireMessage) ──
[JsonSerializable(typeof(CommandEnvelope))]
[JsonSerializable(typeof(CommandResult))]
[JsonSerializable(typeof(Wake))]
[JsonSerializable(typeof(WakeAck))]
// ── Commands (GeneratedTypeRegistry.Commands) ──
[JsonSerializable(typeof(Aktiviere))]
[JsonSerializable(typeof(BeauftrageSammelueberweisung))]
[JsonSerializable(typeof(BeauftrageUeberweisung))]
[JsonSerializable(typeof(BelasteKonto))]
[JsonSerializable(typeof(BestaetigeReise))]
[JsonSerializable(typeof(BucheReise))]
[JsonSerializable(typeof(BucheReservierung))]
[JsonSerializable(typeof(EroeffneKonto))]
[JsonSerializable(typeof(ErstatteKonto))]
[JsonSerializable(typeof(ErstelleImagePair))]
[JsonSerializable(typeof(FristFaellig))]
[JsonSerializable(typeof(GebeBestandFrei))]
[JsonSerializable(typeof(GebeFlugFrei))]
[JsonSerializable(typeof(GebeHotelFrei))]
[JsonSerializable(typeof(GebeReservierungFrei))]
[JsonSerializable(typeof(Genehmige))]
[JsonSerializable(typeof(GibBestellungAuf))]
[JsonSerializable(typeof(KlassifiziereBildPaarDurchKi))]
[JsonSerializable(typeof(KlassifiziereEinzelBildDurchKi))]
[JsonSerializable(typeof(LabelBildPaar))]
[JsonSerializable(typeof(LabelBildRegion))]
[JsonSerializable(typeof(LabelEinzelBild))]
[JsonSerializable(typeof(LabelPhysischesProdukt))]
[JsonSerializable(typeof(MarkiereAlsInspiziert))]
[JsonSerializable(typeof(MeldeBildVerfuegbar))]
[JsonSerializable(typeof(ReserviereBestand))]
[JsonSerializable(typeof(ReserviereBetrag))]
[JsonSerializable(typeof(ReserviereFlug))]
[JsonSerializable(typeof(ReserviereHotel))]
[JsonSerializable(typeof(RichteFlugEin))]
[JsonSerializable(typeof(RichteHotelEin))]
[JsonSerializable(typeof(RichteLagerEin))]
[JsonSerializable(typeof(RichteZahlungskontoEin))]
[JsonSerializable(typeof(SchreibeGut))]
[JsonSerializable(typeof(StelleAntrag))]
[JsonSerializable(typeof(StorniereGutschrift))]
[JsonSerializable(typeof(Versende))]
[JsonSerializable(typeof(WirkeReaktion))]
// ── Events (GeneratedTypeRegistry.Events, inkl. ITransientEvent + Prozess-intern) ──
[JsonSerializable(typeof(AntragGestellt))]
[JsonSerializable(typeof(BestandFreigegeben))]
[JsonSerializable(typeof(BestandReichtNicht))]
[JsonSerializable(typeof(BestandReserviert))]
[JsonSerializable(typeof(BestellungAufgegeben))]
[JsonSerializable(typeof(BetragReserviert))]
[JsonSerializable(typeof(BildNichtVerfuegbar))]
[JsonSerializable(typeof(BildPaarDurchKiKlassifiziert))]
[JsonSerializable(typeof(BildPaarGelabelt))]
[JsonSerializable(typeof(BildRegionGelabelt))]
[JsonSerializable(typeof(BildVerfuegbar))]
[JsonSerializable(typeof(BildVersionBereitsVerfuegbar))]
[JsonSerializable(typeof(CommandFailed))]
[JsonSerializable(typeof(DeckungReichtNicht))]
[JsonSerializable(typeof(EinzelBildDurchKiKlassifiziert))]
[JsonSerializable(typeof(EinzelBildGelabelt))]
[JsonSerializable(typeof(ErinnerungAusgeloest))]
[JsonSerializable(typeof(FlugAusgebucht))]
[JsonSerializable(typeof(FlugEingerichtet))]
[JsonSerializable(typeof(FlugExistiertBereits))]
[JsonSerializable(typeof(FlugFreigegeben))]
[JsonSerializable(typeof(FlugReserviert))]
[JsonSerializable(typeof(Gutgeschrieben))]
[JsonSerializable(typeof(GutschriftStorniert))]
[JsonSerializable(typeof(HotelAusgebucht))]
[JsonSerializable(typeof(HotelEingerichtet))]
[JsonSerializable(typeof(HotelExistiertBereits))]
[JsonSerializable(typeof(HotelFreigegeben))]
[JsonSerializable(typeof(HotelReserviert))]
[JsonSerializable(typeof(ImagePairEingabeUngueltig))]
[JsonSerializable(typeof(ImagePairErstellt))]
[JsonSerializable(typeof(ImagePairExistiertBereits))]
[JsonSerializable(typeof(ImagePairInspiziert))]
[JsonSerializable(typeof(ImagePairKomplett))]
[JsonSerializable(typeof(ImagePairNichtGefunden))]
[JsonSerializable(typeof(KommandoAbgelehnt))]
[JsonSerializable(typeof(KommandoVerarbeitet))]
[JsonSerializable(typeof(KontoBelastet))]
[JsonSerializable(typeof(KontoEroeffnet))]
[JsonSerializable(typeof(KontoErstattet))]
[JsonSerializable(typeof(KontoExistiertBereits))]
[JsonSerializable(typeof(KontoGesperrt))]
[JsonSerializable(typeof(KontoNichtGefunden))]
[JsonSerializable(typeof(KontoUngedeckt))]
[JsonSerializable(typeof(LagerEingerichtet))]
[JsonSerializable(typeof(LagerExistiertBereits))]
[JsonSerializable(typeof(PaarNichtKomplett))]
[JsonSerializable(typeof(PhysischesProduktGelabelt))]
[JsonSerializable(typeof(ProzessBeendet))]
[JsonSerializable(typeof(ProzessGestartet))]
[JsonSerializable(typeof(ReaktionGewirkt))]
[JsonSerializable(typeof(RegionIndexUngueltig))]
[JsonSerializable(typeof(RegionLabelsUngueltig))]
[JsonSerializable(typeof(ReiseBestaetigt))]
[JsonSerializable(typeof(ReiseGebucht))]
[JsonSerializable(typeof(ReservierungFreigegeben))]
[JsonSerializable(typeof(ReservierungGebucht))]
[JsonSerializable(typeof(SammelUeberweisungBeauftragt))]
[JsonSerializable(typeof(SchrittGescheitert))]
[JsonSerializable(typeof(UeberweisungBeauftragt))]
[JsonSerializable(typeof(Versendet))]
[JsonSerializable(typeof(VorgangAktiviert))]
[JsonSerializable(typeof(VorgangGenehmigt))]
[JsonSerializable(typeof(ZahlungskontoEingerichtet))]
public partial class CqrsWireJsonContext : JsonSerializerContext
{
}
