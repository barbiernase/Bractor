using System.Collections.Generic;
using System.Linq;
using Abstractions;
using Microsoft.CodeAnalysis;

namespace Domain.SourceGeneration;

/// <summary>
/// WAS ein Typ ist, für die Lage-Prüfung (docs/07): die <see cref="Projektstruktur.Primitive"/> eines Symbols — nur aus Code-Fakten (Marker,
/// Signaturen, Konstruktor-Bindung), nie aus Name oder Ordner (Leitsatz W2). Eine Quelle für den Analyzer CQRS098 und <c>--check</c>
/// (per Link in <c>GraphExtractor.Kern</c>).
/// </summary>
internal sealed class Lageart
{
    private const string A = "Abstractions.";
    private readonly HashSet<string> _konfigs = new();
    private readonly HashSet<string> _aggregatQueries = new(), _aggregatAntworten = new();
    private readonly HashSet<string> _ausDecide = new();
    private readonly Infrastructure.SourceGeneration.Funktionsform.Technikmenge _technik = new();

    /// <summary>Ist der Vertrag (Abstractions) in der Compilation? Sonst gibt es nichts zu prüfen.</summary>
    public bool Bereit { get; }

    /// <param name="typen">Alle Typen, aus deren Signaturen Ergebnis-Events und Konfig-Records abgeleitet werden — auch aus anderen
    /// Compilations (verglichen wird über den vollen Namen, nie über Symbol-Identität).</param>
    public Lageart(Compilation c, IEnumerable<INamedTypeSymbol> typen)
    {
        Bereit = c.GetTypeByMetadataName(A + "IState") != null;
        if (!Bereit) return;
        var alle = typen.ToList();
        var technik = _technik = Infrastructure.SourceGeneration.Funktionsform.Techniken(alle);
        foreach (var t in alle)
        {
            // Fragen an ein Aggregat (Frage im Decider, docs/03 §3.2): ihre Query und ihre Antworten liegen bei der Schreibseite.
            if (Infrastructure.SourceGeneration.Projektionsform.StateVonDecider(t) is { } state)
                foreach (var f in Infrastructure.SourceGeneration.Projektionsform.LiesAggregatfragen(t, state).Fragen)
                {
                    _aggregatQueries.Add(Name(f.Query));
                    foreach (var a in f.Antworten) _aggregatAntworten.Add(Name(a));
                }
            // Commands ohne Ziel aus einem Decide (Nachrichtenform, docs/03 §3.1): sie liegen bei der Schreibseite; die übrigen geben nur Knoten
            //   einander (bei den Abläufen).
            if (Infrastructure.SourceGeneration.Nachrichtenform.IstDecider(t))
                foreach (var d in Infrastructure.SourceGeneration.Nachrichtenform.Decides(t))
                    foreach (var f in Infrastructure.SourceGeneration.Nachrichtenform.CommandsVon(d)) _ausDecide.Add(Name(f));
            // Konfig-Records: was der Konstruktor einer Funktion, ihrer Umsetzung oder einer Technik-Umsetzung als Konfig bindet (Ctorbindung, CQRS090).
            if (t.TypeKind == TypeKind.Class && !t.IsAbstract && (Hat(t, "IFunktion") || technik.IstUmsetzung(t)))
                foreach (var p in Infrastructure.SourceGeneration.Ctorbindung.Konstruktor(t)?.Parameters ?? Enumerable.Empty<IParameterSymbol>())
                    if (Infrastructure.SourceGeneration.Ctorbindung.ArtVon(p.Type) == Infrastructure.SourceGeneration.Ctorbindung.Art.Konfig
                        && p.Type is INamedTypeSymbol k) _konfigs.Add(Name(k));
        }
    }

