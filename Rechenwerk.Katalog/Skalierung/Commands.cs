using Abstractions;

namespace Rechenwerk.Katalog.Skalierung;

// Die Nachrichten der Skalierung — domänenneutral: der Bezug ist ein Guid, keine Id einer Anwendung (die Übersetzung macht daraus ihre Id).

/// <summary>Ein Wert ist zu skalieren — ohne Angabe verdoppelt.</summary>
public sealed record Skalieren(Guid Bezug, double Wert, double Faktor = 2.0) : ICommand;

/// <summary>Der skalierte Wert, mit dem Bezug, aus dem er kam.</summary>
public sealed record Skaliert(Guid Bezug, double Wert) : ICommand;
