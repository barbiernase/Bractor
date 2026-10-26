namespace Abstractions;

/// <summary>
/// Marker einer FUNKTION (Plan Pub/Sub-Neustrukturierung §2.2, §3.3). Zwei Formen, unterschieden nur an der Methode (Code-Fakt,
/// <see cref="Funktionsvertrag"/>):
/// <list type="bullet">
/// <item><b>Baustein</b> (<see cref="Funktionsvertrag.Auf"/>): je Eingangstyp eine Methode <c>Auf(TEingang e[, Herkunft h][, Lese-Fähigkeiten…][,
///   CancellationToken])</c> mit freier Ausgabe in der Signatur — ein konkreter Typ, <c>OneOf&lt;…&gt;</c>, <c>T?</c> (nichts), ein Strom
///   (<c>IEnumerable</c>/<c>IAsyncEnumerable</c>), je auch als <c>Task&lt;…&gt;</c>. Als <b>Klasse</b> läuft sie im Host (Dienste im Konstruktor), als
///   <b>Schnittstelle</b> draußen (Worker, Client, Hand) oder mit einer Implementierung im Host — der Laufort steht in der Umgebung. Eingang ist ein
///   Event (auch eine Quell-Nachricht) oder ein Hinweis; ausgegeben werden Commands, Events und Hinweise. Position, Fortschritt, Gleichzeitigkeit,
///   Wiederholung und das Festhalten der Ausgabe (R12) trägt der Funktions-Actor des Frameworks.</item>
/// <item><b>Katalog-Funktion</b> (<see cref="Funktionsvertrag.Methode"/>, bis Phase 4): eine Schnittstelle mit genau EINER Methode
///   <c>Task&lt;OneOf&lt;Ergebnis…&gt;&gt; RufeAsync(TAuftrag auftrag, IAusfuehrung x)</c> (CQRS068), gerufen aus Prozess und Fluss
///   (<c>Rufe&lt;TFunktion&gt;</c>); Ergebnisse sind persistente Events im Ausführungs-Stream.</item>
/// </list>
/// Beide Formen in einem Typ sind ein Fehler (CQRS100).
/// </summary>
public interface IFunktion { }

/// <summary>Nicht-generische Sicht auf einen Auftrag — für Ausführer und generierten Dispatch.</summary>
public interface IAuftrag { }

/// <summary>
/// Der EINE Eingang der Funktion <typeparamref name="TFunktion"/>. Bindet den Auftrag typsicher an genau diese
/// Funktion: <c>Rufe&lt;TFunktion&gt;(e =&gt; new MeinAuftrag(…))</c> kompiliert nur, wenn der gebaute Auftrag zu ihr gehört.
/// </summary>
public interface IAuftrag<TFunktion> : IAuftrag where TFunktion : IFunktion { }

/// <summary>
/// Was jede Ausführung mitbekommt, für jede Funktion gleich. <see cref="AusfuehrungsId"/> ist deterministisch
/// (der Vorgang der Prozess-Transition): eine Wiederholung nach Absturz trägt DIESELBE Id — eine Funktion mit
/// Außenwirkung kann darüber deduplizieren, und ein Ergebnis-Pfad, der aus ihr abgeleitet ist, wird überschrieben
/// statt verdoppelt.
/// </summary>
public interface IAusfuehrung
{
    Guid AusfuehrungsId { get; }
    Guid Korrelation { get; }
    CancellationToken Abbruch { get; }
}
