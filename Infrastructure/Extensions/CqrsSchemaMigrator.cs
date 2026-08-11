using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

/// <summary>
/// Dedizierter Marten-Schema-Migrator für Multi-Node-Cold-Start (docs/multi-node-deployment.md).
///
/// Legt ALLE konfigurierten Marten-Objekte — Event-Tabellen UND die sonst LAZY erzeugten
/// Snapshot-Tabellen (<c>es.mt_doc_snapshot_&lt;typ&gt;</c>) — EAGER und advisory-lock-gesichert an.
/// Gedacht als kurzlebiger Init-Schritt: genau EIN Node läuft mit <see cref="MartenSchemaRole.Migrator"/>,
/// ruft diese Methode und exitet; die übrigen Nodes starten danach als <see cref="MartenSchemaRole.Member"/>
/// (<c>AutoCreate.None</c>) und finden das fertige Schema vor → kein gleichzeitiges CREATE-TABLE-Race.
///
/// Bewusst OHNE Cluster-/Consul-/PubSub-Start: der Migrator berührt nur Postgres. Deshalb wird der
/// <see cref="IDocumentStore"/> direkt aufgelöst (kein <c>app.Run()</c>, keine Hosted Services).
/// </summary>
public static class CqrsSchemaMigrator
{
    /// <summary>
    /// Wendet alle konfigurierten Schema-Änderungen eager auf die Datenbank an (advisory-lock-gesichert).
    /// Idempotent: ist das Schema bereits aktuell, ist es ein No-op. Wirft bei DB-/Verbindungsfehlern.
    /// </summary>
    public static async Task ApplyAllAsync(IServiceProvider services)
    {
        var store = services.GetRequiredService<IDocumentStore>();
        // IMartenStorage-Ebene nimmt nur den optionalen AutoCreate-Override (keinen CancellationToken).
        // CreateOrUpdate erzwingt das eager Anlegen unabhängig von der konfigurierten AutoCreate-Stufe.
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync(AutoCreate.CreateOrUpdate);
    }
}
