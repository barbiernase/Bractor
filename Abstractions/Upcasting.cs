namespace Abstractions;

/// <summary>
/// Der EINZIGE Vertrag, den ein Entwickler für Schema-Evolution eines Events schreibt: „aus dieser
/// (früheren) Gestalt wird jene (nächste) Gestalt" — hart typisiert, ohne String, ohne JSON.
///
/// ── Das Modell (docs: Event-Upcasting, typisiert + generiert) ──
/// Ein Event ist im Log unveränderlich und für immer. Ändert sich seine Gestalt, wird die alte
/// Byte-Form NICHT migriert, sondern beim LESEN in die heutige Gestalt übersetzt. Der Entwickler
/// hält die alte Gestalt als schlichten Record fest (<c>KontoEroeffnet_V1</c>) und schreibt genau
/// eine <see cref="IUpcast{TAlt, TNeu}"/>-Implementierung. Alles Weitere leitet der Generator ab:
///
/// - <b>Frühere vs. aktuelle Version wird NICHT deklariert, sondern abgeleitet.</b> Ist ein Typ die
///   Quelle (<c>TAlt</c>) irgendeiner <c>IUpcast</c>, wertet etwas von ihm weg → er ist eine frühere
///   Version (nur lesbar, aus der Write-Registrierung ausgeschlossen). Ist er keine Quelle und ein
///   <see cref="IEvent"/> → die aktuelle Version (schreibbar). Kein Marker, kein Attribut nötig.
/// - <b>Die Kette</b> ergibt sich aus den Typkanten: mehrere Ein-Schritt-Wandlungen
///   (<c>V1→V2</c>, <c>V2→aktuell</c>) verkettet der Generator topologisch. Jede Wandlung ist winzig,
///   isoliert und store-frei testbar (Ebene 1).
/// - <b>Der Speicher-Diskriminator versioniert sich selbst</b> aus der DAG-Position — der Entwickler
///   tippt nie einen Diskriminator-String. Alte Bytes behalten ihren Namen (keine Migration).
///
/// Reine Funktion: <see cref="Wandle"/> darf NICHT auf externe Ressourcen zugreifen (kein async, kein
/// Lookup) — Upcasting läuft bei jedem Read. Teure Ketten federn Snapshots ab; die Eskalation ist
/// Copy-Transform (Stream-Rewrite).
/// </summary>
/// <typeparam name="TAlt">Die frühere Event-Gestalt (schlichter Record; wird aus dieser Kante als
/// „frühere Version" abgeleitet).</typeparam>
/// <typeparam name="TNeu">Die nächste Gestalt — entweder die aktuelle (<see cref="IEvent"/>) oder eine
/// weitere Zwischenversion, die selbst eine <c>IUpcast</c> nach vorn trägt.</typeparam>
public interface IUpcast<TAlt, TNeu>
    where TAlt : class
    where TNeu : class
{
    /// <summary>Übersetzt eine frühere Gestalt in die nächste. Rein, deterministisch, ohne Seiteneffekt.</summary>
    TNeu Wandle(TAlt alt);
}

/// <summary>
/// 1:2-Wandlung (Split): eine frühere Gestalt zerfällt in ZWEI nächste Events. Die Ziel-Typen stehen
/// bewusst in der Signatur (nicht als <c>IReadOnlyList</c>), damit der Generator die Kanten rein aus
/// den Typen ableiten kann (kein verstecktes Ziel). Beide Ausgänge werden weiter durch ihre je eigene
/// Kette gefahren, falls sie selbst noch Zwischenversionen sind.
///
/// ⚠ Split (1:N) ist API-seitig ab Tag 1 möglich, die Fan-out-VERDRAHTUNG im Read-/Dedup-Pfad
/// (mehrere Envelopes je Stored-Position, <c>SubIndex</c>) ist bewusst eine markierte zweite Stufe.
/// </summary>
public interface IUpcast<TAlt, TNeu1, TNeu2>
    where TAlt : class
    where TNeu1 : class
    where TNeu2 : class
{
    /// <summary>Zerlegt eine frühere Gestalt in zwei nächste Events (im selben Stream).</summary>
    (TNeu1, TNeu2) Wandle(TAlt alt);
}

/// <summary>
/// 1:3-Wandlung (Split). Selten; darüber hinaus (1:N mit großem N) ist Copy-Transform die richtige
/// Eskalation. Semantik wie <see cref="IUpcast{TAlt, TNeu1, TNeu2}"/>.
/// </summary>
public interface IUpcast<TAlt, TNeu1, TNeu2, TNeu3>
    where TAlt : class
    where TNeu1 : class
    where TNeu2 : class
    where TNeu3 : class
{
    /// <summary>Zerlegt eine frühere Gestalt in drei nächste Events (im selben Stream).</summary>
    (TNeu1, TNeu2, TNeu3) Wandle(TAlt alt);
}
