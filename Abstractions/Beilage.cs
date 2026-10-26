namespace Abstractions;

/// <summary>Wie ein Command ausging, wie die Inbox ihn festhält (docs/04 §4.4): gewirkt (Effekt oder Noop) oder fachlich abgelehnt.</summary>
public enum KommandoAusgang
{
    Gewirkt = 0,
    Abgelehnt = 1,
}

/// <summary>
/// Eine Beilage zu einem Append (docs/03 §3.3): Framework-Buchhaltung, die in DERSELBEN Transaktion wie die Events geschrieben wird — sie wird
/// mit ihnen gültig oder gar nicht. Geschlossener Summentyp; der Store bildet jeden Fall auf seine Ablage ab.
/// </summary>
public abstract record Beilage
{
    private protected Beilage() { }
}

/// <summary>
/// Die Quittung eines Commands: ein Eintrag der Inbox (eindeutig je <see cref="CommandId"/>). Ein zweiter Eintrag derselben CommandId scheitert
/// am Schlüssel — der Append schreibt dann nichts (<see cref="KommandoSchonEntschiedenException"/>). So wirkt ein Command genau einmal
/// innerhalb des Aufbewahrungs-Horizonts.
/// </summary>
public sealed record Quittung(Guid CommandId, KommandoAusgang Ausgang) : Beilage;

/// <summary>Der Vorgang <paramref name="Korrelation"/> ist offen — sein Eintrag im Offen-Index (<see cref="IProzessOffenIndex"/>), mit dem Start geschrieben.</summary>
public sealed record VorgangOffen(Guid Korrelation, string ProzessName) : Beilage;

/// <summary>Der Vorgang <paramref name="Korrelation"/> ist beendet — sein Eintrag im Offen-Index verschwindet mit dem <c>ProzessBeendet</c>.</summary>
public sealed record VorgangBeendet(Guid Korrelation) : Beilage;

/// <summary>
/// Eine Frist für das Fälligkeits-Verzeichnis der Uhr (Plan Pub/Sub-Neustrukturierung Phase 3): das <see cref="FristGesetzt"/> an
/// <paramref name="Version"/> des Streams, sein Command (Diskriminator und Wire-JSON), die Dauer ab seiner Log-Zeit und der Akteur der Kette.
/// </summary>
public sealed record FristFaellig(int Version, string Befehl, string Daten, TimeSpan Nach, string? Akteur) : Beilage;

/// <summary>
/// Die Kennung einer Quell-Nachricht in einem Stream je Schlüssel (Plan Pub/Sub-Neustrukturierung §4.2): ihr Vorgang (abgeleitet aus Typ und Kennung)
/// steht eindeutig im Kennungs-Verzeichnis — ein zweites Melden derselben Kennung scheitert am Schlüssel, nichts wird geschrieben.
/// </summary>
public sealed record QuellKennung(Guid Vorgang) : Beilage;

/// <summary>
/// Der Append trug eine Quittung, deren CommandId die Inbox schon kennt: nichts ist geschrieben. Den Ausgang des ersten Mals liefert
/// <see cref="IEventStoreRepository.AusgangAsync"/>.
/// </summary>
public sealed class KommandoSchonEntschiedenException(Guid commandId, Exception? innen = null)
    : Exception($"Command {commandId} ist schon entschieden (Inbox)", innen)
{
    public Guid CommandId { get; } = commandId;
}
