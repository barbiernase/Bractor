using Abstractions;

namespace Pruefdomaene.Akteure;

// Die APPS der Prüfdomäne — die konkrete Software, die einen Client umsetzt (docs/konzept-akteure.md §4): je App ihr Vertrag an der Leitung.

/// <summary>Der .NET-Worker der Reaktionen der Prüfdomäne (<c>Pruefdomaene.Client.Worker</c>): die Sachbearbeitung, alles über die App.</summary>
public sealed record ReaktionsApp : IApp<ReaktionsWorker>;
