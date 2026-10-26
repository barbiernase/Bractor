using Marten;
using Weasel.Postgresql;
using Weasel.Postgresql.Tables;

namespace Infrastructure.Quellen;

/// <summary>
/// Das Kennungs-Verzeichnis der Quellen (Plan Pub/Sub-Neustrukturierung §4.2, Option „ein Stream je Schlüssel“): je Quell-Nachricht ihr Vorgang
/// (abgeleitet aus Typ und Kennung) als Primärschlüssel, geschrieben als Beilage in DERSELBEN Transaktion wie die Nachricht. Ein zweites Melden
/// derselben Kennung scheitert am Schlüssel — genau einmal je Kennung, ohne Horizont (anders als die Inbox der Commands).
/// </summary>
public static class QuellKennungen
{
    public const string Tabelle = "cqrs_quell_kennungen";

    public static void Registriere(StoreOptions options)
    {
        var t = new Table(new PostgresqlObjectName(options.Events.DatabaseSchemaName, Tabelle));
        t.AddColumn<Guid>("vorgang").AsPrimaryKey();
        t.AddColumn<Guid>("stream_id").NotNull();
        t.AddColumn("zeit", "timestamptz").NotNull().DefaultValueByExpression("now()");
        options.Storage.ExtendedSchemaObjects.Add(t);
    }

    public static string Name(IDocumentStore store) => $"{store.Options.Events.DatabaseSchemaName}.{Tabelle}";
}
