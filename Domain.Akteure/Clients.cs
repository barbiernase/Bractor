using Abstractions;

namespace Domain.Akteure;

// Die CLIENTS — die Software an der Leitung (docs/konzept-akteure.md §4). Ein Client ist nicht ein Akteur: er kann mehrere Akteure verkörpern (der
// Arbeitsplatz: Inspekteur, Produktprüfer, KIOperator, Modellfreigeber), und ein Akteur kann über mehrere Clients laufen. Ein Client nennt nur, wen er
// verkörpert; was über seine Leitung geht, sind das IDarf und IHoert dieser Akteure (Akteure.cs). Am Handshake gilt für beide Richtungen: verkörperte
// Akteure ∩ Akteure des Tokens. Daraus generiert: GeneratedClientVertraege (Server), je Client eine Python-Basis (domain_client/generated/vertraege.py),
// das Blazor-Wiring.

/// <summary>Python-Worker <c>classifier.py</c> (bzw. <c>run_stub.py</c>): beurteilt komplette Bildpaare mit dem aktiven Modell.</summary>
public sealed record KlassifikationsWorker : IClient, IVerkoerpert<Klassifizierer>;

/// <summary>
/// Python-Worker <c>training_worker.py</c> (bzw. <c>run_train_stub.py</c>): trainiert auf einer eingefrorenen Datensatz-Version und zieht die Samples
/// über den Query-Kanal.
/// </summary>
public sealed record TrainingsWorker : IClient, IVerkoerpert<TrainingsSystem>;

/// <summary>
/// Blazor-Arbeitsplatz (<c>Host.Blazor</c> + <c>Domain.Client.Modules.Blazor</c>, <c>[assembly: IstClient&lt;Arbeitsplatz&gt;]</c>): ein Client, vier
/// Akteure. Der Client-Code wird im Build dagegen gehalten (CQRS095–097): was er erzeugt, muss einer dieser Akteure dürfen, worauf ein Handler hört,
/// muss einer von ihnen hören.
/// </summary>
public sealed record Arbeitsplatz : IClient, IVerkoerpert<Inspekteur>, IVerkoerpert<Produktpruefer>, IVerkoerpert<KIOperator>, IVerkoerpert<Modellfreigeber>;
