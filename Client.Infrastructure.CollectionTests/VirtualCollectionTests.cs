// ── Client.Infrastructure.Tests/Collections/VirtualCollectionTests.cs ──
//
// xUnit. Bei NUnit/MSTest tauschen [Fact] → [Test]/[TestMethod] 1:1;
// Assert.* bleibt fast identisch. Braucht ein Testprojekt, das
// Client.Infrastructure referenziert und xunit + xunit.runner.visualstudio zieht.

using System.Linq;
using Client.Infrastructure.Collections;
using Xunit;

namespace Client.Infrastructure.Tests.Collections;

public class VirtualCollectionTests
{
    // ═══════════════════════════════════════════════════
    // Test-Typen + Helfer
    // ═══════════════════════════════════════════════════

    private sealed record TestDto(int Id, string Name);
    private sealed record TestItem(int Id, string Name);   // "Client-Model"
    private sealed record TestQuery(int Seite, int Groesse);

    private static VirtualCollection<TestItem, int> Neu()
        => new(keyOf: i => i.Id,
               baueQuery: (seite, groesse) => new TestQuery(seite, groesse));

    private static TestItem Map(TestDto d) => new(d.Id, d.Name);

    /// <summary>
    /// Chunk, dessen Item-Ids den ABSOLUTEN stabilen Index tragen (basis + i).
    /// Damit lässt sich this[index].Id direkt gegen den erwarteten Index prüfen.
    /// </summary>
    private static Chunk<TestDto> MacheChunk(int seite, int groesse, int gesamt, int? anzahl = null)
    {
        var n = anzahl ?? groesse;
        var basis = (seite - 1) * groesse;
        var items = Enumerable.Range(0, n)
            .Select(i => new TestDto(basis + i, $"item-{basis + i}"))
            .ToList();
        return new Chunk<TestDto>(seite, groesse, gesamt, items);
    }

    // ═══════════════════════════════════════════════════
    // Anfangszustand
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Anfangszustand_LeerUndNichtInitialisiert()
    {
        var c = Neu();
        Assert.False(c.IstInitialisiert);
        Assert.Equal(0, c.GesamtAnzahl);
        Assert.Equal(0, c.InsertOffset);
        Assert.Null(c[0]);
        Assert.False(c.ConsumeHasChanges());
    }

    // ═══════════════════════════════════════════════════
    // Absorb — Koordinatensystem & Zugriff
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Absorb_ErsterChunk_SetztKoordinatensystem()
    {
        var c = Neu();
        c.Absorb(MacheChunk(seite: 1, groesse: 50, gesamt: 1000), Map);

        Assert.True(c.IstInitialisiert);
        Assert.Equal(1000, c.GesamtAnzahl);
        Assert.Equal(50, c.ChunkGroesse);
        Assert.Equal(0, c[0]!.Id);
        Assert.Equal(49, c[49]!.Id);
        Assert.True(c.ConsumeHasChanges());
    }

