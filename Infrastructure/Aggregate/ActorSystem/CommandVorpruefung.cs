namespace Infrastructure.Aggregate.ActorSystem;

/// <summary>
/// Reine Vorab-Entscheidung eines Commands im Aggregat-Actor — store-/cluster-frei und damit
/// Ebene-1-testbar (P12). Kapselt die für T2b tragende Reihenfolge: <b>Inbox-Dedup VOR der
/// OCC-Prüfung</b>.
///
/// Warum die Reihenfolge zählt: ein wiederholt zugestellter Command (at-least-once — Emittiert
/// immer, Client seit T2b bei Retry nach verlorenem Ack) muss idempotent DASSELBE Ergebnis liefern,
/// auch wenn die Version inzwischen weitergerückt ist. Läge die OCC-Prüfung davor, läse ein
/// Client-Retry seines bereits erfolgreichen Commands fälschlich einen „Concurrency conflict" statt
/// des tatsächlichen Erfolgs. Die Dedup greift NUR bei wiederholter CommandId; ein frischer Command
/// fällt durch zur OCC-Assertion.
/// </summary>
public static class CommandVorpruefung
{
    public enum Ergebnis
    {
        /// <summary>Frischer Command, Version passt → normal verarbeiten.</summary>
        Fortfahren,
        /// <summary>CommandId bereits verarbeitet → idempotent Erfolg (kein erneuter Effekt).</summary>
        IdempotenterErfolg,
        /// <summary>CommandId bereits fachlich abgelehnt → konsistent dieselbe Ablehnung.</summary>
        IdempotenteAblehnung,
        /// <summary>Frischer Command, behauptete Version passt nicht → OCC-Konflikt.</summary>
        VersionsKonflikt,
    }

    /// <summary>
    /// Entscheidet den Vorab-Ausgang. Inbox-Dedup (nur auf dem idempotenten Pfad) hat Vorrang vor der
    /// OCC-Prüfung — das ist die T2b-Semantik.
    /// </summary>
    public static Ergebnis Prüfe(
        bool istIdempotent,
        bool bereitsVerarbeitet,
        bool bereitsAbgelehnt,
        int stateVersion,
        int expectedVersion)
    {
        if (istIdempotent && bereitsVerarbeitet) return Ergebnis.IdempotenterErfolg;
        if (istIdempotent && bereitsAbgelehnt) return Ergebnis.IdempotenteAblehnung;
        if (stateVersion != expectedVersion) return Ergebnis.VersionsKonflikt;
        return Ergebnis.Fortfahren;
    }
}
