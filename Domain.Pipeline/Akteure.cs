using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Modell;
using Domain.Pipeline.ImageProcessing;
using Domain.Projections;
using Domain.Sammelvorgang;
using Domain.Teilauftrag;
using Domain.Trainingslauf;

namespace Domain.Akteure;

// Die Akteure der Domäne — Domänen-Experten, von denen alles kommt, was hineingeht (docs/konzept-akteure.md §8).
// Deklariert wird nur, was ein Akteur SELBST hineingibt (IDarf<T>). Was eine Pipeline, ein Prozess oder eine Frist daraus
// erzeugt (NimmRangeAuf, ErstelleImagePair, MarkiereAlsHaengengeblieben …), trägt den Akteur der Kette — es steht hier
// bewusst NICHT: am Tor käme es so nie durch („nie von der GUI" ist ein Typ-Fakt). Ein Command darf bei mehreren Akteuren
// stehen. Hier liegen sie, weil dieses Projekt Commands, Queries UND Pipeline-Trigger sieht.

// ── Maschinen ──────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Nimmt die Bildpaare (dc0/dc2) an der Linie auf und legt sie auf dem Share ab.</summary>
public sealed record KameraSystem : IMaschine,
    IDarf<DateiErkannt>;

/// <summary>GPU-Worker: trainiert auf einer eingefrorenen Datensatz-Version und meldet den Fortschritt.</summary>
public sealed record TrainingsSystem : IMaschine,
    IDarf<MeldeTrainingBegonnen>, IDarf<MeldeFortschritt>, IDarf<MeldeTrainingAbgeschlossen>, IDarf<MeldeTrainingGescheitert>,
    IDarf<HoleDatensatzSamples>;

// ── KI ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Beurteilt Bildpaare und Einzelbilder mit dem aktiven Modell (Strang 1).</summary>
public sealed record Klassifizierer : IKi,
    IDarf<KlassifiziereBildPaarDurchKi>, IDarf<KlassifiziereEinzelBildDurchKi>,
    IDarf<HoleAktivesModell>;

// ── Menschen ───────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Befundet Kamerabilder am Bildschirm (Strang 2) und markiert Paare als inspiziert.</summary>
public sealed record Inspekteur : IMensch,
    IDarf<LabelBildPaar>, IDarf<LabelEinzelBild>, IDarf<LabelBildRegion>, IDarf<MarkiereAlsInspiziert>,
    IDarf<SucheImagePairs>, IDarf<GetImagePair>, IDarf<GetImagePairHistorie>, IDarf<GetImagePairStatistik>,
    IDarf<GetProduktionsTage>, IDarf<GetProduktionsStrip>, IDarf<GetProduktionsVerlauf>, IDarf<GetUnklassifizierteImagePairs>;

/// <summary>Prüft das physische Teil und setzt damit die Ground Truth (Strang 3) — nicht am Bildschirm.</summary>
public sealed record Produktpruefer : IMensch,
    IDarf<LabelPhysischesProdukt>,
    IDarf<GetImagePair>, IDarf<SucheImagePairs>;

/// <summary>Stellt Datensätze zusammen, kuratiert, startet/bricht Trainings ab und registriert Modelle.</summary>
public sealed record KIOperator : IMensch,
    IDarf<ErstelleDatensatz>, IDarf<FuegeRangeHinzu>, IDarf<NimmPaarAuf>, IDarf<EntfernePaar>, IDarf<SetzeSplit>, IDarf<FriereEin>,
    IDarf<StarteTraining>, IDarf<BricheTrainingAb>, IDarf<RegistriereModell>,
    IDarf<HoleDatensaetze>, IDarf<HoleDatensatz>, IDarf<HoleDatensatzPaare>, IDarf<HoleDatensaetzeFuerPaar>,
    IDarf<HoleTrainingslaeufe>, IDarf<HoleTrainingslauf>,
    IDarf<HoleModelle>, IDarf<SucheImagePairs>, IDarf<GetImagePair>;

/// <summary>Schaltet ein Modell für die Produktions-Inferenz scharf oder zieht es zurück (Freigabe ≠ Training).</summary>
public sealed record Modellfreigeber : IMensch,
    IDarf<SetzeModellAktiv>, IDarf<ArchiviereModell>,
    IDarf<HoleModelle>, IDarf<HoleAktivesModell>;

/// <summary>Beispiel-Domäne Sammelvorgang: startet Sammelvorgänge und ihre Teilaufträge und schließt Teilaufträge ab.</summary>
public sealed record Disponent : IMensch,
    IDarf<StarteSammelvorgang>, IDarf<StarteTeilauftrag>, IDarf<SchließeTeilauftragAb>;
