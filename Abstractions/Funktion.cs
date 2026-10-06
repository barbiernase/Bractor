namespace Abstractions;

/// <summary>
/// Marker einer KATALOG-FUNKTION: eine Schnittstelle mit genau EINER Methode
/// <c>Task&lt;OneOf&lt;Ergebnis…&gt;&gt; RufeAsync(TAuftrag auftrag, IAusfuehrung x)</c> (CQRS068). Die Funktion ist der
/// Inhalt, nicht die Hülle: sie kennt weder Actor noch Mailbox noch Wiederholung — die Ausführung (Ausführer, Slots,
/// Ausführungs-Id, Ergebnis im Log) liefert das Framework. Ein Prozess ruft sie mit <c>Rufe&lt;TFunktion&gt;</c> genau so
/// auf, wie er ein Aggregat mit <c>Sende&lt;TCmd&gt;</c> ruft — ob sie 50 ms oder Stunden braucht, ist nur ein Zeitlimit.
///
/// Ergebnisse sind persistente Events (kein <see cref="ITransientEvent"/>): der Ausführer schreibt den gewählten
/// OneOf-Fall in den Ausführungs-Stream, und der Prozess faltet ihn wie das Event eines Aggregats.
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
