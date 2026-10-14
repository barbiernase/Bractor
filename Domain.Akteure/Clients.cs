using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Modell;
using Domain.Projections;
using Domain.Trainingslauf;
using Domain.Projections.Datensatz;
using Domain.Projections.ImagePair;
using Domain.Projections.Modell;
using Domain.Projections.Trainingslauf;

namespace Domain.Akteure;

// Die CLIENTS — die Software an der Leitung (docs/konzept-akteure.md §4). Ein Client ist nicht ein Akteur: er kann mehrere
// Akteure verkörpern (der Arbeitsplatz: Inspekteur, Produktprüfer, KIOperator, Modellfreigeber), und ein Akteur kann über mehrere
// Clients laufen. Je Client EIN Vertrag: was über SEINE Leitung geht — getragene Akteur-Vertrags-Teile (Zusagen im Namen des
// jeweiligen Akteurs), ISendet/IFragt (spontan, muss ein Akteur dürfen) und eigene Kenntnis (void Auf). Die client-INTERNEN
// Commands/Events (Blazor-Intents, ClientEvents, Python-State) stehen hier nie.
//
// Welche Akteure ein Client verkörpert, wird abgeleitet; am Handshake gilt Vertrag ∩ Akteure des Tokens. Daraus generiert:
// GeneratedClientVertraege (Server), je Client eine Python-Basis (domain_client/generated/vertraege.py), die Rahmen im Editor.

/// <summary>
/// Python-Worker <c>classifier.py</c> (bzw. <c>run_stub.py</c>): beurteilt Bildpaare mit dem aktiven Modell.
/// </summary>
public interface IKlassifikationsWorker : IClientVertrag,
    IKlassifizierer,
    ISendet<KlassifiziereEinzelBildDurchKi>,
    IFragt<HoleAktivesModell>
{ }

/// <summary>
/// Python-Worker <c>training_worker.py</c> (bzw. <c>run_train_stub.py</c>): trainiert auf einer eingefrorenen Datensatz-Version und zieht
/// die Samples über den Query-Kanal.
/// </summary>
public interface ITrainingsWorker : IClientVertrag,
    ITrainingsSystem,
    IFragt<HoleDatensatzSamples>
{ }

/// <summary>
/// Blazor-Arbeitsplatz (<c>Host.Blazor</c> + <c>Domain.Client.Modules.Blazor</c>, <c>[assembly: IstClient&lt;IArbeitsplatz&gt;]</c>): ein Client,
/// vier Akteure. Der Client-Code wird im Build dagegen gehalten (CQRS095–097): was er erzeugt, muss hier gesendet bzw. gefragt sein, worauf
/// ein Handler hört, muss hier Kenntnis sein. <c>NimmRangeAuf</c> steht bewusst nicht hier (laut Domäne nur aus dem Resolver) — die
/// Mehrfach-Auswahl beim Kuratieren schickt je Paar <c>NimmPaarAuf</c>.
/// </summary>
public interface IArbeitsplatz : IClientVertrag,
    // ── Inspekteur: Bilder befunden ──
    ISendet<LabelBildPaar>, ISendet<MarkiereAlsInspiziert>,
    IFragt<SucheImagePairs>, IFragt<GetImagePairHistorie>, IFragt<GetImagePairStatistik>, IFragt<GetProduktionsTage>,
    IFragt<GetProduktionsStrip>,
    // ── Produktprüfer: Ground Truth am physischen Teil ──
    ISendet<LabelPhysischesProdukt>,
    // ── KIOperator: Datensätze, Training ──
    ISendet<ErstelleDatensatz>, ISendet<FuegeRangeHinzu>, ISendet<NimmPaarAuf>, ISendet<EntfernePaar>, ISendet<SetzeSplit>,
    ISendet<FriereEin>, ISendet<StarteTraining>, ISendet<BricheTrainingAb>, ISendet<RegistriereModell>,
    IFragt<HoleDatensaetze>, IFragt<HoleDatensatz>, IFragt<HoleDatensatzPaare>, IFragt<HoleDatensaetzeFuerPaar>,
    IFragt<HoleTrainingslaeufe>, IFragt<HoleTrainingslauf>, IFragt<HoleModelle>, IFragt<HoleDatensatzSamples>,
    // ── Modellfreigeber ──
    ISendet<SetzeModellAktiv>, ISendet<ArchiviereModell>
{
    // Kenntnis: Live-Aktualisierung der Stores (Galerie, Statistik, Kuratieren).
    void Auf(ImagePairErstellt e);
    void Auf(BildVerfuegbar e);
    void Auf(ImagePairKomplett e);
    void Auf(BildPaarGelabelt e);
    void Auf(EinzelBildGelabelt e);
    void Auf(BildPaarDurchKiKlassifiziert e);
    void Auf(EinzelBildDurchKiKlassifiziert e);
    void Auf(PhysischesProduktGelabelt e);
    void Auf(ImagePairInspiziert e);
    void Auf(PaarAufgenommen e);
    void Auf(PaareAufgenommen e);
    void Auf(PaarEntfernt e);
    // Kenntnis: Datensätze (Liste, Komposition) und Modelle.
    void Auf(DatensatzErstellt e);
    void Auf(SplitGesetzt e);
    void Auf(DatensatzEingefroren e);
    void Auf(ModellRegistriert e);
    void Auf(ModellAktiviert e);
    void Auf(ModellArchiviert e);
    // Kenntnis: Trainingsläufe (Liste, Dashboard mit Live-Kurve aus TrainingFortschritt).
    void Auf(TrainingAngefordert e);
    void Auf(TrainingBegonnen e);
    void Auf(TrainingFortschritt e);
    void Auf(TrainingAbgeschlossen e);
    void Auf(TrainingGescheitert e);
    void Auf(TrainingAbgebrochen e);
    void Auf(TrainingHaengengeblieben e);
}
