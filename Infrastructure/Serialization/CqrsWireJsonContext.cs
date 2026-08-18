using System.Text.Json.Serialization;
using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Pipeline.Benchmark;
using Domain.Pipeline.ImageProcessing;
using Domain.Projections;
using Domain.Trainingslauf;
using Infrastructure.Aggregate;
using Infrastructure.Prozess;
using Infrastructure.Projections;
using Infrastructure.PubSub.Messages;

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
        typeof(IStateChangeSignalJsonConverter),
        typeof(IMessageEnvelopeJsonConverter),
        typeof(PidJsonConverter),
    })]
// ── Top-Level Wire-Hüllen (IWireMessage) — Iteration 1 ──
[JsonSerializable(typeof(CommandEnvelope))]
[JsonSerializable(typeof(CommandResult))]
[JsonSerializable(typeof(Wake))]
[JsonSerializable(typeof(WakeAck))]
// ── Top-Level Wire-Hüllen (IWireMessage) — Iteration 2 (PubSub/Pipeline/Prozess) ──
[JsonSerializable(typeof(EventEnvelope))]
[JsonSerializable(typeof(SignalEnvelope))]
[JsonSerializable(typeof(Publish))]
[JsonSerializable(typeof(Subscribe))]
[JsonSerializable(typeof(Unsubscribe))]
[JsonSerializable(typeof(Ack))]
[JsonSerializable(typeof(Activate))]
[JsonSerializable(typeof(GetSubscriberCount))]
[JsonSerializable(typeof(SubscriberCountResponse))]
[JsonSerializable(typeof(PipelineAck))]
[JsonSerializable(typeof(ProzessWake))]
// ── Commands (GeneratedTypeRegistry.Commands) ──
// ── Datensatz-Commands ──
[JsonSerializable(typeof(ErstelleDatensatz))]
[JsonSerializable(typeof(FuegeRangeHinzu))]
[JsonSerializable(typeof(NimmRangeAuf))]
[JsonSerializable(typeof(NimmPaarAuf))]
[JsonSerializable(typeof(EntfernePaar))]
[JsonSerializable(typeof(SetzeSplit))]
[JsonSerializable(typeof(FriereEin))]
[JsonSerializable(typeof(SchliesseEinfrierenAb))]
// ── Trainingslauf-Commands ──
[JsonSerializable(typeof(StarteTraining))]
[JsonSerializable(typeof(BricheTrainingAb))]
[JsonSerializable(typeof(MeldeTrainingBegonnen))]
[JsonSerializable(typeof(MeldeFortschritt))]
[JsonSerializable(typeof(MeldeTrainingAbgeschlossen))]
[JsonSerializable(typeof(MeldeTrainingGescheitert))]
[JsonSerializable(typeof(MarkiereAlsHaengengeblieben))]
[JsonSerializable(typeof(ErstelleImagePair))]
[JsonSerializable(typeof(KlassifiziereBildPaarDurchKi))]
[JsonSerializable(typeof(KlassifiziereEinzelBildDurchKi))]
[JsonSerializable(typeof(LabelBildPaar))]
[JsonSerializable(typeof(LabelBildRegion))]
[JsonSerializable(typeof(LabelEinzelBild))]
[JsonSerializable(typeof(LabelPhysischesProdukt))]
[JsonSerializable(typeof(MarkiereAlsInspiziert))]
[JsonSerializable(typeof(MeldeBildVerfuegbar))]
// ── Events (GeneratedTypeRegistry.Events, inkl. ITransientEvent + Prozess-intern) ──
// ── Datensatz-Events (inkl. Ablehnungen als ITransientEvent) ──
[JsonSerializable(typeof(DatensatzErstellt))]
[JsonSerializable(typeof(RangeAngefordert))]
[JsonSerializable(typeof(PaareAufgenommen))]
[JsonSerializable(typeof(PaarAufgenommen))]
[JsonSerializable(typeof(PaarEntfernt))]
[JsonSerializable(typeof(SplitGesetzt))]
[JsonSerializable(typeof(EinfrierenAngefordert))]
[JsonSerializable(typeof(DatensatzEingefroren))]
[JsonSerializable(typeof(DatensatzExistiertBereits))]
[JsonSerializable(typeof(DatensatzBereitsEingefroren))]
[JsonSerializable(typeof(DatensatzLeer))]
[JsonSerializable(typeof(RangeLeer))]
[JsonSerializable(typeof(SplitUngueltig))]
// ── Trainingslauf-Events (inkl. Ablehnungen als ITransientEvent) ──
[JsonSerializable(typeof(TrainingAngefordert))]
[JsonSerializable(typeof(TrainingBegonnen))]
[JsonSerializable(typeof(TrainingFortschritt))]
[JsonSerializable(typeof(TrainingAbgeschlossen))]
[JsonSerializable(typeof(TrainingGescheitert))]
[JsonSerializable(typeof(TrainingAbgebrochen))]
[JsonSerializable(typeof(TrainingHaengengeblieben))]
[JsonSerializable(typeof(TrainingslaufExistiertBereits))]
[JsonSerializable(typeof(TrainingslaufNichtGefunden))]
[JsonSerializable(typeof(TrainingNichtAktiv))]
[JsonSerializable(typeof(TrainingBereitsBeendet))]
[JsonSerializable(typeof(BildNichtVerfuegbar))]
[JsonSerializable(typeof(BildPaarDurchKiKlassifiziert))]
[JsonSerializable(typeof(BildPaarGelabelt))]
[JsonSerializable(typeof(BildRegionGelabelt))]
[JsonSerializable(typeof(BildVerfuegbar))]
[JsonSerializable(typeof(BildVersionBereitsVerfuegbar))]
[JsonSerializable(typeof(CommandFailed))]
[JsonSerializable(typeof(EinzelBildDurchKiKlassifiziert))]
[JsonSerializable(typeof(EinzelBildGelabelt))]
[JsonSerializable(typeof(ImagePairEingabeUngueltig))]
[JsonSerializable(typeof(ImagePairErstellt))]
[JsonSerializable(typeof(ImagePairExistiertBereits))]
[JsonSerializable(typeof(ImagePairInspiziert))]
[JsonSerializable(typeof(ImagePairKomplett))]
[JsonSerializable(typeof(ImagePairNichtGefunden))]
[JsonSerializable(typeof(KommandoAbgelehnt))]
[JsonSerializable(typeof(KommandoVerarbeitet))]
[JsonSerializable(typeof(PaarNichtKomplett))]
[JsonSerializable(typeof(PhysischesProduktGelabelt))]
[JsonSerializable(typeof(ProzessBeendet))]
[JsonSerializable(typeof(ProzessGestartet))]
[JsonSerializable(typeof(RegionIndexUngueltig))]
[JsonSerializable(typeof(RegionLabelsUngueltig))]
[JsonSerializable(typeof(SchrittGescheitert))]
// ── Pipeline-Trigger (GeneratedTypeRegistry.Triggers) ──
[JsonSerializable(typeof(BenchPing))]
[JsonSerializable(typeof(DateiErkannt))]
// ── Signale (GeneratedTypeRegistry.Signals, StateChangeVia{Event}) ──
// ── Datensatz-Signale (nur persistierbare Events) ──
[JsonSerializable(typeof(StateChangeViaDatensatzErstellt))]
[JsonSerializable(typeof(StateChangeViaRangeAngefordert))]
[JsonSerializable(typeof(StateChangeViaPaareAufgenommen))]
[JsonSerializable(typeof(StateChangeViaPaarAufgenommen))]
[JsonSerializable(typeof(StateChangeViaPaarEntfernt))]
[JsonSerializable(typeof(StateChangeViaSplitGesetzt))]
[JsonSerializable(typeof(StateChangeViaEinfrierenAngefordert))]
[JsonSerializable(typeof(StateChangeViaDatensatzEingefroren))]
// ── Trainingslauf-Signale (nur persistierbare Events) ──
[JsonSerializable(typeof(StateChangeViaTrainingAngefordert))]
[JsonSerializable(typeof(StateChangeViaTrainingBegonnen))]
[JsonSerializable(typeof(StateChangeViaTrainingFortschritt))]
[JsonSerializable(typeof(StateChangeViaTrainingAbgeschlossen))]
[JsonSerializable(typeof(StateChangeViaTrainingGescheitert))]
[JsonSerializable(typeof(StateChangeViaTrainingAbgebrochen))]
[JsonSerializable(typeof(StateChangeViaTrainingHaengengeblieben))]
[JsonSerializable(typeof(StateChangeViaBildPaarDurchKiKlassifiziert))]
[JsonSerializable(typeof(StateChangeViaBildPaarGelabelt))]
[JsonSerializable(typeof(StateChangeViaBildRegionGelabelt))]
[JsonSerializable(typeof(StateChangeViaBildVerfuegbar))]
[JsonSerializable(typeof(StateChangeViaEinzelBildDurchKiKlassifiziert))]
[JsonSerializable(typeof(StateChangeViaEinzelBildGelabelt))]
[JsonSerializable(typeof(StateChangeViaImagePairErstellt))]
[JsonSerializable(typeof(StateChangeViaImagePairInspiziert))]
[JsonSerializable(typeof(StateChangeViaImagePairKomplett))]
[JsonSerializable(typeof(StateChangeViaKommandoAbgelehnt))]
[JsonSerializable(typeof(StateChangeViaKommandoVerarbeitet))]
[JsonSerializable(typeof(StateChangeViaPhysischesProduktGelabelt))]
[JsonSerializable(typeof(StateChangeViaProzessBeendet))]
[JsonSerializable(typeof(StateChangeViaProzessGestartet))]
[JsonSerializable(typeof(StateChangeViaSchrittGescheitert))]
// ── Verkauf (DDD-Muster-Aggregat: Value Object Geldwert reist transitiv mit) ──
public partial class CqrsWireJsonContext : JsonSerializerContext
{
}
