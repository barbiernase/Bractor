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
    private readonly HashSet<string> _ergebnisse = new();
    private readonly HashSet<string> _konfigs = new();

    /// <summary>Ist der Vertrag (Abstractions) in der Compilation? Sonst gibt es nichts zu prüfen.</summary>
    public bool Bereit { get; }

    /// <param name="typen">Alle Typen, aus deren Signaturen Ergebnis-Events und Konfig-Records abgeleitet werden — auch aus anderen
    /// Compilations (verglichen wird über den vollen Namen, nie über Symbol-Identität).</param>
    public Lageart(Compilation c, IEnumerable<INamedTypeSymbol> typen)
    {
        Bereit = c.GetTypeByMetadataName(A + "IState") != null;
        if (!Bereit) return;
        foreach (var t in typen)
        {
            // Ergebnis-Events: die Ausgaben jeder Funktion — der Katalog-Form (OneOf in RufeAsync, CQRS068) und der Baustein-Form (die Fälle
            //   jedes Auf, Funktionsform). Commands unter ihnen bleiben Commands (Command wird vor Event erkannt).
            if (IstFunktion(t))
                foreach (var m in t.GetMembers(Funktionsvertrag.Methode).OfType<IMethodSymbol>())
                    foreach (var e in Blaetter(m.ReturnType)) _ergebnisse.Add(Name(e));
            if (Infrastructure.SourceGeneration.Funktionsform.IstBaustein(t))
                foreach (var a in Infrastructure.SourceGeneration.Funktionsform.Lies(t).Aufs)
                    foreach (var e in a.Ausgaben) _ergebnisse.Add(Name(e));
            // Konfig-Records: was der Konstruktor einer Funktions-/Dienst-Implementierung als Konfig bindet (Ctorbindung, CQRS090).
            if (t.TypeKind == TypeKind.Class && !t.IsAbstract && (Hat(t, "IFunktion") || Hat(t, Dienst)))
                foreach (var p in Infrastructure.SourceGeneration.Ctorbindung.Konstruktor(t)?.Parameters ?? Enumerable.Empty<IParameterSymbol>())
                    if (Infrastructure.SourceGeneration.Ctorbindung.ArtVon(p.Type, null) == Infrastructure.SourceGeneration.Ctorbindung.Art.Konfig
                        && p.Type is INamedTypeSymbol k) _konfigs.Add(Name(k));
        }
    }

    private static readonly string Dienst = Dienstvertrag.Marker.Substring(A.Length);

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
            if (Name(t) != Dienstvertrag.Marker && Hat(t, Dienst)) return Projektstruktur.Primitive.Dienst;
            if (Hat(t, "IStore")) return Projektstruktur.Primitive.StoreBuendel;
            if (Hat(t, "IReadStore") || Hat(t, "IWriteStore")) return Projektstruktur.Primitive.StoreFaehigkeit;
            // Ein Client-Vertrag trägt Akteur-Vertrags-Teile — erst der Client, dann der Akteur-Vertrag.
            if (Hat(t, "IClientVertrag")) return Projektstruktur.Primitive.ClientVertrag;
            if (Hat(t, "IAkteurVertrag`1")) return Projektstruktur.Primitive.AkteurVertrag;
            return Projektstruktur.Primitive.Hilfscode;
        }
        if (Hat(t, "IState")) return Projektstruktur.Primitive.Aggregat;
        if (Hat(t, "ICommand")) return Projektstruktur.Primitive.Command;
        if (Hat(t, "IQuellNachricht")) return Projektstruktur.Primitive.Quelle;
        if (Hat(t, "IEvent")) return _ergebnisse.Contains(Name(t)) ? Projektstruktur.Primitive.FunktionsErgebnis : Projektstruktur.Primitive.Event;
        if (Hat(t, "IProzessDefinition")) return Projektstruktur.Primitive.Prozess;
        if (Hat(t, "IPipeline")) return Projektstruktur.Primitive.Fluss;
        if (Hat(t, "IWertobjekt")) return Projektstruktur.Primitive.Wertobjekt;
        if (Hat(t, "IReadModel")) return Projektstruktur.Primitive.ReadModel;
        if (Hat(t, "IQuery")) return Projektstruktur.Primitive.Query;
        if (Hat(t, "IQueryResponse")) return Projektstruktur.Primitive.Antwort;
        if (Hat(t, "ISubscriber")) return Projektstruktur.Primitive.Projektion;
        if (Hat(t, "IReader`1")) return Projektstruktur.Primitive.Reader;
        if (Hat(t, "IAuftrag`1")) return Projektstruktur.Primitive.Auftrag;
        if (Hat(t, "IFunktion"))
            return Infrastructure.SourceGeneration.Funktionsform.IstBaustein(t) ? Projektstruktur.Primitive.HostFunktion : Projektstruktur.Primitive.FunktionsImplementierung;
        if (Hat(t, Dienst)) return Projektstruktur.Primitive.DienstImplementierung;
        if (Hat(t, "IAkteur")) return Projektstruktur.Primitive.Akteur;
        if (Hat(t, "IStore") || Hat(t, "IReadStore") || Hat(t, "IWriteStore")) return Projektstruktur.Primitive.StoreImplementierung;
        if (_konfigs.Contains(Name(t))) return Projektstruktur.Primitive.Konfig;
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

    /// <summary>Die konkreten Typen in <c>Task&lt;OneOf&lt;A, B&gt;&gt;</c> bzw. <c>Task&lt;A&gt;</c>.</summary>
    private static IEnumerable<INamedTypeSymbol> Blaetter(ITypeSymbol t)
    {
        if (t is not INamedTypeSymbol n) yield break;
        if (n.IsGenericType && (n.Name == "Task" || n.Name == "ValueTask" || n.Name == "OneOf"))
        {
            foreach (var a in n.TypeArguments)
                foreach (var b in Blaetter(a)) yield return b;
            yield break;
        }
        yield return n.OriginalDefinition;
    }
}
