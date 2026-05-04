namespace Abstractions.Options;

/// <summary>
/// Konfiguration für den EventStore (MartenDB/PostgreSQL).
/// 
/// Kein StoreType-Switch mehr – MartenDB ist die einzige Implementierung.
/// Für Tests: Integrationstests gegen echte PostgreSQL-Instanz via Testcontainers.
/// </summary>
public class EventStoreOptions
{
    /// <summary>
    /// PostgreSQL Connection String für Marten.
    /// </summary>
    public string ConnectionString { get; set; } = 
        "Host=localhost;Database=cqrs_events;Username=postgres;Password=postgres";
    
    /// <summary>
    /// PostgreSQL Schema-Name für Marten-Tabellen (mt_streams, mt_events).
    /// </summary>
    public string SchemaName { get; set; } = "es";
}

// ═══════════════════════════════════════════════════════════
// ENTFERNT in Phase 1:
//   - EventStoreType Enum (InMemory, PostgreSql, EventStoreDb)
//   - StoreType Property
// Begründung: Kein Feature-Toggle. MartenDB + Redis ist die
// Architektur-Entscheidung, kein Konfigurationsparameter.
// ═══════════════════════════════════════════════════════════