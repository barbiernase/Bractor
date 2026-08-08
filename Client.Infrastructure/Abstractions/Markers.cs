namespace Client.Infrastructure.Abstractions;

/// <summary>
/// Marker-Interface für client-lokale Events.
/// Diese Events werden NUR auf dem Client-Bus publiziert,
/// NIE an den Server gesendet.
/// 
/// Beispiele: FilterChanged, SortierungChanged, UI-State-Events.
/// </summary>
public interface IClientEvent { }

/// <summary>
/// Marker-Interface für ViewModels.
/// Der Source Generator erkennt Klassen mit diesem Interface
/// und generiert öffentliche Methoden + IRelayCommand-Properties
/// aus privaten _camelCase-Methoden.
/// </summary>
public interface IViewModel { }

/// <summary>
/// Marker für Stores, die zur Start-Hydration gehören.
///
/// Der WiringGenerator sammelt alle Implementierungen und stellt sie als
/// ClientWiringConfig.HydrationStores bereit. Der ClientStartupService wartet
/// nach dem Connect, bis ALLE markierten Stores IsHydrated == true melden,
/// bevor er in den Ready-Zustand wechselt und die Shell die Module mountet.
///
/// Ein Store setzt IsHydrated, sobald seine Start-Daten EINGETROFFEN sind —
/// unabhängig davon, ob Daten vorhanden sind. Eine leere Ergebnismenge
/// (z.B. leere DB) ist "hydriert, aber leer", nicht "noch am Laden".
///
/// StoreBase implementiert IsHydrated/HydrationChanged und die geschützte
/// Methode MarkHydrated(). Ein Store muss nur dieses Interface deklarieren
/// und an der richtigen Stelle MarkHydrated() aufrufen.
/// </summary>
public interface IHydrationStore
{
    /// <summary>True sobald die Start-Daten dieses Stores eingetroffen sind.</summary>
    bool IsHydrated { get; }

    /// <summary>Feuert, wenn IsHydrated von false auf true wechselt.</summary>
    event Action? HydrationChanged;

    /// <summary>Setzt den Store zurück auf "nicht hydriert" (z.B. bei Reconnect).</summary>
    void ResetHydration();
}