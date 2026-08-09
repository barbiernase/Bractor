namespace Abstractions;

/// <summary>
/// GA-1-Marker (P4): „diese Projektion ist APPEND-ARTIG" — jedes Event erzeugt einen NEUEN Eintrag
/// (Historie/Ledger), Doppelverarbeitung ist damit SICHTBAR und korrumpierend (anders als ein
/// idempotenter Upsert, der Doppelverarbeitung versteckt). Eine solche Projektion ist NUR dann
/// exactly-once-wirksam, wenn Effekt und Fortschrittsmarke gemeinsam durabel werden — also ihr
/// Store einen Co-Commit-<see cref="IProjectionTracker"/> mitbringt.
///
/// Der GA-1-Check (Boot-/DI-Zeit) erzwingt genau das: eine <see cref="IAppendProjektion"/> OHNE
/// Co-Commit-Tracker bricht den Start mit klarer Meldung, statt still at-least-once (doppelte
/// Appends) zu laufen. Der Normalfall ist der nicht-idempotente Effekt — deshalb ist der Marker
/// bewusst ein OPT-IN des Entwicklers dort, wo Doppelverarbeitung korrumpiert (keine Fehlalarme
/// bei legitim idempotenten Upsert-Projektionen).
/// </summary>
public interface IAppendProjektion
{
}
