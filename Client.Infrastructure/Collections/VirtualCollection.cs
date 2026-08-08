// ── Client.Infrastructure/Collections/VirtualCollection.cs ──

using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Infrastructure.Collections;

/// <summary>
/// Gleitendes Fenster über eine virtuelle Gesamtliste der Länge GesamtAnzahl.
/// Ersetzt das Seitenmodell (PagedCollection) für virtualisierte Listen —
/// gemeinsame Bausteine (Key-Index, IVersioned/ITrackedCollection, Patch),
/// aber anderes Speichermodell: Chunks mit Löchern statt "eine Seite".
///
/// Rein datenhaltend: kennt weder Bus, Pixel noch scrollTop. Sie liefert nur
/// Wissen und baut Query-Nachrichten; dispatcht wird vom Treiber.
///
/// ZWEI KOORDINATENSYSTEME (analog Basisadresse + Basisregister):
///   stabiler Index  — bei Absorb/Insert vergeben, ändert sich NIE.
///                     Snapshot belegt 0 … GesamtBasis-1, Live-Inserts -1, -2, …
///   Anzeige-Index   — was Scrollbalken/Spacer brauchen:
///                     anzeigeIndex = stabilerIndex + InsertOffset
/// Chunks und Cursor leben ausschließlich auf stabilen Indizes; deshalb bleibt
/// der Cursor bei Live-Zuwachs oben ohne Zutun fix — nur InsertOffset wächst.
///
/// Wichtig (Track-Fallstrick): implementiert ITrackedCollection. Ohne Anmeldung
/// via StoreBase.Track(...) im Store-Konstruktor feuert Store.Changed nicht.
/// </summary>
public sealed class VirtualCollection<TItem, TKey> : IVirtualWindow<TItem>, ITrackedCollection
    where TItem : class
    where TKey : notnull
{
    private readonly Func<TItem, TKey> _keyOf;
    private readonly Func<int, int, object> _baueQuery;   // (seite, groesse) → Query-Message

    // Snapshot-Chunks nach 1-basierter Seitennummer.
    private readonly Dictionary<int, TItem[]> _chunks = new();
    // In-flight: angefordert, noch nicht eingetroffen.
    private readonly HashSet<int> _angefordert = new();
    // Oben live eingefügte Zeilen. _prepends[0] = stabiler Index -1, [1] = -2, …
    private readonly List<TItem> _prepends = new();
    // Key → stabiler Index (>=0 Snapshot, <0 Prepend). Für Get/Patch.
    private readonly Dictionary<TKey, int> _index = new();

    private int _gesamtBasis;     // GesamtAnzahl des Snapshots (erster Chunk), danach fix.
    private bool _initialisiert;
    private bool _hasChanges;

    public VirtualCollection(Func<TItem, TKey> keyOf, Func<int, int, object> baueQuery)
    {
        _keyOf = keyOf;
        _baueQuery = baueQuery;
    }

    // ═══════════════════════════════════════════════════
    // IVersioned / IVirtualWindow — Read-Seite (Komponente)
    // ═══════════════════════════════════════════════════

    public int Version { get; private set; }
    public int ChunkGroesse { get; private set; } = 50;
    public int InsertOffset => _prepends.Count;

    /// <summary>Anzeige-Gesamtzahl = Snapshot-Basis + Live-Einfügungen.</summary>
    public int GesamtAnzahl => _gesamtBasis + InsertOffset;

    /// <summary>True sobald der erste Chunk da ist — der Store setzt daraufhin MarkHydrated().</summary>
    public bool IstInitialisiert => _initialisiert;

    public TItem? this[int anzeigeIndex]
    {
        get
        {
            if (anzeigeIndex < 0 || anzeigeIndex >= GesamtAnzahl) return null;

            var stabil = anzeigeIndex - InsertOffset;

            // Live-Einfügung (stabiler Index negativ).
            if (stabil < 0)
            {
                var pos = -stabil - 1;                       // -1 → 0, -2 → 1
                return pos < _prepends.Count ? _prepends[pos] : null;
            }

            // Snapshot-Bereich: Seite + Offset-in-Seite.
            var seite = stabil / ChunkGroesse + 1;
            var offsetInSeite = stabil % ChunkGroesse;
            return _chunks.TryGetValue(seite, out var arr) && offsetInSeite < arr.Length
                ? arr[offsetInSeite]
                : null;                                      // Skeleton
        }
    }

    public IEnumerable<int> FehlendeSeiten(int vonIndex, int bisIndex)
    {
        if (!_initialisiert || _gesamtBasis == 0) yield break;

        // Anzeige- → stabiler Snapshot-Index. Negative (Prepends) nie nachladen.
        var vonStabil = Math.Max(0, vonIndex - InsertOffset);
        var bisStabil = Math.Min(_gesamtBasis - 1, bisIndex - InsertOffset);
        if (bisStabil < 0 || vonStabil > bisStabil) yield break;

        var vonSeite = vonStabil / ChunkGroesse + 1;
        var bisSeite = bisStabil / ChunkGroesse + 1;

        for (var seite = vonSeite; seite <= bisSeite; seite++)
            if (!_chunks.ContainsKey(seite) && !_angefordert.Contains(seite))
                yield return seite;
    }

    public void MarkiereAngefordert(int seite) => _angefordert.Add(seite);

    public object BaueQuery(int seite) => _baueQuery(seite, ChunkGroesse);

    // ═══════════════════════════════════════════════════
    // Inbound: Chunk absorbieren (Bulk)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Packt einen eintreffenden Chunk aus. Der erste Chunk legt GesamtBasis und
    /// ChunkGroesse fest — spätere Chunks lassen die Basis unangetastet, damit das
    /// Koordinatensystem stabil bleibt (Server-GesamtAnzahl kann durch Inserts
    /// gewachsen sein; der Zuwachs läuft ausschließlich über InsertOffset).
    /// </summary>
    public void Absorb<TDto>(Chunk<TDto> chunk, Func<TDto, TItem> map)
    {
        if (!_initialisiert)
        {
            _gesamtBasis = chunk.GesamtAnzahl;   // Snapshot legt das Koordinatensystem fest.
            ChunkGroesse = chunk.SeitenGroesse;
            _initialisiert = true;
        }

        var arr = new TItem[chunk.Items.Count];
        var basis = (chunk.Seite - 1) * ChunkGroesse;
        for (var i = 0; i < arr.Length; i++)
        {
            var item = map(chunk.Items[i]);
            arr[i] = item;
            _index[_keyOf(item)] = basis + i;
        }

        _chunks[chunk.Seite] = arr;
        _angefordert.Remove(chunk.Seite);
        Bump();
    }

    // ═══════════════════════════════════════════════════
    // Inbound: Live-Einfügung oben (O(1))
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Neuzugang oben. Bekommt einen negativen stabilen Index; InsertOffset
    /// wächst um 1, GesamtAnzahl damit ebenfalls. Keine Umnummerierung, kein
    /// Kopieren — der Cursor auf einem stabilen Index bleibt fix.
    /// </summary>
    public void FuegeVorneEin(TItem item)
    {
        _prepends.Add(item);
        _index[_keyOf(item)] = -_prepends.Count;   // erster Insert → -1, zweiter → -2, …
        Bump();
    }

    // ═══════════════════════════════════════════════════
    // In-Place-Update per Key (verpufft folgenlos bei ungeladen)
    // ═══════════════════════════════════════════════════

    public void Patch(TKey key, Func<TItem, TItem> mutate)
    {
        if (!_index.TryGetValue(key, out var stabil)) return;   // ungeladen → verpufft

        if (stabil < 0)
        {
            var pos = -stabil - 1;
            if (pos >= _prepends.Count) return;
            _prepends[pos] = mutate(_prepends[pos]);
            Bump();
            return;
        }

        var seite = stabil / ChunkGroesse + 1;
        var offset = stabil % ChunkGroesse;
        if (!_chunks.TryGetValue(seite, out var arr) || offset >= arr.Length) return;
        arr[offset] = mutate(arr[offset]);
        Bump();
    }

    public TItem? Get(TKey key)
        => _index.TryGetValue(key, out var stabil) ? this[stabil + InsertOffset] : null;

    /// <summary>Stabiler Index eines Keys, oder null wenn (noch) nicht geladen.</summary>
    public int? StabilerIndexVon(TKey key)
        => _index.TryGetValue(key, out var stabil) ? stabil : null;

    /// <summary>
    /// Alle aktuell geladenen Items (Chunk-Inhalte + Live-Prepends). Für Scans, die
    /// ein Item über einen Nicht-Key-Wert finden müssen (z.B. Dateipfad). Begrenzt
    /// auf das geladene Fenster, NICHT auf GesamtAnzahl.
    /// </summary>
    public IEnumerable<TItem> AlleGeladenen()
    {
        foreach (var arr in _chunks.Values)
            foreach (var item in arr)
                yield return item;
        foreach (var item in _prepends)
            yield return item;
    }

    // ═══════════════════════════════════════════════════
    // Freigabe weit entfernter Chunks (Speicher)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Verwirft Chunks, die weiter als 'schwelle' Seiten von der aktuellen
    /// entfernt sind. Deren Keys fallen aus dem Index — Get/Patch verpuffen
    /// danach wieder (korrekt: beim nächsten Laden kommen sie frisch).
    /// </summary>
    public void GibFernFrei(int aktuelleSeite, int schwelle)
    {
        var raus = _chunks.Keys.Where(s => Math.Abs(s - aktuelleSeite) > schwelle).ToList();
        if (raus.Count == 0) return;

        foreach (var seite in raus)
            if (_chunks.Remove(seite, out var arr))
                foreach (var item in arr)
                    _index.Remove(_keyOf(item));

        Bump();
    }

    // ═══════════════════════════════════════════════════
    // Reset (Filter-/Kontextwechsel)
    // ═══════════════════════════════════════════════════

    public void Reset()
    {
        _chunks.Clear();
        _angefordert.Clear();
        _prepends.Clear();
        _index.Clear();
        _gesamtBasis = 0;
        _initialisiert = false;
        Bump();
    }

    // ═══════════════════════════════════════════════════
    // ITrackedCollection
    // ═══════════════════════════════════════════════════

    public bool ConsumeHasChanges()
    {
        var had = _hasChanges;
        _hasChanges = false;
        return had;
    }

    private void Bump()
    {
        Version++;
        _hasChanges = true;
    }
}
