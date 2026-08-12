namespace Abstractions;

/// <summary>
/// Marker-Profil: „dieser Store macht ECHTEN Co-Commit" — Effekt und Fortschrittsmarke werden in
/// EINER Transaktion gemeinsam durabel (bei Marten: dieselbe Session, ein SaveChangesAsync) → die
/// Verarbeitung ist exactly-once-wirksam, auch für nicht-idempotente (append-artige) Effekte.
///
/// Der Sinn ist der <b>fail-fast-Guard</b> (P9): <see cref="IProjectionTracker"/> allein trägt
/// KEINEN Beweis der Atomarität — ihn implementieren sowohl co-committende Stores (z. B. der
/// puffernde Marten-Store, der im Mark-Punkt ein SaveChanges macht) ALS AUCH bewusst
/// dual-writing Fallbacks (eigene Session je Mark, at-least-once). Ohne Unterscheidung könnte eine
/// append-artige Projektion (<see cref="IAppendProjektion"/>) still mit einem Dual-Write-Tracker
/// laufen → doppelte Appends, und der Boot-Check (<c>tracker != null</c>) bliebe grün (false-green).
///
/// Nur Stores, die die Atomarität tatsächlich einlösen, tragen dieses Interface. Der GA-1-Check
/// verlangt es für <see cref="IAppendProjektion"/> — ein bloßer <see cref="IProjectionTracker"/>
/// bricht dann laut am Boot.
///
/// <b>Grenze (bewusst):</b> Das Interface ist ein VERSPRECHEN, kein statischer Beweis — Atomarität
/// ist eine Laufzeiteigenschaft der Store-Transaktion. Es verengt nur die Vertrauensgrenze auf
/// „diese eine markierte Implementierung", die ein Store-Crash-Test einlöst (siehe
/// <c>CoCommitPostgresTests</c>). Konzept: <c>docs/konzept-exactly-once-naht.md</c>.
/// </summary>
public interface ICoCommitTracker : IProjectionTracker
{
}
