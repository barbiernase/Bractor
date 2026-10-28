// In Projekt: Infrastructure.SourceGeneration
// Dateiname: Leseseitenbild.cs

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Die Leseseite einer Anwendung, wie die Komposition sie sieht (nur dort sind Projektionen, Reader und Store-Klassen zugleich sichtbar):
    /// Projektionen und Reader in ihrer Lesart (<see cref="Projektionsform"/>), je Fähigkeit die Store-Klassen, die sie implementieren, je
    /// Projektion ihre Stores und je Frage die Projektionen dahinter (die in dieselben Stores schreiben).
    /// </summary>
    internal sealed class Leseseitenbild
    {
        public List<Projektionsform.Projektion> Projektionen = new List<Projektionsform.Projektion>();
        public List<Projektionsform.Reader> Reader = new List<Projektionsform.Reader>();
        public List<INamedTypeSymbol> StoreKlassen = new List<INamedTypeSymbol>();

        public static Leseseitenbild Lies(Compilation c)
        {
            var b = new Leseseitenbild();
            foreach (var t in Vertragsassemblies.Typen(c).OrderBy(t => t.ToDisplayString(), System.StringComparer.Ordinal))
            {
                if (Projektionsform.IstProjektion(t)) b.Projektionen.Add(Projektionsform.LiesProjektion(t));
                else if (Projektionsform.IstReader(t)) b.Reader.Add(Projektionsform.LiesReader(t));
                else if (t.TypeKind == TypeKind.Class && !t.IsAbstract
                         && t.AllInterfaces.Any(i => Projektionsform.IstSchreibFaehigkeit(i) || Projektionsform.IstLeseFaehigkeit(i)))
                    b.StoreKlassen.Add(t);
            }
            return b;
        }

        /// <summary>Die Store-Klassen, die eine Fähigkeit implementieren.</summary>
        public List<INamedTypeSymbol> Stores(INamedTypeSymbol faehigkeit) =>
            StoreKlassen.Where(k => k.AllInterfaces.Any(i => Projektionsform.Name(i) == Projektionsform.Name(faehigkeit))).ToList();

        /// <summary>Die Store-Klassen hinter den Schreib-Fähigkeiten einer Projektion.</summary>
        public List<INamedTypeSymbol> Stores(Projektionsform.Projektion p) =>
            p.SchreibFaehigkeiten().SelectMany(Stores).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                .OrderBy(k => k.ToDisplayString(), System.StringComparer.Ordinal).ToList();

        /// <summary>Die Projektionen hinter einer Frage: die, die in einen Store schreiben, dessen Lese-Fähigkeit die Frage nimmt.</summary>
        public List<Projektionsform.Projektion> Hinter(Projektionsform.Frage f)
        {
            var stores = new HashSet<INamedTypeSymbol>(f.Faehigkeiten.SelectMany(Stores), SymbolEqualityComparer.Default);
            return Projektionen.Where(p => Stores(p).Any(stores.Contains)).ToList();
        }
    }
}
