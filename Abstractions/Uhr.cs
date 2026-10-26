namespace Abstractions;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DIE UHR (Plan Pub/Sub-Neustrukturierung §2.2, §3.1, §4.2, Phase 3): ein Aggregat entscheidet eine FRIST wie ein Event — in der Signatur seines
// Decide (`OneOf<…, Frist<PruefeZahlung>>`). Im Log steht sie als Framework-Event FristGesetzt im Stream des Aggregats; das Fälligkeits-Verzeichnis
// entsteht in DERSELBEN Transaktion. Fällig ist sie zur Log-Zeit ihres Events plus Nach — das Aggregat braucht keine Uhr (R11). Die Uhr des
// Frameworks sendet den Command zur Fälligkeit mit einer Command-Id aus dem Frist-Event (Stream, Version): doppelt gefeuert, einmal gewirkt.
// Storniert wird nicht: ist die Frist überholt, entscheidet das Aggregat beim Eintreffen „nichts zu tun“.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Nicht-generische Sicht auf eine Frist (<see cref="Frist{TCmd}"/>).</summary>
public interface IFrist : IPipelineOutput
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
/// Dauer ab der Log-Zeit. <see cref="IProzessIntern"/>: der Applier sieht sie nie, die Version zählt sie.
/// </summary>
public sealed record FristGesetzt(ICommand Befehl, TimeSpan Nach) : IEvent, IProzessIntern;

/// <summary>Wie die Ausgabe eines <c>Decide</c> ins Log geht: ein Event bleibt es selbst, eine Frist wird <see cref="FristGesetzt"/>.</summary>
public static class Entscheidungsausgabe
{
    public static IEvent Als(IPipelineOutput ausgabe) => ausgabe is IFrist f ? new FristGesetzt(f.Befehl, f.Nach) : (IEvent)ausgabe;
}
