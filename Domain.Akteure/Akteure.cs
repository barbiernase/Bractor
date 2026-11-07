using Abstractions;
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
// eine Query, ein Hinweis) und was zu ihm kommt (IHoert<T>): ein Command ohne Ziel ist Arbeit (die Werkstatt gibt ihn einer Sitzung, die ihn verkörpert,
// bis eine Antwort kommt — Commands über sein IDarf), ein Event oder Hinweis ist Kenntnis (gepusht, verlierbar). Was eine Funktion oder eine Frist
// erzeugt (NimmRangeAuf, MarkiereAlsHaengengeblieben …), trägt den Akteur der Kette — es steht hier bewusst NICHT: am Tor käme es so nie durch.
// Ausnahme: ein Akteur, für den eine Instanz mit Quellkopf hineingibt (Akteur in ihrer Datei), darf, was sie verlässt — der Start prüft es
// (docs/04 §4.4). Welche Software für welche Akteure an der Leitung spricht, steht in Clients.cs.

// ── Maschinen ──────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Nimmt die Bildpaare (dc0/dc2) an der Linie auf und legt sie auf dem Share ab.</summary>
public sealed record KameraSystem : IAkteur,
    IDarf<NimmBildAuf>;   // NimmBildAuf: was die Instanz des Linienimports (Dateiwächter → Bildaufbereitung) für das Kamerasystem hineingibt

/// <summary>GPU-Worker: trainiert auf einer eingefrorenen Datensatz-Version (Arbeit: <see cref="TrainingZuRechnen"/>) und meldet den Fortschritt.</summary>
public sealed record TrainingsSystem : IAkteur,
    IHoert<TrainingZuRechnen>,
    IDarf<MeldeTrainingBegonnen>, IDarf<MeldeFortschritt>, IDarf<MeldeTrainingAbgeschlossen>, IDarf<MeldeTrainingGescheitert>,
    IDarf<HoleDatensatzSamples>,
    // Kenntnis: ein laufendes Training kooperativ abbrechen (der Lauf steht schon auf „abgebrochen“).
    IHoert<TrainingAbgebrochen>;

/// <summary>
/// Pflegt Datensätze im Hintergrund (ein Worker, der reagiert): hört das Anfordern einer Range bzw. des Einfrierens, liest die Bildpaare (Queries)
/// und gibt dem Datensatz das Ergebnis — die gefundenen Paare bzw. die Mitglieder mit Label-Stand und Split.
/// </summary>
public sealed record Datensatzpfleger : IAkteur,
    IHoert<RangeAngefordert>, IHoert<EinfrierenAngefordert>,
    IDarf<SucheImagePairs>, IDarf<GetImagePair>,
    IDarf<NimmRangeAuf>, IDarf<SchliesseEinfrierenAb>;

// ── KI ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Beurteilt Bildpaare (Arbeit: <see cref="PaarZuBeurteilen"/>) und Einzelbilder mit dem aktiven Modell (Strang 1).</summary>
public sealed record Klassifizierer : IAkteur,
    IHoert<PaarZuBeurteilen>,
    IDarf<KlassifiziereBildPaarDurchKi>, IDarf<KlassifiziereEinzelBildDurchKi>, IDarf<HoleAktivesModell>,
    // Kenntnis: den Pfad jedes Bildes merken (beurteilt wird das Paar), das neu freigegebene Modell laden.
    IHoert<BildVerfuegbar>, IHoert<ModellAktiviert>;

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
