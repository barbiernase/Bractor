using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Modell;
using Domain.Pipeline.ImagePair;
using Domain.Pipeline.Trainingslauf;
using Domain.Projections;
using Domain.Trainingslauf;
using Domain.Projections.Datensatz;
using Domain.Projections.ImagePair;
using Domain.Projections.Modell;
using Domain.Projections.Trainingslauf;

namespace Domain.Akteure;

// Die CLIENTS — die Software an der Leitung (docs/konzept-akteure.md §4). Ein Client ist nicht ein Akteur: er kann mehrere
// Akteure verkörpern (der Arbeitsplatz: Inspekteur, Produktprüfer, KIOperator, Modellfreigeber), und ein Akteur kann über mehrere
// Clients laufen. Je Client EIN Vertrag: wen er verkörpert (IVerkoerpert) und was er hört (IHoert, Kenntnis, verlierbar). Was über seine
// Leitung hineingeht, sind die IDarf seiner Akteure (Akteure.cs): Commands sendet er, Queries fragt er, Funktionen trägt er. Die
// client-INTERNEN Commands/Events (Blazor-Intents, ClientEvents, Python-State) stehen hier nie.
//
// Am Handshake gilt: verkörperte Akteure ∩ Akteure des Tokens. Daraus generiert:
// GeneratedClientVertraege (Server), je Client eine Python-Basis (domain_client/generated/vertraege.py), das Blazor-Wiring.

/// <summary>
/// Python-Worker <c>classifier.py</c> (bzw. <c>run_stub.py</c>): beurteilt Bildpaare mit dem aktiven Modell (der Klassifizierer rechnet
/// <see cref="IBildpaarBeurteilung"/>).
/// </summary>
public interface IKlassifikationsWorker : IClientVertrag,
    IVerkoerpert<Klassifizierer>,
    // Kenntnis: den Pfad jedes Bildes merken (beurteilt wird das Paar), das neu freigegebene Modell laden.
    IHoert<BildVerfuegbar>, IHoert<ModellAktiviert>;

/// <summary>
/// Python-Worker <c>training_worker.py</c> (bzw. <c>run_train_stub.py</c>): trainiert auf einer eingefrorenen Datensatz-Version und zieht
/// die Samples über den Query-Kanal (das TrainingsSystem rechnet <see cref="ITraining"/>).
/// </summary>
public interface ITrainingsWorker : IClientVertrag,
    IVerkoerpert<TrainingsSystem>,
    // Kenntnis: ein laufendes Training kooperativ abbrechen (der Lauf steht schon auf „abgebrochen").
    IHoert<TrainingAbgebrochen>;

/// <summary>
/// Blazor-Arbeitsplatz (<c>Host.Blazor</c> + <c>Domain.Client.Modules.Blazor</c>, <c>[assembly: IstClient&lt;IArbeitsplatz&gt;]</c>): ein Client,
/// vier Akteure. Der Client-Code wird im Build dagegen gehalten (CQRS095–097): was er erzeugt, muss einer dieser Akteure dürfen, worauf ein
/// Handler hört, muss hier Kenntnis sein.
/// </summary>
public interface IArbeitsplatz : IClientVertrag,
    IVerkoerpert<Inspekteur>, IVerkoerpert<Produktpruefer>, IVerkoerpert<KIOperator>, IVerkoerpert<Modellfreigeber>,
    // ── Kenntnis: Live-Aktualisierung der Stores (Galerie, Statistik, Kuratieren) ──
    IHoert<ImagePairErstellt>, IHoert<BildVerfuegbar>, IHoert<ImagePairKomplett>, IHoert<BildPaarGelabelt>, IHoert<EinzelBildGelabelt>,
    IHoert<BildPaarDurchKiKlassifiziert>, IHoert<EinzelBildDurchKiKlassifiziert>, IHoert<PhysischesProduktGelabelt>, IHoert<ImagePairInspiziert>,
    IHoert<PaarAufgenommen>, IHoert<PaareAufgenommen>, IHoert<PaarEntfernt>,
    // ── Kenntnis: Datensätze (Liste, Komposition) und Modelle ──
    IHoert<DatensatzErstellt>, IHoert<SplitGesetzt>, IHoert<DatensatzEingefroren>,
    IHoert<ModellRegistriert>, IHoert<ModellAktiviert>, IHoert<ModellArchiviert>,
    // ── Kenntnis: Trainingsläufe (Liste, Dashboard mit Live-Kurve aus TrainingFortschritt) ──
    IHoert<TrainingAngefordert>, IHoert<TrainingBegonnen>, IHoert<TrainingFortschritt>, IHoert<TrainingAbgeschlossen>, IHoert<TrainingGescheitert>,
    IHoert<TrainingAbgebrochen>, IHoert<TrainingHaengengeblieben>;
