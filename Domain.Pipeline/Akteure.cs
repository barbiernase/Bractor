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

// Die Akteure der Domäne — Domänen-Experten, von denen alles kommt, was hineingeht (docs/konzept-akteure.md §2).
// Deklariert wird nur, was ein Akteur SELBST hineingibt (IDarf<T>). Was eine Pipeline, ein Prozess oder eine Frist daraus
// erzeugt (NimmRangeAuf, ErstelleImagePair, MarkiereAlsHaengengeblieben …), trägt den Akteur der Kette — es steht hier
// bewusst NICHT: am Tor käme es so nie durch („nie von der GUI" ist ein Typ-Fakt). Ein Command darf bei mehreren Akteuren
// stehen. Hier liegen sie, weil dieses Projekt Commands, Queries UND Pipeline-Trigger sieht.
//
// IDarf ist das SPONTANE (der Mensch klickt, der Worker fragt von sich aus). Was ein Akteur draußen als ZUSAGE auf ein Event
// hineingibt, steht in seinem Vertrag (IAkteurVertrag<A>, docs/konzept-akteure.md §3): Auf(Event) mit dem Ausgabe-Vertrag als Rückgabetyp. Daraus
// entstehen die Client-Basis (Python), die Rechte (Befugt = IDarf ∪ Ausgaben, Hört = Eingänge) und die Kante im Editor.

// ── Maschinen ──────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Nimmt die Bildpaare (dc0/dc2) an der Linie auf und legt sie auf dem Share ab.</summary>
public sealed record KameraSystem : IMaschine,
    IDarf<DateiErkannt>;

/// <summary>GPU-Worker: trainiert auf einer eingefrorenen Datensatz-Version und meldet den Fortschritt.</summary>
public sealed record TrainingsSystem : IMaschine,
    IDarf<HoleDatensatzSamples>;

/// <summary>Worauf das TrainingsSystem reagiert (Python-Worker <c>training_worker.py</c>).</summary>
public interface ITrainingsSystem : IAkteurVertrag<TrainingsSystem>
{
    /// <summary>Trainiert langlaufend und meldet über die Zeit Beginn, Fortschritt (×N) und Ende bzw. Scheitern.</summary>
    IAsyncEnumerable<OneOf<MeldeTrainingBegonnen, MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingGescheitert>>
        Auf(TrainingAngefordert e);

    /// <summary>Bricht ein laufendes Training kooperativ ab (der Lauf steht schon auf „abgebrochen").</summary>
    void Auf(TrainingAbgebrochen e);
}

// ── KI ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Beurteilt Bildpaare und Einzelbilder mit dem aktiven Modell (Strang 1).</summary>
public sealed record Klassifizierer : IKi,
    IDarf<KlassifiziereEinzelBildDurchKi>,
    IDarf<HoleAktivesModell>;

/// <summary>Worauf der Klassifizierer reagiert (Python-Worker <c>classifier.py</c>).</summary>
public interface IKlassifizierer : IAkteurVertrag<Klassifizierer>
{
    /// <summary>Beide Bilder sind da → das Paar mit dem aktiven Modell beurteilen.</summary>
    OneOf<KlassifiziereBildPaarDurchKi> Auf(ImagePairKomplett e);

    /// <summary>Nur zur Kenntnis: den Pfad des Bildes merken (das Paar wird erst bei Komplett beurteilt).</summary>
    void Auf(BildVerfuegbar e);

    /// <summary>Nur zur Kenntnis: das neu freigegebene Modell laden.</summary>
    void Auf(ModellAktiviert e);
}

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
