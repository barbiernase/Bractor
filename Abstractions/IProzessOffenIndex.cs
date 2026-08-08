namespace Abstractions;

/// <summary>
/// Durabler Index der OFFENEN Prozesse (ProzessGestartet ohne ProzessBeendet) — die Grundlage des
/// §3-Prozess-Backstops. Er beantwortet „welche Prozess-Korrelationen laufen noch", damit ein periodischer
/// Scan jeden offenen Prozess direkt wecken kann. Ohne diese Weckung hängt ein Prozess, dessen letzte
/// (terminale/kompensierende) Selbst-Weckung verloren ging, für immer ohne ProzessBeendet — der
/// Korrelations-Router und der Poll routen die auslösenden Ergebnis-Events NICHT (sie sind Auslöser keiner
/// Regel), können den Manager also nicht neu wecken.
///
/// Best-effort-HINWEIS, keine Wahrheit: die Wahrheit bleibt das Manager-Log (Gestartet ohne Beendet). Bleibt
/// ein Eintrag fälschlich „offen" (Remove verloren), weckt der Backstop einen bereits terminalen Prozess —
/// dessen <c>WakeAsync</c> faltet <c>Beendet</c> und kehrt sofort folgenlos zurück. Fehlt ein Eintrag (Add
/// verloren), fällt der Prozess auf den normalen Signal-/Selbst-Weckungs-Pfad zurück (wie vor dem Backstop) —
/// nie schlechter als der Status quo. Reines Vertragsinterface; die Ablage ist Store-Sache.
/// </summary>
public interface IProzessOffenIndex
{
    /// <summary>Vermerkt eine Korrelation als offen (beim <c>ProzessGestartet</c>).</summary>
    Task MarkiereOffenAsync(Guid korrelation, string prozessName, CancellationToken ct);

    /// <summary>Entfernt eine Korrelation aus dem Index (beim <c>ProzessBeendet</c>).</summary>
    Task MarkiereBeendetAsync(Guid korrelation, CancellationToken ct);

    /// <summary>Alle aktuell offenen Korrelationen — die Arbeitsliste des Backstop-Scans (O(offen)).</summary>
    Task<IReadOnlyList<Guid>> ListeOffeneAsync(CancellationToken ct);
}

/// <summary>
/// Durables Offen-Dokument (Id = Korrelations-Guid). Reines POCO ohne Persistenz-Abhängigkeit
/// (analog <see cref="PollCursor"/>); die Marten-Schema-Registrierung erfolgt in der Host-/Store-Konfiguration.
/// </summary>
public class ProzessOffen
{
    public Guid Id { get; set; }
    public string ProzessName { get; set; } = default!;
    public DateTimeOffset SeitUtc { get; set; }
}
