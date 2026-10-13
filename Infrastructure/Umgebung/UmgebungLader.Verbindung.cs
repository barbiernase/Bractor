using Abstractions;

namespace Infrastructure.Umgebung;

// Der Teil des Laders, der Npgsql braucht — getrennt, damit ein Client (Host.Blazor) den Lader per Link mitnehmen kann.
public static partial class UmgebungLader
{
    /// <summary>Die vollständige Verbindung zum Event-Store (Verbindung + Datenbank).</summary>
    public static string Verbindung(EventStoreKonfig e) => new Npgsql.NpgsqlConnectionStringBuilder(e.Verbindung) { Database = e.Datenbank }.ConnectionString;
}
