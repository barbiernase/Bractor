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