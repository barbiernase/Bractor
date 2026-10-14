using Microsoft.CodeAnalysis;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Wo die Laufzeit-Tabellen entstehen: im Projekt, das die Domäne mit dem Framework zusammenführt — erkannt am Code-Fakt, dass die
    /// Framework-Laufzeit (<c>Infrastructure.Quellen.QuellEingang</c>) in der Compilation sichtbar ist, nie an einem Projektnamen.
    /// Dort emittieren die Generatoren auch dann, wenn die Domäne (noch) nichts beiträgt, weil die Laufzeit ihre Tabellen immer liest.
    /// </summary>
    internal static class Laufzeitprojekt
    {
        public static bool Ist(Compilation c) => c.GetTypeByMetadataName("Infrastructure.Quellen.QuellEingang") != null;
    }
}
