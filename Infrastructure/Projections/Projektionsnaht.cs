using Abstractions;
using Core;
using Marten;

namespace Infrastructure.Projections;

/// <summary>
/// Der Fortschritt, den das Framework für die Store-Klassen 2 und 3 hält (Plan Pub/Sub-Neustrukturierung §2.4): dieselben Dokumente wie beim
/// Marten-Store der Klasse 1, aber in einer eigenen Transaktion NACH den Wirkungen des Stores.
/// </summary>
public interface IFortschrittBeimFramework : IFortschrittsAblage
{
    /// <summary>Guards und Fortschritt mit erwarteter Revision; liefert die neue. Wirft <see cref="PartitionsKonfliktException"/> bei einer zweiten
    /// Aktivierung oder einem Neuaufbau dazwischen.</summary>
    Task<int> SchreibeFortschrittAsync(PartitionsCheckpoint fortschritt, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct);
}

/// <summary>Der Fortschritt beim Framework in Marten (im Log-Postgres).</summary>
public sealed class FortschrittBeimFramework(IDocumentStore store) : IFortschrittBeimFramework
{
    public Task<PartitionsCheckpoint> LadeFortschrittAsync(string projektion, int partition, CancellationToken ct)
        => MartenFortschritt.LadeAsync(store, projektion, partition, ct);

    public Task<IReadOnlyDictionary<Guid, int>> LadeGuardsAsync(string projektion, IReadOnlyCollection<Guid> streams, CancellationToken ct)
        => MartenFortschritt.LadeGuardsAsync(store, projektion, streams, ct);

    public Task FortschrittZuruecksetzenAsync(string projektion, int partitionen, string generation, CancellationToken ct)
        => MartenFortschritt.ZuruecksetzenAsync(store, projektion, partitionen, generation, ct);

    public async Task<int> SchreibeFortschrittAsync(PartitionsCheckpoint fortschritt, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct)
    {
        try
        {
            await using var s = store.LightweightSession();
            var neu = MartenFortschritt.Lege(s, fortschritt, guards);
            await s.SaveChangesAsync(ct);
            return neu;
        }
        catch (Exception ex) when (MartenFortschritt.Konflikt(ex, fortschritt, nameof(FortschrittBeimFramework)) is { } konflikt)
        {
            throw konflikt;
        }
    }
}

/// <summary>
/// Die Naht zwischen Batch-Leser und Store einer Projektion: wo der Fortschritt liegt und wie ein Batch gültig wird — je nach Store-Klasse
/// (Plan §2.4, §4.2):
/// <list type="bullet">
///   <item><b>Klasse 1</b> (<see cref="IAtomaresZiel"/>): der Store hält den Fortschritt, Wirkungen und Fortschritt in einer Transaktion.</item>
///   <item><b>Klasse 2</b> (<see cref="IWiederholungsfestesZiel"/>) und <b>3</b> (<see cref="INurMindestensEinmal"/>): erst die Wirkungen
///     (<see cref="IProjektionsZiel.SchreibeAsync"/>), dann der Fortschritt beim Framework. Ein Abbruch dazwischen wiederholt die Wirkungen —
///     Klasse 2 ergibt dabei dasselbe, Klasse 3 nicht (darum nur mit Erlaubnis).</item>
/// </list>
/// </summary>
public sealed class Projektionsnaht
{
    private readonly IFortschrittBeimFramework? _framework;

    public Projektionsnaht(IProjektionsZiel ziel, IFortschrittBeimFramework? framework)
    {
        Ziel = ziel;
        Klasse = Klassenzahl(ziel);
        _framework = framework;
        if (Klasse != 1 && framework is null)
            throw new InvalidOperationException($"{ziel.GetType().Name} ist ein Store der Klasse {Klasse} — er braucht den Fortschritt beim Framework.");
    }

    public IProjektionsZiel Ziel { get; }

