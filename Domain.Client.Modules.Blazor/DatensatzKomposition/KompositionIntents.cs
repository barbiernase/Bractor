using Client.Infrastructure.Abstractions;

namespace Domain.Client.Modules.DatensatzKomposition;

// Client-lokale Intents (IClientEvent): die View dispatcht sie, der
// DatensatzKompositionIntentHandler übersetzt sie in Domänen-Commands.

/// <summary>Neuen (leeren) Datensatz anlegen und aktiv wählen.</summary>
public record ErstelleDatensatzIntent(string Name) : IClientEvent;

/// <summary>Die ganze aktuelle Such-Range in den aktiven Datensatz aufnehmen.</summary>
public record RangeHinzufuegenIntent() : IClientEvent;

/// <summary>Ein einzelnes Paar aus dem aktiven Datensatz entfernen.</summary>
public record PaarEntfernenIntent(Guid ImagePairId) : IClientEvent;

/// <summary>Split-Override auf dem aktiven Datensatz setzen.</summary>
public record SplitSetzenIntent(int Train, int Val, int Test, int Seed) : IClientEvent;

/// <summary>Den aktiven Datensatz einfrieren (Snapshot + Version).</summary>
public record FriereEinIntent() : IClientEvent;
