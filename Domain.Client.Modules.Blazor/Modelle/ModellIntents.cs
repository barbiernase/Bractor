using Client.Infrastructure.Abstractions;

namespace Domain.Client.Modules.Modelle;

// Client-lokale Intents (IClientEvent) der Modelle-Bühne → Modell-Commands.

/// <summary>
/// Ein Modell (manuell) registrieren. Normalerweise übernimmt das die Saga
/// „TrainingAbgeschlossen → RegistriereModell"; dies ist der manuelle/Test-Weg mit
/// optionaler Provenienz aus einem abgeschlossenen Lauf.
/// </summary>
public record ModellRegistrierenIntent(
    string Name,
    Guid TrainingslaufId,
    Guid DatensatzId,
    int DatensatzVersion,
    string Pfad,
    double Loss,
    double Genauigkeit
) : IClientEvent;

/// <summary>Dieses Modell aktiv setzen (Inferenz nutzt es).</summary>
public record ModellAktivSetzenIntent(Guid ModellId) : IClientEvent;

/// <summary>Dieses Modell archivieren.</summary>
public record ModellArchivierenIntent(Guid ModellId) : IClientEvent;