    /// <summary>1, 2 oder 3.</summary>
    public int Klasse { get; }

    /// <summary>Wo der Fortschritt liegt: der Store (Klasse 1) oder das Framework.</summary>
    public IFortschrittsAblage Ablage => Ziel as IAtomaresZiel ?? (IFortschrittsAblage)_framework!;

    /// <summary>Die Klasse eines Stores; wirft, wenn er nicht genau eine implementiert (R14 zur Laufzeit, im Build CQRS109).</summary>
    public static int Klassenzahl(IProjektionsZiel ziel)
    {
        var klassen = new[] { ziel is IAtomaresZiel, ziel is IWiederholungsfestesZiel, ziel is INurMindestensEinmal };
        if (klassen.Count(k => k) != 1)
            throw new InvalidOperationException(
                $"{ziel.GetType().Name} implementiert {klassen.Count(k => k)} Store-Klassen — ein Store implementiert genau eine (IAtomaresZiel, "
                + "IWiederholungsfestesZiel oder INurMindestensEinmal, Plan §2.4, R14).");
        return Array.IndexOf(klassen, true) + 1;
    }

    public void Beginne(string generation) => Ziel.Beginne(generation);

    public void Stream(Guid stream) => Ziel.Stream(stream);

    /// <summary>Macht den gepufferten Batch gültig — mit <paramref name="fortschritt"/> (erwartete Revision) und den Guards. Liefert die neue Revision.</summary>
    public async Task<int> CommitAsync(PartitionsCheckpoint fortschritt, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct)
    {
        if (Ziel is IAtomaresZiel atomar) return await atomar.SchreibeMitFortschrittAsync(fortschritt, guards, ct);
        await Ziel.SchreibeAsync(ct);
        return await _framework!.SchreibeFortschrittAsync(fortschritt, guards, ct);
    }
}

/// <summary>Was der generierte Dispatch einer Projektion braucht (PullPathGenerator).</summary>
public static class Projektionslauf
{
    /// <summary>Die Herkunft eines Events für das <c>Auf</c> einer Projektion: Stream, Version, Log-Zeit, Akteur der Kette, Wirker.</summary>
    public static Herkunft Herkunft(EventEnvelope e, string wirker)
        => new(e.AggregateId, e.AggregateVersion, e.CreatedAtUtc, ImAuftrag.IstAkteur(e.UserId) ? e.UserId : null, wirker);

    /// <summary>
    /// Der Store einer Projektion aus einem Fähigkeits-Bereich: alle ihre Schreib-Fähigkeiten müssen DIESELBE Instanz liefern (ein Store, R9), sie
    /// muss genau eine Store-Klasse implementieren, und Klasse 3 nur mit ausdrücklicher Erlaubnis (R14 zur Laufzeit; im Build CQRS052, CQRS109, CQRS110).
    /// </summary>
    public static IProjektionsZiel Ziel(string projektion, bool mindestensEinmalErlaubt, params object[] faehigkeiten)
    {
        var instanzen = faehigkeiten.Distinct().ToList();
        if (instanzen.Count != 1 || instanzen[0] is not IProjektionsZiel ziel)
            throw new InvalidOperationException(
                $"{projektion}: die Schreib-Fähigkeiten müssen genau einen Store auflösen, der ein IProjektionsZiel ist (gefunden: "
                + string.Join(", ", instanzen.Select(i => i.GetType().Name)) + ") — docs/04 §4.2, CQRS052.");
        if (Projektionsnaht.Klassenzahl(ziel) == 3 && !mindestensEinmalErlaubt)
            throw new InvalidOperationException(
                $"{projektion} schreibt in {ziel.GetType().Name}, einen Store der Klasse 3 (nur mindestens einmal) — das braucht die ausdrückliche Erlaubnis "
                + "[MindestensEinmal] an der Projektion (R14, CQRS110).");
        return ziel;
    }
}
