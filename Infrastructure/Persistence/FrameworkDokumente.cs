using Abstractions;
using Marten;

namespace Infrastructure.Persistence;

/// <summary>Die Marten-Dokumente des Frameworks — registriert vom Host (<c>AddCqrs</c>) und von den Plan-Tests mit derselben Konfiguration.</summary>
public static class FrameworkDokumente
{
    public static void Registriere(StoreOptions options)
    {
        // Dead-Letter-Queue (§5): nicht zustellbare Commands, beobachtbar + replay-bar.
        options.Schema.For<DeadLetter>().Identity(x => x.Id).DatabaseSchemaName("dlq");
    }
}
