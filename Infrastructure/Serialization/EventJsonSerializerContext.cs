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

namespace Infrastructure.Serialization;

/// <summary>
/// STJ-Source-Gen-Manifest über ALLE persistierbaren Events (== <c>GeneratedTypeRegistry.PersistableEvents</c>).
/// Der eingebaute <c>System.Text.Json.SourceGeneration</c>-Generator erzeugt daraus reflection-freie
/// <c>JsonTypeInfo</c> (schnelle Metadaten statt Laufzeit-Reflection) — die Grundlage des STJ-Speedups.
///
/// ── Warum HAND-geschrieben und nicht generiert ──
/// Roslyn-Generatoren sehen die Ausgabe ANDERER Generatoren nicht. Ein eigener Generator, der diese
/// <c>[JsonSerializable]</c>-Liste emittiert, würde vom STJ-Generator NICHT verarbeitet → keine JsonTypeInfo.
/// Diese Liste ist reine DATEN (Typmenge), kein Dispatch. Der Drift-Schutz ist COMPILE-ZEIT: der
/// <c>EventJsonGenerator</c> erzeugt <c>GeneratedEventJson.Serialize/Deserialize</c> über dieselbe
/// <c>PersistableEvents</c>-Menge und referenziert je Event <c>Default.{Event}</c>. Fehlt hier ein Event,
/// existiert die Kontext-Property nicht → der Build bricht. Also: neues persistierbares Event →
/// EINE <c>[JsonSerializable]</c>-Zeile hier ergänzen (sonst schlägt der Build fehl, kein stiller Drift).
///
/// Metadata-Mode: nötig, damit die JsonTypeInfo mit Martens eigener <c>JsonSerializerOptions</c> und einer
/// Resolver-Chain (Source-Gen für Events + Reflection-Fallback für Dokumente) kombinierbar ist.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
// ── ImagePair ──
[JsonSerializable(typeof(ImagePairErstellt))]
[JsonSerializable(typeof(BildVerfuegbar))]
[JsonSerializable(typeof(ImagePairKomplett))]
[JsonSerializable(typeof(EinzelBildDurchKiKlassifiziert))]
[JsonSerializable(typeof(BildPaarDurchKiKlassifiziert))]
[JsonSerializable(typeof(BildRegionGelabelt))]
[JsonSerializable(typeof(EinzelBildGelabelt))]
[JsonSerializable(typeof(BildPaarGelabelt))]
[JsonSerializable(typeof(PhysischesProduktGelabelt))]
[JsonSerializable(typeof(ImagePairInspiziert))]
// ── Konto ──
[JsonSerializable(typeof(KontoEroeffnet))]
[JsonSerializable(typeof(KontoBelastet))]
[JsonSerializable(typeof(KontoErstattet))]
[JsonSerializable(typeof(Gutgeschrieben))]
[JsonSerializable(typeof(GutschriftStorniert))]
[JsonSerializable(typeof(BetragReserviert))]
[JsonSerializable(typeof(ReservierungGebucht))]
[JsonSerializable(typeof(ReservierungFreigegeben))]
// ── Lager / Versand / Zahlung / Bestellung ──
[JsonSerializable(typeof(BestandReserviert))]
[JsonSerializable(typeof(BestandFreigegeben))]
[JsonSerializable(typeof(LagerEingerichtet))]
[JsonSerializable(typeof(LagerEingerichtet_V1))] // frühere Gestalt (Schema-Evolution) — nur lesbar
[JsonSerializable(typeof(Versendet))]
[JsonSerializable(typeof(ZahlungskontoEingerichtet))]
[JsonSerializable(typeof(BestellungAufgegeben))]
// ── Auftrag / Sammelauftrag ──
[JsonSerializable(typeof(UeberweisungBeauftragt))]
[JsonSerializable(typeof(SammelUeberweisungBeauftragt))]
// ── Reise / Reiseauftrag / Flug / Hotel ──
[JsonSerializable(typeof(FlugEingerichtet))]
[JsonSerializable(typeof(FlugReserviert))]
[JsonSerializable(typeof(FlugFreigegeben))]
[JsonSerializable(typeof(HotelEingerichtet))]
[JsonSerializable(typeof(HotelReserviert))]
[JsonSerializable(typeof(HotelFreigegeben))]
[JsonSerializable(typeof(ReiseGebucht))]
[JsonSerializable(typeof(ReiseBestaetigt))]
// ── Antrag / Vorgang / Erinnerung ──
[JsonSerializable(typeof(AntragGestellt))]
[JsonSerializable(typeof(VorgangAktiviert))]
[JsonSerializable(typeof(VorgangGenehmigt))]
[JsonSerializable(typeof(ErinnerungAusgeloest))]
// ── Reaktion ──
[JsonSerializable(typeof(ReaktionGewirkt))]
// ── Prozess-intern (IProzessIntern, aber IEvent → persistiert) ──
[JsonSerializable(typeof(ProzessGestartet))]
[JsonSerializable(typeof(ProzessBeendet))]
[JsonSerializable(typeof(SchrittGescheitert))]
[JsonSerializable(typeof(KommandoVerarbeitet))]
[JsonSerializable(typeof(KommandoAbgelehnt))]
public partial class EventJsonSerializerContext : JsonSerializerContext
{
}