    /// <summary>Die Primitive eines im Quelltext deklarierten Typs; <c>null</c> für verschachtelte Typen außer Decider/Applier.</summary>
    public Projektstruktur.Primitive? Von(INamedTypeSymbol t)
    {
        if (t.ContainingType is { } aussen)
        {
            if (!Hat(aussen, "IState")) return null;
            if (t.Name == Aggregatvertrag.Decider) return Projektstruktur.Primitive.Decider;
            if (t.Name == Aggregatvertrag.Applier) return Projektstruktur.Primitive.Applier;
            return null;
        }
        if (t.TypeKind == TypeKind.Enum) return Projektstruktur.Primitive.Enum;
        if (t.TypeKind == TypeKind.Interface)
        {
            if (IstFunktion(t)) return Projektstruktur.Primitive.Funktion;
            if (Hat(t, "IStore")) return Projektstruktur.Primitive.StoreBuendel;
            if (Hat(t, "IReadStore") || Hat(t, "IWriteStore")) return Projektstruktur.Primitive.StoreFaehigkeit;
            if (Hat(t, "IClientVertrag")) return Projektstruktur.Primitive.ClientVertrag;
            if (_technik.IstTechnik(t)) return Projektstruktur.Primitive.Technik;
            return Projektstruktur.Primitive.Hilfscode;
        }
        if (Hat(t, "IState")) return Projektstruktur.Primitive.Aggregat;
        if (Hat(t, "IAggregatCommand")) return Projektstruktur.Primitive.Command;
        if (Hat(t, "ICommand")) return _ausDecide.Contains(Name(t)) ? Projektstruktur.Primitive.Command : Projektstruktur.Primitive.KnotenCommand;
        if (Hat(t, "IGraph")) return Projektstruktur.Primitive.Graph;
        if (Hat(t, "IQuellNachricht")) return Projektstruktur.Primitive.Quelle;
        if (Hat(t, "IEvent")) return Projektstruktur.Primitive.Event;
        if (Hat(t, "IWertobjekt")) return Projektstruktur.Primitive.Wertobjekt;
        if (Hat(t, "IReadModel")) return Projektstruktur.Primitive.ReadModel;
        if (Hat(t, "IQuery")) return _aggregatQueries.Contains(Name(t)) ? Projektstruktur.Primitive.AggregatQuery : Projektstruktur.Primitive.Query;
        if (Hat(t, "IQueryResponse")) return _aggregatAntworten.Contains(Name(t)) ? Projektstruktur.Primitive.AggregatAntwort : Projektstruktur.Primitive.Antwort;
        if (Hat(t, "IProjektion")) return Projektstruktur.Primitive.Projektion;
        if (Hat(t, "IReader")) return Projektstruktur.Primitive.Reader;
        if (Hat(t, "IFunktion"))
            return Infrastructure.SourceGeneration.Funktionsform.IstFunktion(t) ? Projektstruktur.Primitive.HostFunktion : Projektstruktur.Primitive.FunktionsImplementierung;
        if (Hat(t, "IAkteur")) return Projektstruktur.Primitive.Akteur;
        if (Hat(t, "IStore") || Hat(t, "IReadStore") || Hat(t, "IWriteStore")) return Projektstruktur.Primitive.StoreImplementierung;
        if (_konfigs.Contains(Name(t))) return Projektstruktur.Primitive.Konfig;
        if (_technik.IstTechnik(t) || _technik.IstUmsetzung(t)) return Projektstruktur.Primitive.Technik;
        return Projektstruktur.Primitive.Hilfscode;
    }

    /// <summary>Die Bündel (<c>: IStore</c>) unter <paramref name="typen"/>, die die Fähigkeit <paramref name="t"/> enthalten (ihre Datei-Kandidaten).</summary>
    public static IEnumerable<string> Buendel(INamedTypeSymbol t, IEnumerable<INamedTypeSymbol> typen) =>
        typen.Where(b => b.TypeKind == TypeKind.Interface && Hat(b, "IStore") && b.AllInterfaces.Any(i => Name(i) == Name(t))).Select(b => b.Name).Distinct();

    /// <summary>Der Aggregat-Name eines Deciders/Appliers (der umschließende State).</summary>
    public static string? Aggregat(INamedTypeSymbol t) => t.ContainingType?.Name;

    private static string Name(INamedTypeSymbol t) => t.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + t.OriginalDefinition.MetadataName;

    private static bool IstFunktion(INamedTypeSymbol t) =>
        t.TypeKind == TypeKind.Interface && Name(t) != A + "IFunktion" && Hat(t, "IFunktion");

    /// <summary>Implementiert <paramref name="t"/> den Vertrags-Marker (Metadaten-Name ohne „Abstractions.“, generisch mit `n)?</summary>
    private static bool Hat(INamedTypeSymbol t, string marker) => t.AllInterfaces.Any(i => Name(i) == A + marker);
}
