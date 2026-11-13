using Abstractions;
using Bildvorschau.Katalog;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Modell;
using Domain.Projections;
using Domain.Sammelvorgang;
using Domain.Teilauftrag;
using Domain.Trainingslauf;
using Domain.Projections.Datensatz;
using Domain.Projections.ImagePair;
using Domain.Projections.Modell;
using Domain.Projections.Trainingslauf;

namespace Domain.Akteure;

// Die Akteure der Domäne — Domänen-Experten draußen (docs/konzept-akteure.md §2). Je Akteur steht, was er SELBST hineingibt (IDarf<T>: ein Command,
// eine Query, ein Hinweis) und was zu ihm kommt (IHoert<T>): ein Event oder Hinweis — ein Fakt, auf das sein Client reagiert oder das er zur Kenntnis
// nimmt. Was eine Funktion oder eine Frist
// erzeugt (NimmRangeAuf, MarkiereAlsHaengengeblieben …), trägt den Akteur der Kette — es steht hier bewusst NICHT: am Tor käme es so nie durch.
// Welche Software für welche Akteure spricht, steht in Clients.cs; wie ein Akteur aus dem, was eine Quelle meldet, seine Nachricht macht, in
// Uebersetzungen.cs.

// ── Maschinen ──────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Nimmt die Bildpaare (dc0/dc2) an der Linie auf und legt sie auf dem Share ab; jede fertige Datei gibt es zum Lesen in die Bildaufbereitung. Verwirklicht
/// wird es durch einen Dateiwächter (Übersetzung in Uebersetzungen.cs, Eingänge des Linienrechners in der Umgebung).
/// </summary>
public sealed record KameraSystem : IAkteur,
    IDarf<LiesBild>;

/// <summary>
/// GPU-Worker: reagiert auf <see cref="TrainingAngefordert"/> — trainiert auf der eingefrorenen Datensatz-Version und meldet den Fortschritt; ob der
/// Lauf inzwischen abgebrochen ist, liest er zwischen den Epochen (<see cref="HoleTrainingslauf"/>).
/// </summary>
public sealed record TrainingsSystem : IAkteur,
    IHoert<TrainingAngefordert>,
    IDarf<MeldeTrainingBegonnen>, IDarf<MeldeFortschritt>, IDarf<MeldeTrainingAbgeschlossen>, IDarf<MeldeTrainingGescheitert>,
    IDarf<HoleDatensatzSamples>, IDarf<HoleTrainingslauf>;

/// <summary>
/// Pflegt Datensätze im Hintergrund (ein Worker, der reagiert): hört das Anfordern einer Range bzw. des Einfrierens, liest die Bildpaare (Queries)
/// und gibt dem Datensatz das Ergebnis — die gefundenen Paare bzw. die Mitglieder mit Label-Stand und Split.
/// </summary>
public sealed record Datensatzpfleger : IAkteur,
    IHoert<RangeAngefordert>, IHoert<EinfrierenAngefordert>,
    IDarf<SucheImagePairs>, IDarf<GetImagePair>,
    IDarf<NimmRangeAuf>, IDarf<SchliesseEinfrierenAb>;

// ── KI ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Beurteilt Bildpaare und Einzelbilder mit dem aktiven Modell (Strang 1): reagiert auf <see cref="ImagePairKomplett"/> mit seinem Urteil; davor
/// merkt er sich den Pfad jedes Bildes (<see cref="BildVerfuegbar"/>, im Log davor) und lädt das neu freigegebene Modell.
/// </summary>
public sealed record Klassifizierer : IAkteur,
    IHoert<BildVerfuegbar>, IHoert<ImagePairKomplett>, IHoert<ModellAktiviert>,
    IDarf<KlassifiziereBildPaarDurchKi>, IDarf<KlassifiziereEinzelBildDurchKi>, IDarf<HoleAktivesModell>;

