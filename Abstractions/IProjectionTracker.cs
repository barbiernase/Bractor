namespace Abstractions;

/// <summary>
/// Optionale Zusatzfähigkeit, die ein Projektions-Store implementieren KANN.
/// Liefert dem Framework die semantischen Operationen, die es braucht, um ein
/// Event nicht erneut anzuwenden: den Fortschritt lesen, vorrücken und (für
/// Replay) zurücksetzen.
///
/// Das Framework STELLT diesen Nahtpunkt BEREIT. Es schreibt nicht vor, WIE der
/// Store den Fortschritt ablegt, und es garantiert NICHT, dass Fortschritt und
/// fachlicher Effekt gemeinsam durabel werden — das ist allein Sache des Stores.
/// Implementiert der Store beides über EINE Transaktion (bei Marten: dieselbe
/// Session, ein SaveChangesAsync), ist das Ergebnis exactly-once-wirksam;
/// schreibt er getrennt, gilt at-least-once und die Handler müssen idempotent
/// sein. Das Projektions-/Write-Interface bleibt unverändert daneben bestehen.
///
/// Ein Store OHNE dieses Interface bleibt voll funktionsfähig: applied bleibt -1,
/// es wird stets ab Stream-Anfang gelesen, der Guard ist wirkungslos — die
/// Handler müssen dann idempotent sein.
/// </summary>
public interface IProjectionTracker
{
    /// <summary>
    /// Höchste Stream-Version, die für diese Projektion und diesen Stream bereits
    /// durabel angewandt wurde, oder -1 wenn noch nichts. Der Adapter überspringt
    /// jedes Event mit version &lt;= diesem Wert und liest den Stream ab version + 1.
    /// </summary>
    Task<int> LastProcessedVersionAsync(
        string projectionId, Guid streamId, CancellationToken ct);

    /// <summary>
    /// Vermerkt, dass die Projektion für diesen Stream auf <paramref name="version"/>
    /// vorgerückt ist. Ob der Store das in dieselbe Transaktion wie seine
    /// Effekt-Schreibvorgänge legt, ist ausschließlich seine Entscheidung.
    /// </summary>
    Task MarkProcessedAsync(
        string projectionId, Guid streamId, int version, CancellationToken ct);

    // ─────────────────────────────────────────────────────────────────────
    // Replay / Rebuild
    //
    // Reset ist die Umkehrung von Mark — eine semantische Operation, KEIN
    // Transaktions- oder Scope-Konzept. Deshalb gehört es auf dieses Interface
    // und nicht in eine separate Begin/Commit-Mechanik (vgl. Spec 7.2: der
    // Vertrag drückt nur aus, was das Framework semantisch braucht).
    //
    // Grundsatz (Spec 7.7): Abgeleitetes darf zurückgespult werden, weil es aus
    // der Wahrheit neu berechenbar ist; der Log selbst spult nie zurück. Ein
    // Rebuild ist derselbe Codepfad mit Fortschritt = -1 und geleertem Ziel
    // (das Ziel-Löschen gehört dem Store, siehe IRebuildableProjection).
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Setzt die Marke EINES Streams dieser Projektion zurück (auf -1). Der
    /// nächste Read liest diesen Stream wieder ab Anfang. Für gezielten
    /// Einzel-Stream-Rebuild.
    /// </summary>
    Task ResetAsync(string projectionId, Guid streamId, CancellationToken ct);

    /// <summary>
    /// Setzt ALLE Marken dieser Projektion zurück. Für den vollständigen
    /// Neuaufbau. Muss zusammen mit dem Leeren des Ziels erfolgen
    /// (IRebuildableProjection.ClearAsync) — sonst würde in einen befüllten
    /// Zustand replayt.
    /// </summary>
    Task ResetAllAsync(string projectionId, CancellationToken ct);
}
