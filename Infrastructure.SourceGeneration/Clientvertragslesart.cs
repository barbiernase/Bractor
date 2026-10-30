// In Projekt: Infrastructure.SourceGeneration — per Link auch in Client.SourceGeneration (EINE Lesart, ein Hash).

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>Ein Eingang einer getragenen Funktion (ein <c>Auf</c>): Eingang, die konkreten Ausgaben ihrer Signatur, Strom ja/nein.</summary>
    internal sealed class GetragenerEingang
    {
        public GetragenerEingang(INamedTypeSymbol ein, List<INamedTypeSymbol> aus, bool strom, bool mitHerkunft)
        { Ein = ein; Aus = aus; Strom = strom; MitHerkunft = mitHerkunft; }
        public INamedTypeSymbol Ein { get; }
        public List<INamedTypeSymbol> Aus { get; }
        public bool Strom { get; }
        public bool MitHerkunft { get; }
    }

    /// <summary>Ein Client-Vertrag (<c>IClientVertrag</c>, docs/konzept-akteure.md §4), gelesen aus seinen Symbolen.</summary>
    internal sealed class GelesenerClientVertrag
    {
        public INamedTypeSymbol Typ { get; set; } = null!;
        /// <summary>Handshake-Name (Interface ohne führendes I).</summary>
        public string Name { get; set; } = "";
        /// <summary>Client-Hash (<c>Abstractions.Clientvertragsregeln.ClientKanon</c>) — Server und Client rechnen ihn hier.</summary>
        public string Hash { get; set; } = "";
        /// <summary>Die verkörperten Akteure (<c>IVerkoerpert&lt;A&gt;</c>), nach Name sortiert — die einzige Befugnis-Angabe des Vertrags.</summary>
        public List<INamedTypeSymbol> Akteure { get; set; } = new List<INamedTypeSymbol>();
        /// <summary>Getragene Funktionen (<c>IDarf&lt;F&gt;</c> eines verkörperten Akteurs) mit ihren Eingängen, nach Funktions-Name sortiert.</summary>
        public List<(INamedTypeSymbol Funktion, List<GetragenerEingang> Eingaenge)> Traegt { get; } = new List<(INamedTypeSymbol, List<GetragenerEingang>)>();
        /// <summary>Was er von sich aus hineingibt: Commands, Quell-Nachrichten, Hinweise der verkörperten Akteure.</summary>
        public List<INamedTypeSymbol> Sendet { get; set; } = new List<INamedTypeSymbol>();
        /// <summary>Die Queries der verkörperten Akteure.</summary>
        public List<INamedTypeSymbol> Fragt { get; set; } = new List<INamedTypeSymbol>();
        /// <summary>Kenntnis (<c>IHoert&lt;T&gt;</c>) — genau das wird gepusht.</summary>
        public List<INamedTypeSymbol> Hoert { get; set; } = new List<INamedTypeSymbol>();
    }

    /// <summary>
    /// Die EINE Lesart eines Client-Vertrags aus den Symbolen — für den Server-Generator (<c>GeneratedClientVertraege</c>) und den
    /// Client-Generator (<c>GeneratedWiring</c>: Handshake-Name, Hash, Gehörtes). So ergibt derselbe Vertrag auf beiden Seiten denselben Hash.
    /// </summary>
    internal static class Clientvertragslesart
    {
        public const string IClientVertrag = "Abstractions.IClientVertrag";

        public static bool IstClient(INamedTypeSymbol t, INamedTypeSymbol? iClient) =>
            iClient != null && t.AllInterfaces.Contains(iClient, SymbolEqualityComparer.Default);

        public static GelesenerClientVertrag Lies(INamedTypeSymbol cl, Compilation c)
        {
            var iClient = c.GetTypeByMetadataName(IClientVertrag);
            var v = new GelesenerClientVertrag { Typ = cl, Name = Abstractions.Clientvertragsregeln.ClientName(cl.Name) };

            // IVerkoerpert/IHoert am Vertrag selbst und an den Client-Verträgen, die er erbt.
            var basen = new[] { cl }.Concat(cl.AllInterfaces.Where(i => IstClient(i, iClient))).ToList();
            List<INamedTypeSymbol> Generisch(string meta) => basen.SelectMany(b => b.Interfaces)
                .Where(i => i.OriginalDefinition.ToDisplayString().StartsWith(meta, System.StringComparison.Ordinal) && i.TypeArguments.Length == 1)
                .Select(i => i.TypeArguments[0]).OfType<INamedTypeSymbol>().Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
            v.Akteure = Generisch("Abstractions.IVerkoerpert<").OrderBy(a => a.Name, System.StringComparer.Ordinal).ToList();
            v.Hoert = Generisch("Abstractions.IHoert<");

            // Trägt, sendet, fragt: die IDarf der verkörperten Akteure, nach dem Marker ihres Typs (Clientvertragsregeln.Art).
            var darf = v.Akteure.SelectMany(a => a.AllInterfaces)
                .Where(i => i.OriginalDefinition.ToDisplayString().StartsWith("Abstractions.IDarf<", System.StringComparison.Ordinal) && i.TypeArguments.Length == 1)
                .Select(i => i.TypeArguments[0]).OfType<INamedTypeSymbol>().Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
            List<INamedTypeSymbol> Je(Abstractions.Clientvertragsregeln.Befugnisart art) => darf.Where(t => Art(t) == art).ToList();
            v.Sendet = Je(Abstractions.Clientvertragsregeln.Befugnisart.Sendet);
            v.Fragt = Je(Abstractions.Clientvertragsregeln.Befugnisart.Fragt);
            foreach (var f in Je(Abstractions.Clientvertragsregeln.Befugnisart.Traegt).OrderBy(f => f.Name, System.StringComparer.Ordinal))
                v.Traegt.Add((f, Eingaenge(f)));

            var kanon = Abstractions.Clientvertragsregeln.ClientKanon(v.Name, v.Akteure.Select(a => a.Name),
                v.Traegt.Select(x => new KeyValuePair<string, IEnumerable<Abstractions.Clientvertragsregeln.Eingang>>(x.Funktion.Name,
                    x.Eingaenge.Select(r => new Abstractions.Clientvertragsregeln.Eingang(r.Ein.Name, r.Aus.Select(y => y.Name).ToList(), r.Strom)))),
                v.Sendet.Select(t => t.Name), v.Fragt.Select(t => t.Name), v.Hoert.Select(t => t.Name));
            v.Hash = Abstractions.Clientvertragsregeln.Hash(kanon);
            return v;
        }

        /// <summary>Die Befugnisart eines <c>IDarf&lt;T&gt;</c> (die eine Regel <c>Abstractions.Clientvertragsregeln.Art</c>).</summary>
        public static Abstractions.Clientvertragsregeln.Befugnisart Art(ITypeSymbol t) =>
            Abstractions.Clientvertragsregeln.Art(
                m => t.ToDisplayString() == m || t.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == m),
                t.TypeKind == TypeKind.Interface);

        /// <summary>Die Eingänge einer getragenen Funktion aus ihren <c>Auf</c>-Methoden (die eine Lesart <see cref="Funktionsform"/>).</summary>
        public static List<GetragenerEingang> Eingaenge(INamedTypeSymbol funktion) =>
            Funktionsform.IstBaustein(funktion)
                ? Funktionsform.Lies(funktion).Aufs.Select(a => new GetragenerEingang(a.Eingang, a.Ausgaben, a.Strom, a.MitHerkunft)).ToList()
                : new List<GetragenerEingang>();

        public static bool Implementiert(INamedTypeSymbol t, INamedTypeSymbol? iface) =>
            iface != null && (SymbolEqualityComparer.Default.Equals(t, iface)
                || t.AllInterfaces.Contains(iface, SymbolEqualityComparer.Default));
    }
}
