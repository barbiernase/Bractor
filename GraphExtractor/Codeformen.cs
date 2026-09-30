using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codeformen;

/// <summary>
/// Die SYNTAX-REGELN, was im Code als Modell-Feld gilt — EINE Quelle für den Extractor (Code → Modell) und den DateiSchreiber
/// (Modell → Code, Abgleich bestehender Deklarationen). In SimHost per Link eingebunden, damit beide nie auseinanderlaufen.
/// </summary>
public static class Feldregeln
{
    /// <summary>Ein Record-/Klassen-Feld als Property: <c>public</c> [<c>required</c>], ohne Attribute, Auto-Accessoren, optional Initialisierer.</summary>
    public sealed record EigenschaftsFeld(string Name, string Typ, string? Standard, string Zugriff, bool Pflicht);

    public static EigenschaftsFeld? Eigenschaft(PropertyDeclarationSyntax p)
    {
        var mods = p.Modifiers.Select(x => x.Kind()).ToList();
        if (!mods.Contains(SyntaxKind.PublicKeyword) || mods.Any(k => k is not (SyntaxKind.PublicKeyword or SyntaxKind.RequiredKeyword))) return null;
        if (p.AttributeLists.Count > 0 || p.ExplicitInterfaceSpecifier != null || p.ExpressionBody != null) return null;
        if (p.AccessorList is not { } al || al.Accessors.Any(a => a.Body != null || a.ExpressionBody != null || a.Modifiers.Count > 0 || a.AttributeLists.Count > 0))
            return null;
        var kinds = al.Accessors.Select(a => a.Kind()).ToList();
        string? zugriff = kinds switch
        {
            [SyntaxKind.GetAccessorDeclaration] => "{ get; }",
            [SyntaxKind.GetAccessorDeclaration, SyntaxKind.SetAccessorDeclaration] => "{ get; set; }",
            [SyntaxKind.GetAccessorDeclaration, SyntaxKind.InitAccessorDeclaration] => "{ get; init; }",
            _ => null,
        };
        return zugriff == null ? null
            : new(p.Identifier.Text, p.Type.ToString(), p.Initializer?.Value.ToString(), zugriff, mods.Contains(SyntaxKind.RequiredKeyword));
    }

    /// <summary>Ein State-Feld: <c>public T X { get; [set;] } [= init;]</c> oder <c>public T X => expr;</c> (sonst Handcode).</summary>
    public sealed record StateFeld(string Name, string Typ, string? Standard, string? Ausdruck, bool NurGet);

    public static StateFeld? State(PropertyDeclarationSyntax p)
    {
        if (!p.Modifiers.Any(SyntaxKind.PublicKeyword) || p.Modifiers.Any(SyntaxKind.StaticKeyword)
            || p.Modifiers.Count != 1 || p.AttributeLists.Count > 0 || p.ExplicitInterfaceSpecifier != null)
            return null;
        if (p.ExpressionBody is { } eb) return new(p.Identifier.Text, p.Type.ToString(), null, eb.Expression.ToString(), false);
        if (p.AccessorList is not { } al || al.Accessors.Any(a => a.Body != null || a.ExpressionBody != null || a.Modifiers.Count > 0))
            return null;
        var kinds = al.Accessors.Select(a => a.Kind()).ToList();
        var nurGet = kinds.SequenceEqual(new[] { SyntaxKind.GetAccessorDeclaration });
        var getSet = kinds.SequenceEqual(new[] { SyntaxKind.GetAccessorDeclaration, SyntaxKind.SetAccessorDeclaration });
        if (!nurGet && !getSet) return null;
        return new(p.Identifier.Text, p.Type.ToString(), p.Initializer?.Value.ToString(), null, nurGet);
    }

    /// <summary>Ein Enum-Wert als Text: <c>Name</c> bzw. <c>Name = Wert</c>.</summary>
    public static string EnumWert(EnumMemberDeclarationSyntax m) =>
        m.EqualsValue is { } ev ? $"{m.Identifier.Text} = {ev.Value}" : m.Identifier.Text;
}
