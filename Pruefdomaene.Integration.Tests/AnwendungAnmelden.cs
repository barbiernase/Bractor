using System.Runtime.CompilerServices;

namespace Pruefdomaene.Integration.Tests;

/// <summary>Die Tests fahren die Prüfdomäne: ihre Komposition meldet sich beim Laden der Test-Assembly an (docs/07, ein Prozess = eine Anwendung).</summary>
internal static class AnwendungAnmelden
{
    [ModuleInitializer]
    internal static void Anmelden() => Pruefdomaene.Laufzeit.GeneratedAnwendung.Anmelden();
}
