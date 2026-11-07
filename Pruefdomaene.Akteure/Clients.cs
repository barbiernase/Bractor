using Abstractions;

namespace Pruefdomaene.Akteure;

// Die CLIENTS der Prüfdomäne — die Software an der Leitung (docs/konzept-akteure.md §4): ein Client nennt nur, wen er verkörpert.

/// <summary>Der Worker der Reaktionen der Prüfdomäne (Pruefdomaene.Client.Worker): er verkörpert die Sachbearbeitung.</summary>
public sealed record ReaktionsWorker : IClient, IVerkoerpert<Sachbearbeitung>;
