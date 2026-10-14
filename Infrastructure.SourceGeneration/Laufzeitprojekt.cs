using Microsoft.CodeAnalysis;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Wo die Laufzeit-Tabellen entstehen: in der Komposition der Anwendung — dem Projekt der Rolle Komposition (<c>&lt;Wurzel&gt;.Laufzeit</c>,
    /// <see cref="Abstractions.Projektstruktur"/>, docs/07) bzw. dem Projekt, das die Domäne mit der Framework-Laufzeit zusammenführt (sie ist
    /// sichtbar: <c>Infrastructure.Quellen.QuellEingang</c>). Dort emittieren die Generatoren auch dann, wenn die Domäne (noch) nichts beiträgt,
    /// weil die Laufzeit ihre Tabellen immer liest.
    /// </summary>
    internal static class Laufzeitprojekt
    {
        public static bool Ist(Compilation c) =>
            Abstractions.Projektstruktur.Zerlege(c.AssemblyName ?? "").Rolle == Abstractions.Projektstruktur.Rolle.Komposition
            || c.GetTypeByMetadataName("Infrastructure.Quellen.QuellEingang") != null;
    }
}
