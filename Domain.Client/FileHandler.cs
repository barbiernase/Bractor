// ── Zielverzeichnis: Domain.Client/ImagePair/Handlers/FileHandler.cs ──

using Client.Infrastructure.Abstractions;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Preload-Logik: Bilder für das ausgewählte Paar + nächste 2 Einträge laden.
///
/// Sync-Handler: IEnumerable Handle → läuft NACH allen Stores.
///
/// Liest: NavigationStore (AktuelleId, SelectedIndex), ListStore (FindById, Count, Indexer)
/// Hält intern: HashSet zum Deduplizieren (kein Store-State).
///
/// Yieldet: LoadFile (wird von FileBridge abgefangen)
/// </summary>
public partial class FileHandler
{
    private readonly NavigationStore _nav;
    private readonly ListStore _listStore;
    private readonly HashSet<string> _requestedPaths = new();

    public FileHandler(NavigationStore nav, ListStore listStore)
    {
        _nav = nav;
        _listStore = listStore;
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Paarauswahl → Preload
    // ═══════════════════════════════════════════════════

    IEnumerable<object> Handle(ImagePairAusgewaehlt evt, MessageContext ctx)
    {
        // Preload: aktuelles Paar + nächste 2 Einträge (3 × 2 = bis zu 6 Bilder)
        var startIndex = _nav.SelectedIndex;

        for (var offset = 0; offset <= 2; offset++)
        {
            var i = startIndex + offset;
            if (i >= _listStore.Count) break;

            var entry = _listStore[i];
            foreach (var loadFile in RequestFiles(entry))
                yield return loadFile;
        }
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — BildVerfuegbar → sofort laden wenn aktuelles Paar
    // ═══════════════════════════════════════════════════

    IEnumerable<object> Handle(BildVerfuegbar evt, MessageContext ctx)
    {
        // Nur laden wenn das Event zum aktuell angezeigten Paar gehört
        if (ctx.AggregateId != _nav.AktuelleId)
            yield break;

        if (TryRequest(evt.Pfad, out var loadFile))
            yield return loadFile;
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Neue Seite → Reset
    // ═══════════════════════════════════════════════════

    IEnumerable<object> Handle(ImagePairSuchergebnis result, MessageContext ctx)
    {
        // Neue Seite = neue Bilder, alte Pfade vergessen
        _requestedPaths.Clear();

        // Kein yield — das Preloading kommt über ImagePairAusgewaehlt
        // wenn NavigationStore den SelectedIndex setzt.
        yield break;
    }

    // ═══════════════════════════════════════════════════
    // INTERN
    // ═══════════════════════════════════════════════════

    private IEnumerable<LoadFile> RequestFiles(ImagePairRecord entry)
    {
        if (TryRequest(entry.Dc0Pfad, out var dc0))
            yield return dc0;
        if (TryRequest(entry.Dc2Pfad, out var dc2))
            yield return dc2;
    }

    private bool TryRequest(string? pfad, out LoadFile loadFile)
    {
        loadFile = default!;
        if (string.IsNullOrEmpty(pfad) || !_requestedPaths.Add(pfad))
            return false;
        loadFile = new LoadFile(pfad);
        return true;
    }
}
