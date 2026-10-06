using Abstractions;
using Domain.Akteure;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Modell;
using Domain.Projections;
using Domain.Trainingslauf;

namespace Domain.Clients;

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
/// Blazor-Arbeitsplatz (<c>Host.Blazor</c> + <c>Domain.Client.Modules.Blazor</c>): ein Client, vier Akteure. Gemessen am Client-Code
/// (IntentHandler, RefreshHandler, Effects, Stores) am 2026-10-06 — bewusst NICHT im Vertrag, weil kein Akteur des Arbeitsplatzes es darf:
/// <c>NimmRangeAuf</c> (Kuratieren, Mehrfach-Auswahl; laut Domäne nur aus dem Resolver) und <c>HoleDatensatzSamples</c> (Komposition;
/// nur das TrainingsSystem). Mit Tor würden beide abgelehnt — offene Domänen-Entscheidung, keine Vertrags-Frage.
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
    IFragt<HoleTrainingslaeufe>, IFragt<HoleTrainingslauf>, IFragt<HoleModelle>,
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
}
