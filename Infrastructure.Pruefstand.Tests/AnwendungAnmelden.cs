using System.Runtime.CompilerServices;

namespace Infrastructure.Pruefstand.Tests;

/// <summary>Die Tests fahren die Beispielanwendung: ihre Komposition meldet sich beim Laden der Test-Assembly an (docs/07).</summary>
internal static class AnwendungAnmelden
{
    [ModuleInitializer]
    internal static void Anmelden() => Domain.Laufzeit.GeneratedAnwendung.Anmelden();
}
