using System.Text.Json.Serialization;
using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Projections;
using Domain.Trainingslauf;
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
// ── Lager / Versand / Zahlung / Bestellung ──
// ── Auftrag / Sammelauftrag ──
// ── Reise / Reiseauftrag / Flug / Hotel ──
// ── Antrag / Vorgang / Erinnerung ──
// ── Verkauf (DDD-Muster-Aggregat: nur PERSISTENTE Events; Value Object Geldwert reist transitiv mit) ──
// ── Datensatz (nur PERSISTENTE Events; Value Objects reisen transitiv mit) ──
[JsonSerializable(typeof(DatensatzErstellt))]
[JsonSerializable(typeof(RangeAngefordert))]
[JsonSerializable(typeof(PaareAufgenommen))]
[JsonSerializable(typeof(PaarAufgenommen))]
[JsonSerializable(typeof(PaarEntfernt))]
[JsonSerializable(typeof(SplitGesetzt))]
[JsonSerializable(typeof(EinfrierenAngefordert))]
[JsonSerializable(typeof(DatensatzEingefroren))]
// ── Trainingslauf (nur PERSISTENTE Events) ──
[JsonSerializable(typeof(TrainingAngefordert))]
[JsonSerializable(typeof(TrainingBegonnen))]
[JsonSerializable(typeof(TrainingFortschritt))]
[JsonSerializable(typeof(TrainingAbgeschlossen))]
[JsonSerializable(typeof(TrainingGescheitert))]
[JsonSerializable(typeof(TrainingAbgebrochen))]
[JsonSerializable(typeof(TrainingHaengengeblieben))]
// ── Reaktion ──
// ── Prozess-intern (IProzessIntern, aber IEvent → persistiert) ──
[JsonSerializable(typeof(ProzessGestartet))]
[JsonSerializable(typeof(ProzessBeendet))]
[JsonSerializable(typeof(SchrittGescheitert))]
[JsonSerializable(typeof(KommandoVerarbeitet))]
[JsonSerializable(typeof(KommandoAbgelehnt))]
public partial class EventJsonSerializerContext : JsonSerializerContext
{
}
