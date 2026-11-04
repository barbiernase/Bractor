using Abstractions;
using Marten;
using Weasel.Postgresql;
using Weasel.Postgresql.Tables;

namespace Infrastructure.Persistence;

/// <summary>
/// Das VERZEICHNIS DER SCHLÜSSEL der Bündel (docs/04 §4.3.1): je Event eines Typs, das ein Bündel über ein Feld <c>Id&lt;T&gt;</c> oder global
/// bündelt, eine Zeile (Typ, Feld, Schlüssel, Sequenz) — geschrieben vom Append in DERSELBEN Transaktion wie das Event (die Sequenz aus
/// <c>mt_events</c> nach dem Einfügen), für die Events von vorher einmal aus dem Log nachgetragen (<see cref="Nachgetragen"/>). So findet der Rückblick
/// eines Bündels das neueste Event zu einem Schlüssel über den Primärschlüssel, ohne das Log zu durchsuchen. Eigene Tabelle statt eines Index auf
/// <c>mt_events</c>: den löschte Weasel beim Abgleich (Martens Event-Tabelle kennt keine fremden Indizes). Ein Bündel über den Stream braucht sie
/// nicht — dort trägt der Index auf (Stream, Version).
/// </summary>
public static class RueckblickTabelle
{
    public const string Tabelle = "cqrs_rueckblick";

    /// <summary>Welche (Typ, Feld) schon aus dem Log nachgetragen sind.</summary>
    public const string Nachgetragen = "cqrs_rueckblick_stand";

    public static void Registriere(StoreOptions options)
    {
        var t = new Table(new PostgresqlObjectName(options.Events.DatabaseSchemaName, Tabelle));
        t.AddColumn<string>("typ").NotNull().AsPrimaryKey();
        t.AddColumn<string>("feld").NotNull().AsPrimaryKey();
        t.AddColumn<Guid>("schluessel").NotNull().AsPrimaryKey();
        t.AddColumn<long>("seq").NotNull().AsPrimaryKey();
        options.Storage.ExtendedSchemaObjects.Add(t);

        var s = new Table(new PostgresqlObjectName(options.Events.DatabaseSchemaName, Nachgetragen));
        s.AddColumn<string>("typ").NotNull().AsPrimaryKey();
        s.AddColumn<string>("feld").NotNull().AsPrimaryKey();
        options.Storage.ExtendedSchemaObjects.Add(s);
    }

    public static string Name(IDocumentStore store) => $"{store.Options.Events.DatabaseSchemaName}.{Tabelle}";

    /// <summary>
    /// Die Zeilen eines Appends bzw. Group-Commits als EINE Anweisung derselben Transaktion, nach den Events: die Sequenz liest sie aus
    /// <c>mt_events</c> über (Stream, Version). Eine schon bekannte Zeile schreibt nichts.
    /// </summary>
    public static void Lege(IDocumentSession session, IDocumentStore store, List<string> typen, List<string> felder, List<Guid> schluessel,
        List<Guid> streams, List<int> versionen)
    {
        var schema = store.Options.Events.DatabaseSchemaName;
        session.QueueSqlCommand(
            $"insert into {Name(store)} (typ, feld, schluessel, seq) "
            + $"select u.t, u.f, u.k, e.seq_id from unnest(?::text[], ?::text[], ?::uuid[], ?::uuid[], ?::int[]) as u(t, f, k, s, v) "
            + $"join {schema}.mt_events e on e.stream_id = u.s and e.version = u.v "
            + "on conflict do nothing",
            typen.ToArray(), felder.ToArray(), schluessel.ToArray(), streams.ToArray(), versionen.ToArray());
    }
}
