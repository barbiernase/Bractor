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
    ICommand Befehl { get; }
    TimeSpan Nach { get; }
}

/// <summary>
/// Eine Frist als Ausgabe eines <c>Decide</c>: nach <see cref="Nach"/> (ab der Log-Zeit dieser Entscheidung) bekommt das Aggregat
/// <see cref="Befehl"/>. Der Command zielt auf DASSELBE Aggregat (R5; der Aggregat-Actor weist eine fremde Frist ab).
/// </summary>
public sealed record Frist<TCmd>(TCmd Befehl, TimeSpan Nach) : IFrist where TCmd : ICommand
{
    ICommand IFrist.Befehl => Befehl;
}

/// <summary>
/// Die Frist im Log (Framework-Event im Stream des Aggregats): der Command (über den generierten, reflexionsfreien Command-Konverter) und die
/// Dauer ab der Log-Zeit. <see cref="IRahmenEvent"/>: der Applier sieht sie nie, die Version zählt sie.
/// </summary>
public sealed record FristGesetzt(ICommand Befehl, TimeSpan Nach) : IEvent, IRahmenEvent;

/// <summary>
/// Wie die Ausgabe eines <c>Decide</c> weitergeht: ein Event bleibt es selbst, eine Frist wird <see cref="FristGesetzt"/>, alles andere ist eine
/// <b>Folge</b> (ein schlichter Record ohne Marker, docs/03 §3.1) — sie geht nicht ins Log, sondern als offener Eingang an die Funktionen, die sie hören.
/// </summary>
public static class Entscheidungsausgabe
{
    public static object Als(object ausgabe) => ausgabe is IFrist f ? new FristGesetzt(f.Befehl, f.Nach) : ausgabe;

    /// <summary>Eine Folge: weder Event (auch kein Hinweis) noch Frist.</summary>
    public static bool IstFolge(object ausgabe) => ausgabe is not IEvent and not IFrist;
}
