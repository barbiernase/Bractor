using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Modell;
using Domain.Trainingslauf;
using Domain.Projections;

namespace Domain.Akteure;

// Akteure (docs/konzept-akteure.md): wer von außen hineingibt — und nur das, was er darf (IDarf<T>).
// Was ein Akteur hören darf, leitet der Generator aus dem Graphen ab (Aggregate seiner Commands + Projektionen
// hinter seinen Queries). Hier liegen sie, weil dieses Projekt Commands UND Queries sieht.

/// <summary>Mensch an der Bild-Inspektion: labelt Einzelbilder und markiert Paare als inspiziert.</summary>
public sealed record Inspektor : IAkteur,
    IDarf<LabelEinzelBild>, IDarf<MarkiereAlsInspiziert>,
    IDarf<SucheImagePairs>, IDarf<GetImagePair>;

/// <summary>Mensch im ML-Betrieb: stellt Datensätze zusammen, startet Trainings, schaltet Modelle.</summary>
public sealed record Trainer : IAkteur,
    IDarf<ErstelleDatensatz>, IDarf<StarteTraining>, IDarf<SetzeModellAktiv>, IDarf<ArchiviereModell>,
    IDarf<HoleDatensaetze>, IDarf<HoleTrainingslaeufe>, IDarf<HoleModelle>;

/// <summary>Fremdsystem: der Python-Klassifikator (Domain.Client.Worker.Python.ML/classifier.py).</summary>
public sealed record KlassifikationsWorker : IAkteur,
    IDarf<KlassifiziereBildPaarDurchKi>;

/// <summary>Fremdsystem: der Python-Trainings-Worker (Domain.Client.Worker.Python.ML/training_worker.py).</summary>
public sealed record TrainingsWorker : IAkteur,
    IDarf<MeldeTrainingBegonnen>, IDarf<MeldeFortschritt>, IDarf<MeldeTrainingAbgeschlossen>, IDarf<MeldeTrainingGescheitert>,
    IDarf<HoleDatensatzSamples>;
