using Abstractions;
using Core.Katalog;

namespace Domain.Akteure;

// Die APPS — die konkrete Software, die einen Client umsetzt (docs/konzept-akteure.md §4, docs/10 E18): je App ihr Vertrag an der Leitung,
// gegen den verbindlich entwickelt wird. Je Nachricht des Clients setzt sie entweder die App selbst um (der Standard) oder eine ihrer Quellen — ein
// Parameter des Records: die Quellart, [Uebersetzt<…>] nennt die Übersetzung der Akteur-Seite (Uebersetzungen.cs). Eine Nachricht mit Quelle steht nicht
// im Vertrag der App. Wo eine App läuft, sagt die Umgebung. Daraus generiert: GeneratedAppVertraege (Server), je App eine Python-Basis
// (domain_client/generated/vertraege.py), das Blazor-Wiring ([assembly: IstApp<…>]).

/// <summary>Der Blazor-Arbeitsplatz (<c>Host.Blazor</c> + <c>Domain.Client.Modules.Blazor</c>): alles, was seine vier Akteure tun, über die App.</summary>
public sealed record ArbeitsplatzApp : IApp<Arbeitsplatz>;

/// <summary>Der Python-Klassifizierer (<c>classifier.py</c>, <c>run_stub.py</c>): beurteilt komplette Bildpaare mit dem aktiven Modell.</summary>
public sealed record KlassifikationsApp : IApp<KlassifikationsWorker>;

/// <summary>Das Python-Training (<c>training_worker.py</c>, <c>run_train_stub.py</c>) auf einer eingefrorenen Datensatz-Version.</summary>
public sealed record TrainingsApp : IApp<TrainingsWorker>;

/// <summary>Der .NET-Worker der Reaktionen (<c>Domain.Client.Worker</c>): Datensatzpfleger und Teilmelder.</summary>
public sealed record ReaktionsApp : IApp<ReaktionsWorker>;

/// <summary>
/// Der Kameraeingang am Linienrechner: ein Dateiwächter auf dem Verzeichnis der Kamera; jede fertige Datei gibt das Kamerasystem als <c>LiesBild</c> in
/// die Bildaufbereitung. Keine eigene Software: alles, was der Linienrechner sendet, kommt aus der Quelle.
/// </summary>
public sealed record KameraApp([Uebersetzt<DateiGefunden_LiesBild_1>] Dateiwaechter Kamera) : IApp<Linienrechner>;
