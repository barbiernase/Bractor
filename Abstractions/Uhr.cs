namespace Abstractions;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DIE UHR (docs/10 §10.1, docs/10 §10.2, docs/11): ein Aggregat entscheidet eine FRIST wie ein Event — in der Signatur seines
// Decide (`OneOf<…, Frist<PruefeZahlung>>`). Im Log steht sie als Framework-Event FristGesetzt im Stream des Aggregats; das Fälligkeits-Verzeichnis
// entsteht in DERSELBEN Transaktion. Fällig ist sie zur Log-Zeit ihres Events plus Nach — das Aggregat braucht keine Uhr (R11). Die Uhr des
// Frameworks sendet den Command zur Fälligkeit mit einer Command-Id aus dem Frist-Event (Stream, Version): doppelt gefeuert, einmal gewirkt.
// Storniert wird nicht: ist die Frist überholt, entscheidet das Aggregat beim Eintreffen „nichts zu tun“.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Nicht-generische Sicht auf eine Frist (<see cref="Frist{TCmd}"/>).</summary>
public interface IFrist : IAusgabe
{
    IAggregatCommand Befehl { get; }
    TimeSpan Nach { get; }
}

/// <summary>
/// Eine Frist als Ausgabe eines <c>Decide</c>: nach <see cref="Nach"/> (ab der Log-Zeit dieser Entscheidung) bekommt das Aggregat
/// <see cref="Befehl"/>. Der Command zielt auf DASSELBE Aggregat (R5; der Aggregat-Actor weist eine fremde Frist ab).
/// </summary>
public sealed record Frist<TCmd>(TCmd Befehl, TimeSpan Nach) : IFrist where TCmd : IAggregatCommand
{
    IAggregatCommand IFrist.Befehl => Befehl;
}

/// <summary>
/// Die Frist im Log (Framework-Event im Stream des Aggregats): der Command (über den generierten, reflexionsfreien Command-Konverter) und die
/// Dauer ab der Log-Zeit. <see cref="IRahmenEvent"/>: der Applier sieht sie nie, die Version zählt sie.
/// </summary>
public sealed record FristGesetzt(IAggregatCommand Befehl, TimeSpan Nach) : IEvent, IRahmenEvent;

/// <summary>
/// Wie die Ausgabe eines <c>Decide</c> weitergeht: ein Event bzw. Hinweis bleibt es selbst, eine Frist wird <see cref="FristGesetzt"/> — sonst gibt ein
/// Decide nichts aus (Schritt 6, CQRS114: was nach einem Fakt geschieht, stößt ein Client an).
/// </summary>
public static class Entscheidungsausgabe
{
    public static object Als(object ausgabe) => ausgabe is IFrist f ? new FristGesetzt(f.Befehl, f.Nach) : ausgabe;

    /// <summary>Die Ausgaben einer Entscheidung als Events; etwas anderes ist ein Fehler (den der Build schon meldet, CQRS114).</summary>
    public static List<IEvent> NurEvents(object command, IEnumerable<object> ausgaben)
    {
        var aus = new List<IEvent>();
        foreach (var a in ausgaben)
            aus.Add(a as IEvent ?? throw new InvalidOperationException(
                $"Decide({command.GetType().Name}) gab {a.GetType().Name} aus — ein Decide gibt nur Events, Hinweise und Fristen aus (CQRS114)."));
        return aus;
    }
}
