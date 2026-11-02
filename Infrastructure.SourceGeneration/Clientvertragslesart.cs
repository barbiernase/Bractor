// In Projekt: Infrastructure.SourceGeneration — per Link auch in Client.SourceGeneration und Domain.SourceGeneration (EINE Lesart, ein Hash).

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>Ein Eingang der Arbeit eines Akteurs: der gehörte Command ohne Ziel und die Commands, die er als Antwort geben darf (sein IDarf).</summary>
    internal sealed class Arbeitseingang
    {
        public Arbeitseingang(INamedTypeSymbol ein, List<INamedTypeSymbol> aus) { Ein = ein; Aus = aus; }
        public INamedTypeSymbol Ein { get; }
        public List<INamedTypeSymbol> Aus { get; }
        /// <summary>Eine Antwort darf in Stücken kommen (ein langer Lauf meldet Fortschritt) — immer.</summary>
        public bool Strom => true;
    }

    /// <summary>Ein Akteur (<c>IAkteur</c>), gelesen aus seiner Basisliste: was er hineingibt (<c>IDarf</c>) und was zu ihm kommt (<c>IHoert</c>).</summary>
    internal sealed class GelesenerAkteur
    {
        public INamedTypeSymbol Typ { get; set; } = null!;
        /// <summary>Commands und Hinweise, die er hineingibt.</summary>
        public List<INamedTypeSymbol> Sendet { get; set; } = new List<INamedTypeSymbol>();
        /// <summary>Seine Queries.</summary>
        public List<INamedTypeSymbol> Fragt { get; set; } = new List<INamedTypeSymbol>();
        /// <summary>Kenntnis: die Events und Hinweise, die er hört (gepusht, verlierbar).</summary>
        public List<INamedTypeSymbol> Kenntnis { get; set; } = new List<INamedTypeSymbol>();
        /// <summary>Arbeit: die Commands ohne Ziel, die er hört, je mit den Commands, die er als Antwort geben darf.</summary>
        public List<Arbeitseingang> Arbeit { get; set; } = new List<Arbeitseingang>();
    }

    /// <summary>Ein Client (<c>IClient</c>, docs/konzept-akteure.md §4), gelesen aus seinem Record und seinen Akteuren.</summary>
    internal sealed class GelesenerClientVertrag
    {
        public INamedTypeSymbol Typ { get; set; } = null!;
        /// <summary>Handshake-Name (der Record-Name).</summary>
        public string Name { get; set; } = "";
        /// <summary>Client-Hash (<c>Abstractions.Clientvertragsregeln.ClientKanon</c>) — Server und Client rechnen ihn hier.</summary>
        public string Hash { get; set; } = "";
        /// <summary>Die verkörperten Akteure (<c>IVerkoerpert&lt;A&gt;</c>), nach Name sortiert — die einzige Angabe des Clients.</summary>
        public List<INamedTypeSymbol> Akteure { get; set; } = new List<INamedTypeSymbol>();
        /// <summary>Die Arbeit der verkörperten Akteure, nach Akteur sortiert (nur die Akteure, die Commands hören).</summary>
        public List<(INamedTypeSymbol Akteur, List<Arbeitseingang> Eingaenge)> Arbeit { get; } = new List<(INamedTypeSymbol, List<Arbeitseingang>)>();
        /// <summary>Was er hineingibt: Commands und Hinweise der verkörperten Akteure.</summary>
        public List<INamedTypeSymbol> Sendet { get; set; } = new List<INamedTypeSymbol>();
        /// <summary>Die Queries der verkörperten Akteure.</summary>
        public List<INamedTypeSymbol> Fragt { get; set; } = new List<INamedTypeSymbol>();
        /// <summary>Kenntnis der verkörperten Akteure — genau das wird gepusht.</summary>
        public List<INamedTypeSymbol> Hoert { get; set; } = new List<INamedTypeSymbol>();
    }

    /// <summary>
    /// Die EINE Lesart von Akteuren und Clients aus den Symbolen — für den Server-Generator (<c>GeneratedAkteurRechte</c>,
    /// <c>GeneratedClientVertraege</c>), den Client-Generator (<c>GeneratedWiring</c>: Handshake-Name, Hash, Gehörtes) und die Analyzer. So ergibt
    /// derselbe Client auf beiden Seiten denselben Hash.
    /// </summary>
    internal static class Clientvertragslesart
    {
        public const string IClient = "Abstractions.IClient", IAkteur = "Abstractions.IAkteur";

        public static bool IstClient(INamedTypeSymbol t, INamedTypeSymbol? iClient) =>
            iClient != null && t.TypeKind != TypeKind.Interface && t.AllInterfaces.Contains(iClient, SymbolEqualityComparer.Default);

        private static List<INamedTypeSymbol> Generisch(INamedTypeSymbol t, string meta) => t.AllInterfaces
            .Where(i => i.OriginalDefinition.ToDisplayString().StartsWith(meta, System.StringComparison.Ordinal) && i.TypeArguments.Length == 1)
            .Select(i => i.TypeArguments[0]).OfType<INamedTypeSymbol>().Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();

        private static bool Hat(ITypeSymbol t, string m) => t.ToDisplayString() == m || t.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == m);

        /// <summary>Liest einen Akteur: IDarf nach Art (senden, fragen), IHoert nach Arbeit (Command ohne Ziel) oder Kenntnis (Event, Hinweis).</summary>
        public static GelesenerAkteur LiesAkteur(INamedTypeSymbol a)
        {
            var g = new GelesenerAkteur { Typ = a };
            var darf = Generisch(a, "Abstractions.IDarf<");
            g.Sendet = darf.Where(t => Art(t) == Abstractions.Clientvertragsregeln.Befugnisart.Sendet).ToList();
            g.Fragt = darf.Where(t => Art(t) == Abstractions.Clientvertragsregeln.Befugnisart.Fragt).ToList();
            var antworten = g.Sendet.Where(t => Hat(t, Abstractions.Clientvertragsregeln.ICommand)).OrderBy(t => t.Name, System.StringComparer.Ordinal).ToList();
            foreach (var h in Generisch(a, "Abstractions.IHoert<"))
            {
                if (IstArbeit(h)) g.Arbeit.Add(new Arbeitseingang(h, antworten));
                else g.Kenntnis.Add(h);
            }
            g.Arbeit = g.Arbeit.OrderBy(x => x.Ein.Name, System.StringComparer.Ordinal).ToList();
            return g;
        }

        public static GelesenerClientVertrag Lies(INamedTypeSymbol cl, Compilation c)
        {
            var v = new GelesenerClientVertrag { Typ = cl, Name = Abstractions.Clientvertragsregeln.ClientName(cl.Name) };
            v.Akteure = Generisch(cl, "Abstractions.IVerkoerpert<").OrderBy(a => a.Name, System.StringComparer.Ordinal).ToList();
            var akteure = v.Akteure.Select(LiesAkteur).ToList();
            List<INamedTypeSymbol> U(System.Func<GelesenerAkteur, IEnumerable<INamedTypeSymbol>> f) =>
                akteure.SelectMany(f).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
            v.Sendet = U(a => a.Sendet);
            v.Fragt = U(a => a.Fragt);
            v.Hoert = U(a => a.Kenntnis);
            foreach (var a in akteure.Where(a => a.Arbeit.Count > 0)) v.Arbeit.Add((a.Typ, a.Arbeit));

            var kanon = Abstractions.Clientvertragsregeln.ClientKanon(v.Name, v.Akteure.Select(a => a.Name),
                v.Arbeit.Select(x => new KeyValuePair<string, IEnumerable<Abstractions.Clientvertragsregeln.Eingang>>(x.Akteur.Name,
                    x.Eingaenge.Select(r => new Abstractions.Clientvertragsregeln.Eingang(r.Ein.Name, r.Aus.Select(y => y.Name).ToList(), r.Strom)))),
                v.Sendet.Select(t => t.Name), v.Fragt.Select(t => t.Name), v.Hoert.Select(t => t.Name));
            v.Hash = Abstractions.Clientvertragsregeln.Hash(kanon);
            return v;
        }

        /// <summary>Die Befugnisart eines <c>IDarf&lt;T&gt;</c> (die eine Regel <c>Abstractions.Clientvertragsregeln.Art</c>).</summary>
        public static Abstractions.Clientvertragsregeln.Befugnisart Art(ITypeSymbol t) =>
            Abstractions.Clientvertragsregeln.Art(m => Hat(t, m), t.TypeKind == TypeKind.Interface);

        /// <summary>Ist ein gehörter Typ Arbeit (ein Command ohne Ziel)? Die eine Regel <c>Abstractions.Clientvertragsregeln.IstArbeit</c>.</summary>
        public static bool IstArbeit(ITypeSymbol t) => Abstractions.Clientvertragsregeln.IstArbeit(m => Hat(t, m));

        public static bool Implementiert(INamedTypeSymbol t, INamedTypeSymbol? iface) =>
            iface != null && (SymbolEqualityComparer.Default.Equals(t, iface)
                || t.AllInterfaces.Contains(iface, SymbolEqualityComparer.Default));
    }
}