    [Fact]
    public void Absorb_UngeladeneRegion_LiefertSkeleton()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);   // nur Seite 1

        Assert.Null(c[50]);    // Seite 2 — Loch
        Assert.Null(c[999]);   // Seite 20 — Loch
    }

    [Fact]
    public void Index_AusserhalbDerGrenzen_LiefertNull()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);

        Assert.Null(c[-1]);
        Assert.Null(c[1000]);   // == GesamtAnzahl
        Assert.Null(c[5000]);
    }

    [Fact]
    public void Absorb_MehrereChunks_LochBleibtSkeleton()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);
        c.Absorb(MacheChunk(3, 50, 1000), Map);   // Seite 2 bleibt Loch

        Assert.Equal(0,   c[0]!.Id);      // Seite 1
        Assert.Null(c[50]);               // Seite 2 — Loch
        Assert.Equal(100, c[100]!.Id);    // Seite 3, korrekte Basis (3-1)*50
        Assert.Equal(149, c[149]!.Id);
    }

    [Fact]
    public void Absorb_TeilweiseLetzteSeite_KorrekteGrenzen()
    {
        var c = Neu();
        c.Absorb(MacheChunk(3, 50, gesamt: 125, anzahl: 25), Map);   // Seite 3: nur 25 Items

        Assert.Equal(125, c.GesamtAnzahl);
        Assert.Equal(100, c[100]!.Id);
        Assert.Equal(124, c[124]!.Id);    // letztes gültiges Item
        Assert.Null(c[125]);              // == GesamtAnzahl → out of bounds
    }

    [Fact]
    public void Absorb_SpaetererChunk_LaesstBasisUnangetastet()
    {
        // Der Server kann durch Inserts gewachsen sein; die Anzeige-Gesamtzahl
        // bleibt trotzdem an der Snapshot-Basis verankert (+ InsertOffset).
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, gesamt: 1000), Map);
        c.Absorb(MacheChunk(2, 50, gesamt: 1005), Map);   // Server meldet jetzt 1005

        Assert.Equal(1000, c.GesamtAnzahl);
    }

    // ═══════════════════════════════════════════════════
    // FehlendeSeiten
    // ═══════════════════════════════════════════════════

    [Fact]
    public void FehlendeSeiten_VorInit_Leer()
    {
        var c = Neu();
        Assert.Empty(c.FehlendeSeiten(0, 500));
    }

    [Fact]
    public void FehlendeSeiten_LiefertNurUngeladene()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);

        // Bereich 0..149 → Seiten 1,2,3; Seite 1 geladen.
        Assert.Equal(new[] { 2, 3 }, c.FehlendeSeiten(0, 149).ToArray());
    }

    [Fact]
    public void FehlendeSeiten_Angefordert_WirdUebersprungen()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);
        c.MarkiereAngefordert(2);

        Assert.Equal(new[] { 3 }, c.FehlendeSeiten(0, 149).ToArray());
    }

    [Fact]
    public void FehlendeSeiten_KlemmtAufGesamtAnzahl()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, gesamt: 125), Map);   // nur 3 Seiten existieren

        // Riesiger Bereich darf nur bis Seite 3 fordern, nicht Seite 4..N.
        Assert.Equal(new[] { 2, 3 }, c.FehlendeSeiten(0, 10_000).ToArray());
    }

    [Fact]
    public void FehlendeSeiten_Seitengrenzen()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);

        // 45..55 überspannt Seite 1 (geladen) und Seite 2.
        Assert.Equal(new[] { 2 }, c.FehlendeSeiten(45, 55).ToArray());
    }

    [Fact]
    public void BaueQuery_NutztChunkGroesse()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, groesse: 40, gesamt: 1000), Map);

        var q = Assert.IsType<TestQuery>(c.BaueQuery(7));
        Assert.Equal(7, q.Seite);
        Assert.Equal(40, q.Groesse);
    }

    // ═══════════════════════════════════════════════════
    // Live-Einfügung — Offset-Arithmetik
    // ═══════════════════════════════════════════════════

    [Fact]
    public void FuegeVorneEin_ErscheintObenUndVerschiebtAnzeige()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 5, gesamt: 10), Map);   // Ids 0..4
        Assert.True(c.ConsumeHasChanges());

        c.FuegeVorneEin(new TestItem(-100, "neu"));

        Assert.Equal(1, c.InsertOffset);
        Assert.Equal(11, c.GesamtAnzahl);
        Assert.Equal(-100, c[0]!.Id);   // Neuzugang oben
        Assert.Equal(0,    c[1]!.Id);   // altes Snapshot-Item, um 1 verschoben
        Assert.True(c.ConsumeHasChanges());
    }

    [Fact]
    public void FuegeVorneEin_Mehrfach_NeuesteOben()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 5, gesamt: 10), Map);
        c.FuegeVorneEin(new TestItem(-100, "A"));   // zuerst
        c.FuegeVorneEin(new TestItem(-200, "B"));   // danach → oben

        Assert.Equal(2, c.InsertOffset);
        Assert.Equal(-200, c[0]!.Id);   // B (neuester) oben
        Assert.Equal(-100, c[1]!.Id);   // A darunter
        Assert.Equal(0,    c[2]!.Id);   // Snapshot beginnt
    }

    [Fact]
    public void FuegeVorneEin_LaesstStabilenIndexFix()
    {
        // Die Kern-Invariante: ein Snapshot-Item bleibt per Key auffindbar und
        // "wandert" nur in der Anzeige — analog zum Cursor, der auf dem stabilen
        // Index sitzt und ohne Zutun fix bleibt.
        var c = Neu();
        c.Absorb(MacheChunk(1, 10, gesamt: 100), Map);   // Ids 0..9

        Assert.Equal(5, c[5]!.Id);          // vor Inserts: Anzeige == stabil
        Assert.Equal(5, c.Get(5)!.Id);

        c.FuegeVorneEin(new TestItem(-1, "x"));
        c.FuegeVorneEin(new TestItem(-2, "y"));
        c.FuegeVorneEin(new TestItem(-3, "z"));

        Assert.Equal(5, c.Get(5)!.Id);      // per Key: unverändert
        Assert.Equal(5, c[8]!.Id);          // Anzeige um InsertOffset(3) verschoben
    }

    [Fact]
    public void FuegeVorneEin_LoestKeineNachladungAus()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);
        c.FuegeVorneEin(new TestItem(-1, "neu"));

        // Anzeige-Bereich der Einfügung (Index 0) darf keine Server-Seite fordern.
        Assert.Empty(c.FehlendeSeiten(0, 0));
    }

    // ═══════════════════════════════════════════════════
    // Patch — In-Place, verpufft bei ungeladen
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Patch_GeladenesItem_Mutiert()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);

        c.Patch(2, i => i with { Name = "GEPATCHT" });

        Assert.Equal("GEPATCHT", c[2]!.Name);
        Assert.True(c.ConsumeHasChanges());
    }

    [Fact]
    public void Patch_UngeladenesItem_Verpufft()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);   // nur Seite 1 geladen
        Assert.True(c.ConsumeHasChanges());
        var versionVor = c.Version;

        c.Patch(500, i => i with { Name = "sollte-verpuffen" });

        Assert.Null(c[500]);                       // weiterhin Skeleton
        Assert.Null(c.Get(500));
        Assert.Equal(versionVor, c.Version);       // keine Version, kein Signal
        Assert.False(c.ConsumeHasChanges());
    }

    [Fact]
    public void Patch_LiveEingefuegtesItem_Mutiert()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);
        c.FuegeVorneEin(new TestItem(-100, "a"));

        c.Patch(-100, i => i with { Name = "b" });

        Assert.Equal("b", c.Get(-100)!.Name);
        Assert.Equal("b", c[0]!.Name);
    }

    // ═══════════════════════════════════════════════════
    // Get / Freigabe
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Get_UnbekannterKey_Null()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);
        Assert.Null(c.Get(9999));
    }

    [Fact]
    public void GibFernFrei_VerwirftFerneChunks()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);
        c.Absorb(MacheChunk(2, 50, 1000), Map);
        c.Absorb(MacheChunk(10, 50, 1000), Map);

        c.GibFernFrei(aktuelleSeite: 1, schwelle: 2);   // |10-1| = 9 > 2 → Seite 10 raus

        Assert.Equal(0,  c[0]!.Id);     // Seite 1 bleibt
        Assert.Equal(50, c[50]!.Id);    // Seite 2 bleibt
        Assert.Null(c[450]);            // Seite 10 → wieder Skeleton
        Assert.Null(c.Get(450));        // Key aus dem Index entfernt → Patch verpufft wieder
    }

    [Fact]
    public void GibFernFrei_NichtsZuTun_KeineAenderung()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);
        c.Absorb(MacheChunk(2, 50, 1000), Map);
        Assert.True(c.ConsumeHasChanges());

        c.GibFernFrei(aktuelleSeite: 1, schwelle: 100);   // nichts weit genug weg

        Assert.False(c.ConsumeHasChanges());
    }

    // ═══════════════════════════════════════════════════
    // Reset
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Reset_LeertAlles()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);
        c.FuegeVorneEin(new TestItem(-1, "x"));
        Assert.True(c.ConsumeHasChanges());

        c.Reset();

        Assert.False(c.IstInitialisiert);
        Assert.Equal(0, c.GesamtAnzahl);
        Assert.Equal(0, c.InsertOffset);
        Assert.Null(c[0]);
        Assert.True(c.ConsumeHasChanges());   // Reset ist ein Change
    }

    // ═══════════════════════════════════════════════════
    // Tracking-Signal
    // ═══════════════════════════════════════════════════

    [Fact]
    public void ConsumeHasChanges_NachMutation_TrueDannFalse()
    {
        var c = Neu();
        c.Absorb(MacheChunk(1, 50, 1000), Map);

        Assert.True(c.ConsumeHasChanges());    // konsumiert + resettet
        Assert.False(c.ConsumeHasChanges());   // nichts Neues
    }
}
