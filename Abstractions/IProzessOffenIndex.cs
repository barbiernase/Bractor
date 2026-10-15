namespace Abstractions;

/// <summary>
/// Durabler Index der OFFENEN Prozesse (ProzessGestartet ohne ProzessBeendet) — die Grundlage des
/// §3-Prozess-Backstops. Er beantwortet „welche Prozess-Korrelationen laufen noch", damit ein periodischer
/// Scan jeden offenen Prozess direkt wecken kann. Ohne diese Weckung hängt ein Prozess, dessen letzte
/// (terminale/kompensierende) Selbst-Weckung verloren ging, für immer ohne ProzessBeendet — der
/// Korrelations-Router und der Poll routen die auslösenden Ergebnis-Events NICHT (sie sind Auslöser keiner
/// Regel), können den Manager also nicht neu wecken.
///
/// HINWEIS, keine Wahrheit: die Wahrheit bleibt das Manager-Log (Gestartet ohne Beendet). Eintrag und Ende schreibt der Manager als
/// Beilage im Append des <c>ProzessGestartet</c> bzw. <c>ProzessBeendet</c> (<see cref="VorgangOffen"/>, <see cref="VorgangBeendet"/>) — in
/// derselben Transaktion, ein gestarteter Vorgang ohne Eintrag kann nicht entstehen. Ein Eintrag ohne laufenden Vorgang (ein Ende, dessen
/// Beilage fehlte, etwa ohne Index verdrahtet) ist folgenlos — der Backstop weckt ihn, der Manager räumt ihn mit
/// <see cref="MarkiereBeendetAsync"/> ab. Reines Vertragsinterface; die Ablage ist Store-Sache.
/// </summary>
public interface IProzessOffenIndex
{
    /// <summary>Entfernt eine Korrelation aus dem Index (beim <c>ProzessBeendet</c>).</summary>
    Task MarkiereBeendetAsync(Guid korrelation, CancellationToken ct);

    /// <summary>Alle aktuell offenen Korrelationen — die Arbeitsliste des Backstop-Scans (O(offen)).</summary>
    Task<IReadOnlyList<Guid>> ListeOffeneAsync(CancellationToken ct);

    /// <summary>
    /// Die Frist eines offenen Prozesses: bis wann ihn der Backstop wecken muss, auch wenn kein Event kommt (ein Zeitlimit läuft ab, ein
    /// Warte-Knoten liest seinen Strom nach). <c>null</c> = keine. Gleicher Status wie der Index: ein verlorener Eintrag kostet höchstens
    /// die Genauigkeit des Zeitlimits bis zur nächsten Heil-Weckung, nie Korrektheit. Ohne Ablage (Prüfstand) folgenlos.
    /// </summary>
    Task SetzeFristAsync(Guid korrelation, DateTimeOffset? frist, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Die offenen Korrelationen, deren Frist bis <paramref name="bis"/> (DB-Zeit) fällig ist.</summary>
    Task<IReadOnlyList<Guid>> ListeFaelligeAsync(DateTimeOffset bis, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
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
    /// <summary>Bis wann der Backstop den Prozess wecken muss (<see cref="IProzessOffenIndex.SetzeFristAsync"/>); null = keine.</summary>
    public DateTimeOffset? Frist { get; set; }
}
