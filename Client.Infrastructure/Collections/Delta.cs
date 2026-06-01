namespace Client.Infrastructure.Collections;

/// <summary>
/// Gemeinsamer Basistyp für alle Collection-Deltas.
///
/// Typisiert per TKey damit die View weiß welcher Eintrag betroffen ist —
/// ohne Reflection, ohne Casting auf object.
///
/// Verwendung in der View:
///   foreach (var delta in deltas)
///   {
///       switch (delta)
///       {
///           case Replaced&lt;Guid&gt; r: UpdateRow(r.Key); break;
///           case Reset&lt;Guid&gt;:      RerenderAll();    break;
///       }
///   }
/// </summary>
public abstract record CollectionDelta;

/// <summary>
/// Neuer Eintrag wurde hinzugefügt.
/// Index ist die Position in der Einfügereihenfolge — stabil solange kein Reset.
/// </summary>
public record Added<TKey>(TKey Key, int Index) : CollectionDelta;

/// <summary>
/// Vorhandener Eintrag wurde ersetzt (Put auf existierendem Key oder Update).
/// Der Record an dieser Position ist neu — Views müssen re-rendern.
/// </summary>
public record Replaced<TKey>(TKey Key) : CollectionDelta;

/// <summary>
/// Eintrag wurde entfernt.
/// </summary>
public record Removed<TKey>(TKey Key) : CollectionDelta;

/// <summary>
/// Alle Einträge wurden ersetzt (ReplaceAll).
/// View macht einen vollständigen Re-Render — kein Diff nötig oder sinnvoll.
/// </summary>
public record Reset<TKey>() : CollectionDelta;