// ── Menschen ───────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Befundet Kamerabilder am Bildschirm (Strang 2) und markiert Paare als inspiziert.</summary>
public sealed record Inspekteur : IAkteur,
    IDarf<LabelBildPaar>, IDarf<LabelEinzelBild>, IDarf<LabelBildRegion>, IDarf<MarkiereAlsInspiziert>,
    IDarf<SucheImagePairs>, IDarf<GetImagePair>, IDarf<GetImagePairHistorie>, IDarf<GetImagePairStatistik>,
    IDarf<GetProduktionsTage>, IDarf<GetProduktionsStrip>, IDarf<GetProduktionsVerlauf>, IDarf<GetUnklassifizierteImagePairs>,
    IDarf<HoleBildPaarStand>,
    // Kenntnis: Live-Aktualisierung der Galerie und der Statistik.
    IHoert<ImagePairErstellt>, IHoert<BildVerfuegbar>, IHoert<ImagePairKomplett>, IHoert<BildPaarGelabelt>, IHoert<EinzelBildGelabelt>,
    IHoert<BildPaarDurchKiKlassifiziert>, IHoert<EinzelBildDurchKiKlassifiziert>, IHoert<PhysischesProduktGelabelt>, IHoert<ImagePairInspiziert>;

/// <summary>Prüft das physische Teil und setzt damit die Ground Truth (Strang 3) — nicht am Bildschirm.</summary>
public sealed record Produktpruefer : IAkteur,
    IDarf<LabelPhysischesProdukt>,
    IDarf<GetImagePair>, IDarf<SucheImagePairs>,
    IHoert<ImagePairErstellt>, IHoert<PhysischesProduktGelabelt>;

/// <summary>Stellt Datensätze zusammen, kuratiert, startet/bricht Trainings ab und registriert Modelle.</summary>
public sealed record KIOperator : IAkteur,
    IDarf<ErstelleDatensatz>, IDarf<FuegeRangeHinzu>, IDarf<NimmPaarAuf>, IDarf<EntfernePaar>, IDarf<SetzeSplit>, IDarf<FriereEin>,
    IDarf<StarteTraining>, IDarf<BricheTrainingAb>, IDarf<RegistriereModell>,
    IDarf<HoleDatensaetze>, IDarf<HoleDatensatz>, IDarf<HoleDatensatzPaare>, IDarf<HoleDatensaetzeFuerPaar>,
    IDarf<HoleTrainingslaeufe>, IDarf<HoleTrainingslauf>, IDarf<HoleDatensatzSamples>,
    IDarf<HoleModelle>, IDarf<SucheImagePairs>, IDarf<GetImagePair>,
    // Kenntnis: Kuratieren, Datensätze (Liste, Komposition), Trainingsläufe (Liste, Dashboard mit Live-Kurve aus TrainingFortschritt), Modelle.
    IHoert<PaarAufgenommen>, IHoert<PaareAufgenommen>, IHoert<PaarEntfernt>,
    IHoert<DatensatzErstellt>, IHoert<SplitGesetzt>, IHoert<DatensatzEingefroren>,
    IHoert<TrainingAngefordert>, IHoert<TrainingBegonnen>, IHoert<TrainingFortschritt>, IHoert<TrainingAbgeschlossen>, IHoert<TrainingGescheitert>,
    IHoert<TrainingAbgebrochen>, IHoert<TrainingHaengengeblieben>,
    IHoert<ModellRegistriert>;

/// <summary>Schaltet ein Modell für die Produktions-Inferenz scharf oder zieht es zurück (Freigabe ≠ Training).</summary>
public sealed record Modellfreigeber : IAkteur,
    IDarf<SetzeModellAktiv>, IDarf<ArchiviereModell>,
    IDarf<HoleModelle>, IDarf<HoleAktivesModell>,
    IHoert<ModellRegistriert>, IHoert<ModellAktiviert>, IHoert<ModellArchiviert>;

/// <summary>Beispiel-Domäne Sammelvorgang: startet Sammelvorgänge und ihre Teilaufträge und schließt Teilaufträge ab.</summary>
public sealed record Disponent : IAkteur,
    IDarf<StarteSammelvorgang>, IDarf<StarteTeilauftrag>, IDarf<SchließeTeilauftragAb>;

/// <summary>
/// Meldet jedes fertige Teil an seiner Barriere (Fan-in, Schritt 6): hört <see cref="TeilauftragAbgeschlossen"/> und gibt <see cref="MeldeTeilFertig"/>
/// an den Sammelvorgang, den das Event nennt — die Zugehörigkeit ist das fachliche Feld (A2), Zähler und Barriere liegen im Aggregat.
/// </summary>
public sealed record Teilmelder : IAkteur,
    IHoert<TeilauftragAbgeschlossen>, IDarf<MeldeTeilFertig>;
