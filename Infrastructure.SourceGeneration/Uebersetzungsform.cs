// In Projekt: Infrastructure.SourceGeneration (per Link auch in Domain.SourceGeneration und GraphExtractor.Kern)
// Dateiname: Uebersetzungsform.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Die EINE Lesart einer ÜBERSETZUNG (docs/04 §4.4) für Generator, Analyzer und Extractor — nur Code-Fakten: ein Knoten
    /// <c>Uebersetze&lt;TEin, TAus&gt;</c> eines Graph-Records und seine <c>[Feld(nameof(Ziel), nameof(Quelle))]</c>. Je Parameter des Konstruktors von
    /// <c>TAus</c> eine Quelle, in dieser Reihenfolge: <c>[Feld]</c> am Knoten (ausdrücklich darf ein <c>Guid</c> ein <c>Id&lt;T&gt;</c> füllen und
    /// umgekehrt), eine Eigenschaft von <c>TEin</c> mit gleichem Namen und implizit zuweisbarem Typ, der Standardwert des Parameters — sonst CQRS119.
    /// <c>TEin</c> ist ein Command ohne Ziel, <c>TAus</c> ein Command (ohne Ziel oder an ein Aggregat), sonst CQRS121.
    /// </summary>
    internal static class Uebersetzungsform
    {
        public const string Feld = "Abstractions.FeldAttribute";

        /// <summary>Die Regeln einer Übersetzung (Analyzer-Kennungen).</summary>
        public const string Zuordnung = "CQRS119", Verlassen = "CQRS120", Nachricht = "CQRS121";

        /// <summary>Woher ein Parameter von <c>TAus</c> kommt.</summary>
        public const string AusFeld = "feld", AusName = "name", AusStandard = "standard";

        /// <summary>Ein Parameter von <c>TAus</c> und seine Quelle (null, wenn keine — dann steht ein Fehler in der Form).</summary>
        internal sealed class Ziel
        {
            public string Name = "";
            public ITypeSymbol Typ = null!;
            /// <summary>Die Eigenschaft von <c>TEin</c> (bei <see cref="AusStandard"/> und ohne Quelle null).</summary>
            public string? Quelle;
            /// <summary><see cref="AusFeld"/>, <see cref="AusName"/>, <see cref="AusStandard"/> — leer: offen.</summary>
            public string Regel = "";
            /// <summary>Der C#-Ausdruck über der genommenen Nachricht <c>e</c> (leer bei Standard und offen).</summary>
            public string Ausdruck = "";
        }

        /// <summary>Eine gelesene Übersetzung: Ein, Aus, die Felder von <c>TEin</c>, je Parameter von <c>TAus</c> seine Quelle und die Fehler.</summary>
        internal sealed class Form
        {
            public ITypeSymbol Ein = null!, Aus = null!;
            /// <summary>Die lesbaren Eigenschaften von <c>TEin</c> (öffentlich, Instanz), in Reihenfolge der Deklaration.</summary>
            public List<IPropertySymbol> Felder = new List<IPropertySymbol>();
            public List<Ziel> Ziele = new List<Ziel>();
            /// <summary>Die <c>[Feld]</c>, wie sie dastehen (Ziel, Quelle).</summary>
            public List<(string Ziel, string Quelle)> Abweichungen = new List<(string, string)>();
            public List<(string Id, string Text, Location Ort)> Fehler = new List<(string, string, Location)>();
        }

        /// <summary>Ist <paramref name="t"/> eine Übersetzung — <c>Abstractions.Uebersetze&lt;TEin, TAus&gt;</c>?</summary>
        public static bool Ist(ITypeSymbol t) =>
            t is INamedTypeSymbol { IsGenericType: true, Arity: 2, Name: "Uebersetze" } n
            && n.OriginalDefinition.ContainingNamespace?.ToDisplayString() == "Abstractions";

        /// <summary>Die <c>[Feld]</c> eines Parameters (Ziel, Quelle), wie sie dastehen.</summary>
        public static List<(string Ziel, string Quelle, Location Ort)> Felder(IParameterSymbol p) =>
            p.GetAttributes().Where(a => a.AttributeClass != null && Funktionsform.Name(a.AttributeClass) == Feld && a.ConstructorArguments.Length == 2)
                .Select(a => (a.ConstructorArguments[0].Value as string ?? "", a.ConstructorArguments[1].Value as string ?? "",
                    a.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? p.Locations.FirstOrDefault() ?? Location.None))
                .ToList();

        /// <summary>Liest die Übersetzung <paramref name="u"/> am Knoten <paramref name="p"/> (dessen <c>[Feld]</c>).</summary>
        public static Form Lies(INamedTypeSymbol u, IParameterSymbol p, Location ort)
        {
            var f = new Form { Ein = u.TypeArguments[0], Aus = u.TypeArguments[1] };
            var felder = Felder(p);
            f.Abweichungen = felder.Select(x => (x.Ziel, x.Quelle)).ToList();
            var ein = f.Ein as INamedTypeSymbol;
            var aus = f.Aus as INamedTypeSymbol;
            if (ein == null || !Funktionsform.IstCommandOhneZiel(ein))
                f.Fehler.Add((Nachricht, $"die Übersetzung '{p.Name}' nimmt {f.Ein.ToDisplayString()} — sie nimmt einen Command ohne Ziel (die Ausgabe "
                    + "einer Funktion oder die Nachricht eines Clients)", ort));
            if (aus == null || aus.TypeKind != TypeKind.Class || aus.IsAbstract || aus.IsGenericType && aus.TypeArguments.Any(a => a.TypeKind == TypeKind.TypeParameter)
                || !Funktionsform.Hat(aus, "Abstractions.ICommand"))
                f.Fehler.Add((Nachricht, $"die Übersetzung '{p.Name}' erzeugt {f.Aus.ToDisplayString()} — sie erzeugt einen Command (ohne Ziel an einen "
                    + "Knoten oder Graphen, mit Ziel an ein Aggregat)", ort));
            if (f.Fehler.Count > 0) return f;

            for (var t = (ITypeSymbol?)ein; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
                foreach (var e in t.GetMembers().OfType<IPropertySymbol>())
                    if (e.DeclaredAccessibility == Accessibility.Public && !e.IsStatic && !e.IsIndexer && e.GetMethod != null && e.Name != "EqualityContract"
                        && !f.Felder.Any(x => x.Name == e.Name))
                        f.Felder.Add(e);

            var ctor = aus!.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public && !IstKopie(c, aus))
                .OrderByDescending(c => c.Parameters.Length).FirstOrDefault();
            if (ctor == null)
            {
                f.Fehler.Add((Nachricht, $"{aus.Name} hat keinen öffentlichen Konstruktor — eine Übersetzung baut ihre Nachricht über ihn", ort));
                return f;
            }
            foreach (var (ziel, quelle, fOrt) in felder)
            {
                if (!ctor.Parameters.Any(x => x.Name == ziel))
                    f.Fehler.Add((Zuordnung, $"[Feld] an '{p.Name}' nennt das Ziel '{ziel}' — kein Parameter von {aus.Name} ({Namen(ctor.Parameters.Select(x => x.Name))})", fOrt));
                else if (felder.Count(x => x.Ziel == ziel) > 1)
                    f.Fehler.Add((Zuordnung, $"[Feld] an '{p.Name}' nennt das Ziel '{ziel}' mehrfach — je Parameter eine Quelle", fOrt));
                if (!f.Felder.Any(x => x.Name == quelle))
                    f.Fehler.Add((Zuordnung, $"[Feld] an '{p.Name}' nennt die Quelle '{quelle}' — keine Eigenschaft von {ein!.Name} ({Namen(f.Felder.Select(x => x.Name))})", fOrt));
            }
            foreach (var par in ctor.Parameters)
            {
                var z = new Ziel { Name = par.Name, Typ = par.Type };
                f.Ziele.Add(z);
                var feld = felder.FirstOrDefault(x => x.Ziel == par.Name);
                if (feld.Ziel != null)
                {
                    var q = f.Felder.FirstOrDefault(x => x.Name == feld.Quelle);
                    if (q == null) continue;   // gemeldet
                    z.Quelle = q.Name;
                    var a = Ausdruck(q.Type, par.Type, "e.@" + q.Name, ausdruecklich: true);
                    if (a == null)
                        f.Fehler.Add((Zuordnung, $"[Feld] an '{p.Name}': {ein!.Name}.{q.Name} ({q.Type.ToDisplayString()}) passt nicht in {aus.Name}.{par.Name} "
                            + $"({par.Type.ToDisplayString()}) — der Typ ist nicht zuweisbar (übersetzt wird ohne Logik; eine Umrechnung ist eine Funktion)", feld.Ort));
                    else { z.Regel = AusFeld; z.Ausdruck = a; }
                    continue;
                }
                var gleich = f.Felder.FirstOrDefault(x => x.Name == par.Name) ?? f.Felder.FirstOrDefault(x => string.Equals(x.Name, par.Name, System.StringComparison.OrdinalIgnoreCase));
                var ausdruck = gleich == null ? null : Ausdruck(gleich.Type, par.Type, "e.@" + gleich.Name, ausdruecklich: false);
                if (ausdruck != null) { z.Quelle = gleich!.Name; z.Regel = AusName; z.Ausdruck = ausdruck; continue; }
                if (par.HasExplicitDefaultValue) { z.Regel = AusStandard; continue; }
                f.Fehler.Add((Zuordnung, $"'{p.Name}': {aus.Name}.{par.Name} ({par.Type.ToDisplayString()}) bekommt nichts — "
                    + (gleich != null
                        ? $"{ein!.Name}.{gleich.Name} ({gleich.Type.ToDisplayString()}) ist nicht implizit zuweisbar"
                        : $"{ein!.Name} hat kein Feld {par.Name}")
                    + $"; nenne die Quelle mit [Feld(nameof({aus.Name}.{par.Name}), nameof({ein.Name}.…))]", ort));
            }
            return f;
        }

        /// <summary>Der Kopier-Konstruktor eines Records (<c>X(X original)</c>) zählt nicht.</summary>
        private static bool IstKopie(IMethodSymbol c, INamedTypeSymbol t) =>
            c.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, t);

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
            if (SymbolEqualityComparer.Default.Equals(von, nach)) return quelle;
            if (nach is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nn && SymbolEqualityComparer.Default.Equals(nn.TypeArguments[0], von))
                return quelle;
            if (von.IsReferenceType && nach.IsReferenceType)
            {
                for (var b = von.BaseType; b != null; b = b.BaseType)
                    if (SymbolEqualityComparer.Default.Equals(b, nach)) return quelle;
                if (von.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, nach))) return quelle;
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
