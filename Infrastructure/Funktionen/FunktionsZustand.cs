using Abstractions;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// ZUSTAND EINER FUNKTION (docs/04 §4.4): gekapselt in der Aktivierung (je Kind eines Parents), ein CACHE — frisch bei jeder Aktivierung, nie
// gesichert, keine Wahrheit. Was fachlich zählt, steht im Log und wird in Projektionen gelernt; eine Funktion liest es über ihre Lese-Fähigkeiten.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Der Zustand einer Aktivierung (die Umsetzung bekommt ihn als <see cref="IZustand{T}"/> in den Konstruktor).</summary>
public sealed class Zustand<T> : IZustand<T> where T : class, new()
{
    public T Wert { get; set; } = new();
}

/// <summary>
/// Was der Generator je Funktion mit Zustand beibringt (<c>GeneratedFunktionsBausteine</c>): der Typ, ein frischer Anfangszustand, Schreiben bzw.
/// Lesen über den generierten Serializer-Kontext (ohne Reflection) und zurück auf den Anfangszustand.
/// </summary>
public sealed record FunktionsZustandsform(Type Typ, Func<object> Neu, Func<object, byte[]> Schreibe, Action<object, byte[]> Lies, Action<object> Anfang);
