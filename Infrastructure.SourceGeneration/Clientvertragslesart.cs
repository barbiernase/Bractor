// In Projekt: Infrastructure.SourceGeneration — per Link auch in Client.SourceGeneration (EINE Lesart, ein Hash).

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>Eine Zusage eines Akteur-Vertrags-Teils: Eingang (Event), erlaubte Ausgaben (konkrete Commands), Strom ja/nein.</summary>
    internal sealed class VertragsZusage
    {
        public VertragsZusage(INamedTypeSymbol ein, List<INamedTypeSymbol> aus, bool strom) { Ein = ein; Aus = aus; Strom = strom; }
        public INamedTypeSymbol Ein { get; }
        public List<INamedTypeSymbol> Aus { get; }
        public bool Strom { get; }
    }

    /// <summary>Ein Client-Vertrag (<c>IClientVertrag</c>, docs/konzept-akteure.md §4), gelesen aus seinen Symbolen.</summary>
    internal sealed class GelesenerClientVertrag
    {
        public INamedTypeSymbol Typ { get; set; } = null!;
        /// <summary>Handshake-Name (Interface ohne führendes I).</summary>
        public string Name { get; set; } = "";
        /// <summary>Client-Hash (<c>Abstractions.Akteurvertrag.ClientKanon</c>) — Server und Client rechnen ihn hier.</summary>
        public string Hash { get; set; } = "";
        /// <summary>Getragene Akteur-Vertrags-Teile je Akteur, nach Akteur-Name sortiert.</summary>
        public List<(INamedTypeSymbol Akteur, List<VertragsZusage> Zusagen)> Traegt { get; } = new List<(INamedTypeSymbol, List<VertragsZusage>)>();
        public List<INamedTypeSymbol> Sendet { get; set; } = new List<INamedTypeSymbol>();
        public List<INamedTypeSymbol> Fragt { get; set; } = new List<INamedTypeSymbol>();
        public List<INamedTypeSymbol> Kenntnis { get; set; } = new List<INamedTypeSymbol>();

        /// <summary>Was über die Leitung hereinkommt: die eigene Kenntnis und die Eingänge der getragenen Zusagen.</summary>
        public IEnumerable<INamedTypeSymbol> Eingaenge =>
            Kenntnis.Concat(Traegt.SelectMany(t => t.Zusagen).Select(z => z.Ein)).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    }

    /// <summary>
    /// Die EINE Lesart eines Client-Vertrags aus den Symbolen — für den Server-Generator (<c>GeneratedClientVertraege</c>) und den
    /// Client-Generator (<c>GeneratedWiring</c>: Handshake-Name, Hash, Eingänge). So ergibt derselbe Vertrag auf beiden Seiten denselben Hash.
    /// </summary>
    internal static class Clientvertragslesart
    {
        public const string IClientVertrag = "Abstractions.IClientVertrag";
        public const string IAkteurVertrag = "Abstractions.IAkteurVertrag`1";

        public static bool IstClient(INamedTypeSymbol t, INamedTypeSymbol? iClient) =>
            iClient != null && t.AllInterfaces.Contains(iClient, SymbolEqualityComparer.Default);

        /// <summary>Der Akteur eines Vertrags-Teils (erstes <c>IAkteurVertrag&lt;A&gt;</c> seiner Schnittstellen), sonst null.</summary>
        public static INamedTypeSymbol? AkteurVon(INamedTypeSymbol teil, INamedTypeSymbol? iVertrag) =>
            iVertrag == null ? null
                : teil.AllInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iVertrag))?.TypeArguments[0] as INamedTypeSymbol;

        public static GelesenerClientVertrag Lies(INamedTypeSymbol cl, Compilation c)
        {
            var iClient = c.GetTypeByMetadataName(IClientVertrag);
            var iVertrag = c.GetTypeByMetadataName(IAkteurVertrag);
            var iCommand = c.GetTypeByMetadataName("Abstractions.ICommand");
            var iEvent = c.GetTypeByMetadataName("Abstractions.IEvent");
            var iTransient = c.GetTypeByMetadataName("Abstractions.ITransientEvent");
            var v = new GelesenerClientVertrag { Typ = cl, Name = Abstractions.Akteurvertrag.ClientName(cl.Name) };

            // getragene Teile (auch über andere Client-Verträge geerbt) → je Akteur die Zusagen
            var getragen = cl.AllInterfaces.Where(i => !IstClient(i, iClient) && AkteurVon(i, iVertrag) != null).ToList();
            foreach (var g in getragen.GroupBy(t => AkteurVon(t, iVertrag)!, SymbolEqualityComparer.Default)
                         .Select(g => ((INamedTypeSymbol)g.Key!, Zusagen(g.ToList(), iCommand)))
                         .OrderBy(x => x.Item1.Name, System.StringComparer.Ordinal))
                v.Traegt.Add(g);

            var basen = new[] { cl }.Concat(cl.AllInterfaces.Where(i => IstClient(i, iClient))).ToList();
            List<INamedTypeSymbol> Generisch(string meta) => basen.SelectMany(b => b.Interfaces)
                .Where(i => i.OriginalDefinition.ToDisplayString().StartsWith(meta, System.StringComparison.Ordinal) && i.TypeArguments.Length == 1)
                .Select(i => i.TypeArguments[0]).OfType<INamedTypeSymbol>().Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
            v.Sendet = Generisch("Abstractions.ISendet<");
            v.Fragt = Generisch("Abstractions.IFragt<");
            v.Kenntnis = basen.SelectMany(b => b.GetMembers(Abstractions.Akteurvertrag.Auf).OfType<IMethodSymbol>())
                .Where(m => m.Parameters.Length == 1).Select(m => m.Parameters[0].Type).OfType<INamedTypeSymbol>()
                .Where(e => Implementiert(e, iEvent) || Implementiert(e, iTransient)).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();

            var kanon = Abstractions.Akteurvertrag.ClientKanon(v.Name,
                v.Traegt.Select(x => new KeyValuePair<string, IEnumerable<Abstractions.Akteurvertrag.Zusage>>(x.Akteur.Name,
                    x.Zusagen.Select(r => new Abstractions.Akteurvertrag.Zusage(r.Ein.Name, r.Aus.Select(y => y.Name).ToList(), r.Strom)))),
                v.Sendet.Select(t => t.Name), v.Fragt.Select(t => t.Name), v.Kenntnis.Select(t => t.Name));
            v.Hash = Abstractions.Akteurvertrag.Hash(kanon);
            return v;
        }

        /// <summary>Die Zusagen aus den Auf-Methoden der Teile (eigene Members, je Eingang einmal), Ausgaben = konkrete Commands.</summary>
        public static List<VertragsZusage> Zusagen(List<INamedTypeSymbol> teile, INamedTypeSymbol? iCommand)
        {
            var liste = new List<VertragsZusage>();
            var gesehen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var t in teile)
                foreach (var m in t.GetMembers(Abstractions.Akteurvertrag.Auf).OfType<IMethodSymbol>())
                    if (m.Parameters.Length == 1 && m.Parameters[0].Type is INamedTypeSymbol ein && gesehen.Add(ein))
                    {
                        var aus = VertragsAusgaben(m.ReturnType, out var strom).Where(x => Implementiert(x, iCommand)).ToList();
                        liste.Add(new VertragsZusage(ein, aus, strom));
                    }
            return liste;
        }

        /// <summary>Ausgaben einer Zusage: void → keine; T/OneOf&lt;…&gt; → eine; (Async)Enumerable davon → Strom.</summary>
        public static List<INamedTypeSymbol> VertragsAusgaben(ITypeSymbol rueckgabe, out bool strom)
        {
            strom = false;
            var el = rueckgabe;
            if (el.SpecialType == SpecialType.System_Void) return new List<INamedTypeSymbol>();
            if (el is INamedTypeSymbol en && en.TypeArguments.Length == 1
                && (en.Name == "IEnumerable" || en.Name == "IAsyncEnumerable"))
            {
                strom = true;
                el = en.TypeArguments[0];
            }
            if (el is INamedTypeSymbol oneOf && oneOf.Name == "OneOf")
                return oneOf.TypeArguments.OfType<INamedTypeSymbol>().ToList();
            return el is INamedTypeSymbol n ? new List<INamedTypeSymbol> { n } : new List<INamedTypeSymbol>();
        }

        public static bool Implementiert(INamedTypeSymbol t, INamedTypeSymbol? iface) =>
            iface != null && (SymbolEqualityComparer.Default.Equals(t, iface)
                || t.AllInterfaces.Contains(iface, SymbolEqualityComparer.Default));
    }
}
