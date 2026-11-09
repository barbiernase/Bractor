// In Projekt: Infrastructure.SourceGeneration (per Link auch in Domain.SourceGeneration und GraphExtractor.Kern)
// Dateiname: Uebersetzungsform.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Die EINE Lesart einer ÜBERSETZUNG (docs/04 §4.4) für Generator, Analyzer und Extractor — nur Code-Fakten: eine Klasse der Anwendung mit
    /// <c>ITranslate&lt;TSource, TTarget&gt;</c>. Ihr Konstruktor nimmt genau die Quelle (<c>TSource source</c>, sonst CQRS119); <c>Translate()</c> baut
    /// das Ziel aus ihren Feldern — der Compiler prüft, dass es vollständig ist. <c>TSource</c> ist ein Command ohne Ziel oder ein Event (eine Antwort),
    /// <c>TTarget</c> ein Command, sonst CQRS121. Für den Editor liest sie aus dem Ausdruck in <c>Translate()</c>, welcher Parameter des Ziels aus welchem
    /// Feld der Quelle kommt (<c>Ziel: source.Feld</c>, <c>Ziel: new(source.Feld)</c>, <c>Ziel: source.Feld.Wert</c>) — so schreibt ihn der Editor aus der
    /// Verdrahtung.
    /// <para>Eine ANTWORT-ÜBERSETZUNG nimmt statt eines Commands ein Event: die Antwort eines Aggregats auf den Command einer Übersetzung (ob das
    /// <c>Decide</c> eines solchen Commands das Event ausgeben kann, prüft CQRS123 im Gesamtbild).</para>
    /// </summary>
    internal static class Uebersetzungsform
    {
        /// <summary>Die Regeln einer Übersetzung (Analyzer-Kennungen).</summary>
        public const string Zuordnung = "CQRS119", Verlassen = "CQRS120", Nachricht = "CQRS121", Antwort = "CQRS123";

        /// <summary>Woher ein Parameter von <c>TTarget</c> kommt: aus einem Feld der Quelle mit gleichem bzw. anderem Namen, aus dem Standardwert, aus
        /// einem eigenen Ausdruck — leer: offen.</summary>
        public const string AusFeld = "feld", AusName = "name", AusStandard = "standard", AusAusdruck = "ausdruck";

        /// <summary>Ein Parameter von <c>TTarget</c> und seine Quelle (null, wenn keine).</summary>
        internal sealed class Ziel
        {
            public string Name = "";
            public ITypeSymbol Typ = null!;
            /// <summary>Das Feld der Quelle (bei Standard, Ausdruck und offen null).</summary>
            public string? Quelle;
            /// <summary><see cref="AusFeld"/>, <see cref="AusName"/>, <see cref="AusStandard"/>, <see cref="AusAusdruck"/> — leer: offen.</summary>
            public string Regel = "";
        }

        /// <summary>Eine gelesene Übersetzung: Quelle, Ziel, die Felder der Quelle, je Parameter des Ziels sein Feld der Quelle.</summary>
        internal sealed class Form
        {
            /// <summary>Die Übersetzung selbst (die Klasse); ihr Name ist der Name der Übersetzung.</summary>
            public INamedTypeSymbol Typ = null!;
            public ITypeSymbol Ein = null!, Aus = null!;
            /// <summary>Die lesbaren Eigenschaften von <c>TSource</c> (öffentlich, Instanz), in Reihenfolge der Deklaration.</summary>
            public List<IPropertySymbol> Felder = new List<IPropertySymbol>();
            public List<Ziel> Ziele = new List<Ziel>();
            public List<(string Id, string Text, Location Ort)> Fehler = new List<(string, string, Location)>();
            /// <summary>Eine Antwort-Übersetzung: <c>TSource</c> ist ein Event (die Antwort eines Aggregats), kein Command ohne Ziel.</summary>
            public bool IstAntwort;
        }

        /// <summary>Der Marker <c>ITranslate&lt;TSource, TTarget&gt;</c>, den <paramref name="t"/> trägt (oder ist) — sonst null.</summary>
        public static INamedTypeSymbol? Marker(ITypeSymbol t) =>
            (t is INamedTypeSymbol n ? new[] { n }.Concat(n.AllInterfaces) : t.AllInterfaces).FirstOrDefault(i =>
                i is { IsGenericType: true, Arity: 2, Name: "ITranslate" } && i.OriginalDefinition.ContainingNamespace?.ToDisplayString() == "Abstractions");

        /// <summary>Trägt <paramref name="t"/> den Marker einer Übersetzung (auch als Typ eines Parameters — dann ist es ein Fehler)?</summary>
        public static bool Ist(ITypeSymbol t) => Marker(t) != null;

        /// <summary>Ist <paramref name="t"/> eine Übersetzung — eine konkrete Klasse bzw. ein Record mit <c>ITranslate&lt;TSource, TTarget&gt;</c>?</summary>
        public static bool IstUebersetzung(INamedTypeSymbol t) => t.TypeKind == TypeKind.Class && !t.IsAbstract && !t.IsGenericType && Ist(t);

        /// <summary>Der Konstruktor der Übersetzung bzw. des Ziels: der öffentliche mit den meisten Parametern (ohne Kopier-Konstruktor eines Records).</summary>
        private static IMethodSymbol? Konstruktor(INamedTypeSymbol t) =>
            t.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public && !IstKopie(c, t)).OrderByDescending(c => c.Parameters.Length).FirstOrDefault();

        /// <summary>Liest die Übersetzung <paramref name="t"/> (<see cref="IstUebersetzung"/> vorausgesetzt): Marker, Konstruktor, Zuordnung, Fehler.</summary>
        public static Form Lies(INamedTypeSymbol t)
        {
            var u = Marker(t)!;
            var p = t;
            var ort = t.Locations.FirstOrDefault() ?? Location.None;
            var f = new Form { Typ = t, Ein = u.TypeArguments[0], Aus = u.TypeArguments[1] };
            var ein = f.Ein as INamedTypeSymbol;
            var aus = f.Aus as INamedTypeSymbol;
            f.IstAntwort = ein != null && Funktionsform.Hat(ein, "Abstractions.IEvent") && !Funktionsform.Hat(ein, "Abstractions.ITransientEvent");
            if (ein == null || !f.IstAntwort && !Funktionsform.IstCommandOhneZiel(ein))
                f.Fehler.Add((Nachricht, $"die Übersetzung '{p.Name}' nimmt {f.Ein.ToDisplayString()} — sie nimmt einen Command ohne Ziel (die Ausgabe "
                    + "eines Graphen oder die Nachricht eines Clients) oder die Antwort eines Aggregats (ein Event) auf den Command einer Übersetzung", ort));
            if (aus == null || aus.TypeKind != TypeKind.Class || aus.IsAbstract || aus.IsGenericType && aus.TypeArguments.Any(a => a.TypeKind == TypeKind.TypeParameter)
                || !Funktionsform.Hat(aus, "Abstractions.ICommand"))
                f.Fehler.Add((Nachricht, $"die Übersetzung '{p.Name}' erzeugt {f.Aus.ToDisplayString()} — sie erzeugt einen Command (ohne Ziel an einen "
                    + "Knoten oder Graphen, mit Ziel an ein Aggregat)", ort));
            if (f.Fehler.Count > 0) return f;

            for (var b = (ITypeSymbol?)ein; b != null && b.SpecialType != SpecialType.System_Object; b = b.BaseType)
                foreach (var e in b.GetMembers().OfType<IPropertySymbol>())
                    if (e.DeclaredAccessibility == Accessibility.Public && !e.IsStatic && !e.IsIndexer && e.GetMethod != null && e.Name != "EqualityContract"
                        && !f.Felder.Any(x => x.Name == e.Name))
                        f.Felder.Add(e);

            // Der Konstruktor der Übersetzung nimmt genau die Quelle (so ruft ihn der Generator mit der ankommenden Nachricht).
            var eigen = t.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public && !IstKopie(c, t)).ToList();
            var genau = eigen.FirstOrDefault(c => c.Parameters.Length == 1 && Gleich(c.Parameters[0].Type, ein!));
            if (genau == null)
            {
                f.Fehler.Add((Zuordnung, $"'{p.Name}': der Konstruktor nimmt genau die Quelle — ({ein!.Name} source)"
                    + (eigen.Count == 0 ? "" : $", nicht ({string.Join(", ", eigen.OrderByDescending(c => c.Parameters.Length).First().Parameters.Select(x => x.Type.ToDisplayString() + " " + x.Name))})"),
                    (eigen.FirstOrDefault()?.Locations.FirstOrDefault()) ?? ort));
                return f;
            }
            var quellName = genau.Parameters[0].Name;

            var ctor = aus is null ? null : Konstruktor(aus);
            if (ctor == null)
            {
                f.Fehler.Add((Nachricht, $"{aus!.Name} hat keinen öffentlichen Konstruktor — eine Übersetzung baut ihre Nachricht über ihn", ort));
                return f;
            }
            var zuordnung = ZuordnungImCode(t, ctor, quellName);
            foreach (var par in ctor.Parameters)
            {
                var z = new Ziel { Name = par.Name, Typ = par.Type };
                f.Ziele.Add(z);
                if (zuordnung.TryGetValue(par.Name, out var quelle))
                {
                    if (quelle == null) z.Regel = AusAusdruck;
                    else { z.Quelle = quelle; z.Regel = quelle == par.Name ? AusName : AusFeld; }
                }
                else if (par.HasExplicitDefaultValue) z.Regel = AusStandard;
            }
            return f;
        }

        /// <summary>
        /// Die Zuordnung, wie sie in <c>Translate()</c> steht: je Argument des Ziels (benannt oder nach Stelle) das Feld der Quelle — <c>source.Feld</c>,
        /// <c>new(source.Feld)</c> bzw. <c>new Id&lt;T&gt;(source.Feld)</c>, <c>source.Feld.Wert</c> (<paramref name="quelle"/> = der Name des
        /// Konstruktor-Parameters); ein anderer Ausdruck ergibt null. Ohne Quelltext (Metadaten) leer.
        /// </summary>
        public static Dictionary<string, string?> ZuordnungImCode(INamedTypeSymbol t, IMethodSymbol zielKonstruktor, string quelle)
        {
            var aus = new Dictionary<string, string?>();
            foreach (var r in t.DeclaringSyntaxReferences)
            {
                var m = r.GetSyntax().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                    .FirstOrDefault(x => x.Identifier.Text == "Translate" && x.ParameterList.Parameters.Count == 0);
                if (m == null) continue;
                var ausdruck = m.ExpressionBody?.Expression
                    ?? m.Body?.Statements.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ReturnStatementSyntax>().LastOrDefault()?.Expression;
                if (ausdruck is not Microsoft.CodeAnalysis.CSharp.Syntax.BaseObjectCreationExpressionSyntax neu || neu.ArgumentList == null) continue;
                var i = 0;
                foreach (var a in neu.ArgumentList.Arguments)
                {
                    var name = a.NameColon?.Name.Identifier.Text ?? (i < zielKonstruktor.Parameters.Length ? zielKonstruktor.Parameters[i].Name : "");
                    i++;
                    if (name.Length > 0) aus[name] = Feld(a.Expression, quelle);
                }
            }
            return aus;
        }

        private static string? Feld(Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax e, string quelle) => e switch
        {
            Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax { Expression: Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax q } m
                when q.Identifier.Text == quelle => m.Name.Identifier.Text,
            Microsoft.CodeAnalysis.CSharp.Syntax.BaseObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 1 } o => Feld(o.ArgumentList!.Arguments[0].Expression, quelle),
            Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax { Name.Identifier.Text: "Wert" } w
                when w.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax => Feld(w.Expression, quelle),
            _ => null,
        };

        /// <summary>Der Kopier-Konstruktor eines Records (<c>X(X original)</c>) zählt nicht.</summary>
        private static bool IstKopie(IMethodSymbol c, INamedTypeSymbol t) =>
            c.Parameters.Length == 1 && Gleich(c.Parameters[0].Type, t);

        /// <summary>Gleiche Typen — über den vollen Namen (im Extractor kommt derselbe Vertragstyp mal aus Quelle, mal aus Metadaten).</summary>
        private static bool Gleich(ITypeSymbol a, ITypeSymbol b) => Funktionsform.Voll(a) == Funktionsform.Voll(b);

        private static string Namen(IEnumerable<string> namen)
        {
            var l = namen.ToList();
            return l.Count == 0 ? "keine" : string.Join(", ", l);
        }

        /// <summary>
        /// Der Ausdruck, der <paramref name="quelle"/> (vom Typ <paramref name="von"/>) in den Typ <paramref name="nach"/> bringt, wenn er IMPLIZIT
        /// zuweisbar ist (gleich, Referenz-Umwandlung auf Basis bzw. Schnittstelle, <c>T</c> → <c>T?</c>, erweiternde Zahlen) — oder, nur
        /// <paramref name="ausdruecklich"/> (<c>[Feld]</c>), <c>Guid</c> ⇄ <c>Id&lt;T&gt;</c>. Sonst null: fremde Enums, Wertobjekte und Ids anderer
        /// Aggregate werden nie zugeordnet.
        /// </summary>
        public static string? Ausdruck(ITypeSymbol von, ITypeSymbol nach, string quelle, bool ausdruecklich)
        {
            if (Gleich(von, nach)) return quelle;
            if (nach is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nn && Gleich(nn.TypeArguments[0], von))
                return quelle;
            if (von.IsReferenceType && nach.IsReferenceType)
            {
                for (var b = von.BaseType; b != null; b = b.BaseType)
                    if (Gleich(b, nach)) return quelle;
                if (von.AllInterfaces.Any(i => Gleich(i, nach))) return quelle;
            }
            if (Erweitert(von.SpecialType, nach is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } n2 ? n2.TypeArguments[0].SpecialType : nach.SpecialType))
                return quelle;
            if (!ausdruecklich) return null;
            if (IstGuid(von) && IstId(nach)) return $"new {nach.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}({quelle})";
            if (IstId(von) && IstGuid(nach)) return quelle + ".Wert";
            return null;
        }

        private static bool IstGuid(ITypeSymbol t) => t.ToDisplayString() == "System.Guid";

        private static bool IstId(ITypeSymbol t) =>
            t is INamedTypeSymbol { IsGenericType: true, Name: "Id", Arity: 1 } n && n.ContainingNamespace?.ToDisplayString() == "Abstractions";

        /// <summary>Die erweiternden Zahl-Umwandlungen von C# (implizit, ohne Verlust des Bereichs).</summary>
        private static bool Erweitert(SpecialType von, SpecialType nach)
        {
            switch (von)
            {
                case SpecialType.System_SByte: return nach is SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
                case SpecialType.System_Byte: return nach is SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
                case SpecialType.System_Int16: return nach is SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
                case SpecialType.System_UInt16: return nach is SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
                case SpecialType.System_Int32: return nach is SpecialType.System_Int64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
                case SpecialType.System_UInt32: return nach is SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64: return nach is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
                case SpecialType.System_Char: return nach is SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
                case SpecialType.System_Single: return nach is SpecialType.System_Double;
                default: return false;
            }
        }
    }
}
