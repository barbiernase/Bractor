using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Domain.SourceGeneration
{
    /// <summary>
    /// Die normalisierte Syntax eines Typs für den Code-Hash (<c>Abstractions/Kodehash.cs</c>, docs/04 §4.3): die Token-Folge aller
    /// Deklarationsteile — ohne Leerraum und Kommentare, also unempfindlich gegen Formatierung. <paramref name="auslassen"/> schneidet
    /// Teilbäume heraus (z. B. den Decider aus dem Aggregat: er ändert die Daten nicht). Auch in Projections.SourceGeneration verlinkt.
    /// </summary>
    internal static class Kodeform
    {
        public static string Von(INamedTypeSymbol typ, Func<SyntaxNode, bool>? auslassen = null)
            => string.Join(" ", typ.DeclaringSyntaxReferences
                .Select(r => r.GetSyntax())
                .OrderBy(k => k.SyntaxTree.FilePath, StringComparer.Ordinal).ThenBy(k => k.SpanStart)
                .SelectMany(k => k.DescendantTokens(n => auslassen == null || n == k || !auslassen(n)))
                .Select(t => t.Text));

        public static string Hash(INamedTypeSymbol typ, Func<SyntaxNode, bool>? auslassen = null)
            => Abstractions.Kodehash.Hash(Von(typ, auslassen));
    }
}
