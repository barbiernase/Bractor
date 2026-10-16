using Domain.ImagePair;
using FluentAssertions;
using Marten;
using Marten.Services;
using Npgsql;

namespace Infrastructure.Integration.Tests.Garantien;

/// <summary>
/// G-9, die Voraussetzung des Horizonts der Vollständigkeit (<see cref="Persistence.Horizont"/>): im Anhänge-Modus <c>Quick</c> schreibt Marten in
/// jeder Transaktion zuerst die Stream-Zeile (insert bzw. update von <c>mt_streams</c>) und erst danach die Events mit <c>nextval</c>. So hat
/// jede Transaktion ihre Nummer, bevor sie eine Sequenz zieht. Ändert ein Marten-Update diese Reihenfolge, ist der Horizont nicht mehr exakt —
/// dieser Test fängt das ab, bevor es eine Messung oder ein Nutzer tut.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class AnhaengeReihenfolgePostgresTests
{
    private sealed class Mitschnitt : IMartenLogger, IMartenSessionLogger
    {
        public readonly List<string> Anweisungen = new();
        public IMartenSessionLogger StartSession(IQuerySession session) => this;
        public void SchemaChange(string sql) { }
        public void LogSuccess(NpgsqlCommand command) => Anweisungen.Add(command.CommandText);
        public void LogFailure(NpgsqlCommand command, Exception ex) { }
        public void LogSuccess(NpgsqlBatch batch) { foreach (var c in batch.BatchCommands) Anweisungen.Add(c.CommandText); }
        public void LogFailure(NpgsqlBatch batch, Exception ex) { }
        public void LogFailure(Exception ex, string message) { }
        public void RecordSavedChanges(IDocumentSession session, IChangeSet commit) { }
        public void OnBeforeExecute(NpgsqlCommand command) { }
        public void OnBeforeExecute(NpgsqlBatch batch) { }
    }

    [Fact]
    public async Task Marten_schreibt_im_schnellen_Modus_die_Stream_Zeile_vor_den_Events()
    {
        var mitschnitt = new Mitschnitt();
        using var store = DocumentStore.For(opts =>
        {
            opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;
            opts.Events.MetadataConfig.CorrelationIdEnabled = true;
            opts.Events.MetadataConfig.CausationIdEnabled = true;
            opts.Events.MetadataConfig.HeadersEnabled = true;
            opts.Connection("Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
            opts.DatabaseSchemaName = "garantie_reihenfolge_it";
            opts.Events.DatabaseSchemaName = "garantie_reihenfolge_it";
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
            opts.Logger(mitschnitt);
        });
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        await using (var s = store.LightweightSession())
        {
            s.Events.StartStream(a, new ImagePairInspiziert());
            await s.SaveChangesAsync();
        }

        mitschnitt.Anweisungen.Clear();
        await using (var s = store.LightweightSession())   // so schreibt der Group-Commit: mehrere Streams, Anhängen und Anlegen gemischt
        {
            s.Events.Append(a, 2, new ImagePairInspiziert());
            s.Events.StartStream(b, new ImagePairInspiziert());
            await s.SaveChangesAsync();
        }

        var schreibend = mitschnitt.Anweisungen
            .Where(sql => sql.Contains(".mt_streams") || sql.Contains(".mt_events"))
            .ToList();
        schreibend.Should().NotBeEmpty();
        schreibend[0].Should().Contain(".mt_streams",
            "die erste schreibende Anweisung jeder Transaktion muss die Stream-Zeile sein — sonst zieht sie eine Sequenz ohne Transaktionsnummer");
        schreibend.FindIndex(sql => sql.Contains("nextval")).Should().BeGreaterThan(0);
    }
}
