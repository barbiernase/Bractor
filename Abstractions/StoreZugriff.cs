namespace Abstractions;

/// <summary>
/// Was ein Co-Commit-Store von der Datenzugriffs-Bibliothek benutzen DARF — die EINE Quelle dieser Positivliste. Der
/// Analyzer <c>StoreZugriffAnalyzer</c> (CQRS066/067, per Link eingebunden, netstandard2.0) und der LLM-Kontext lesen sie
/// von hier.
///
/// <b>Warum:</b> ein Store schreibt nur über die Puffer-Methoden seiner Co-Commit-Basis (Effekt + Marke in EINER
/// Transaktion, Schreib-Regel je Dokument). Mit dem <c>IDocumentStore</c> der Basis ginge auch der übliche Marten-Weg
/// <c>LightweightSession() … SaveChangesAsync()</c> — still am Puffer vorbei. Deshalb ist von den Datenzugriffs-Assemblies
/// nur der Lese-Einstieg erlaubt. Eine Positivliste bleibt dicht, auch wenn die Bibliothek neue Schreib-APIs bekommt.
/// </summary>
public static class StoreZugriff
{
    /// <summary>Assemblies, über die man Daten schreiben kann. Ihre Symbole sind in einem Store verboten, außer der
    /// Positivliste unten. Dazu der Service-Locator (über ihn holte man sich den Document-Store wieder).</summary>
    public static readonly string[] DatenzugriffAssemblies =
    {
        "Marten", "Npgsql", "Weasel.Core", "Weasel.Postgresql", "JasperFx", "JasperFx.Events",
        "StackExchange.Redis", "System.Data.Common", "Microsoft.Extensions.DependencyInjection.Abstractions",
    };

    /// <summary>Service-Locator im Kern-Framework (nicht in einer der Assemblies oben).</summary>
    public const string ServiceProvider = "System.IServiceProvider";

    /// <summary>Der Lese-Einstieg: <c>IDocumentStore.QuerySession()</c>.</summary>
    public const string DocumentStore = "Marten.IDocumentStore";
    public const string LeseEinstieg = "QuerySession";

    /// <summary>Was man auf der Lese-Session benutzen darf. Bewusst NICHT <c>DocumentStore</c>, <c>Connection</c>,
    /// <c>ExecuteAsync</c>, <c>AdvancedSql</c>, <c>QueryAsync</c> (SQL), <c>Events</c> — darüber käme man zum Schreiben.</summary>
    public const string QuerySession = "Marten.IQuerySession";
    public static readonly string[] SessionErlaubt = { "LoadAsync", "LoadManyAsync", "Query" };

    /// <summary>LINQ auf einer Marten-Abfrage: Erweiterungsmethoden der Datenzugriffs-Assemblies auf <c>IQueryable</c>, die
    /// nur materialisieren/sortieren. Nicht dabei: <c>…Sql</c>-Varianten, <c>ToCommand</c>, <c>ExplainAsync</c>.</summary>
    public static readonly string[] AbfrageErlaubt =
    {
        "ToListAsync", "ToAsyncEnumerable", "ToPagedListAsync",
        "CountAsync", "LongCountAsync", "AnyAsync",
        "FirstAsync", "FirstOrDefaultAsync", "SingleAsync", "SingleOrDefaultAsync",
        "SumAsync", "MinAsync", "MaxAsync", "AverageAsync",
        "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending", "Include", "Stats",
    };

    /// <summary>Kurzfassung für den LLM-Kontext.</summary>
    public const string Hinweis =
        "nur lesen: QuerySession() → LoadAsync/LoadManyAsync/Query (+ ToListAsync/CountAsync …) — schreiben nur über die Puffer-Methoden der Basis (CQRS066)";
}
