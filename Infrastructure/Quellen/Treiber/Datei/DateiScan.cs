using Abstractions;
namespace Infrastructure.Quellen.Treiber.Datei;

/// <summary>Eine Datei, wie ein Scan sie sieht.</summary>
public sealed record DateiStand(string Pfad, string RelativerPfad, string Name, long Groesse, DateTimeOffset GeaendertAm, bool MarkerDa);

/// <summary>
/// Der store-freie Kern des Datei-Treibers (docs/04 §4.7): Kennung bilden, Fertig-Erkennung (Stabil/Marker),
/// Wartezone und die begrenzte „schon gemeldet"-Menge. Kein Dateisystem, keine Uhr, kein Log — alles kommt als Argument, damit der
/// Prüfstand ihn vollständig beweisen kann. Die Wahrheit, ob eine Datei schon einen Vorgang hat, bleibt der Log (Start-Abgleich im
/// Treiber); die Menge hier spart nur wiederholtes Melden innerhalb eines Laufs.
/// </summary>
public sealed class DateiScan
{
    private readonly DateiQuellKonfig _konfig;
    private readonly Dictionary<string, (long Groesse, DateTimeOffset Geaendert, DateTimeOffset Seit)> _wartezone = new(StringComparer.Ordinal);
    // RelativerPfad → Kennung, unter der die Datei gemeldet (oder beim Start als bekannt gefunden) wurde.
    private readonly Dictionary<string, string> _gemeldet = new(StringComparer.Ordinal);

    public DateiScan(DateiQuellKonfig konfig) => _konfig = konfig;

    /// <summary>Die Kennung einer Datei nach Konfig — sie bestimmt den Vorgang (<c>QuellVorgang.StreamFür</c>).</summary>
    public string Kennung(DateiStand d) => Bilde(_konfig.Kennung, d);

    public static string Bilde(DateiKennungArt art, DateiStand d) => art switch
    {
        DateiKennungArt.NameGroesse => $"{d.Name}|{d.Groesse}",
        _ => $"{d.RelativerPfad.Replace('\\', '/')}|{d.Groesse}|{d.GeaendertAm.ToUniversalTime():O}",
    };

    /// <summary>Ist die Datei eine Marker-Datei (selbst kein Kandidat)?</summary>
    public bool IstMarker(string name) =>
        _konfig.Fertig.Art == DateiFertigArt.Marker && name.EndsWith(_konfig.Fertig.Endung, StringComparison.OrdinalIgnoreCase);

    /// <summary>Beim Start: Dateien, deren Kennung schon einen Vorgang hat, gelten als gemeldet.</summary>
    public void SchonBekannt(DateiStand d) => _gemeldet[d.RelativerPfad] = Kennung(d);

    /// <summary>Nach dem Melden: diese Datei (unter dieser Kennung) nicht noch einmal melden.</summary>
    public void Gemeldet(DateiStand d)
    {
        _gemeldet[d.RelativerPfad] = Kennung(d);
        _wartezone.Remove(d.RelativerPfad);
    }

    /// <summary>
    /// Ein Scan: aus der aktuellen Sicht die Dateien, die jetzt fertig und noch nicht gemeldet sind (Reihenfolge: relativer Pfad).
    /// Verschwundene Dateien fallen aus Wartezone und Gedächtnis; eine ersetzte Datei mit neuer Kennung ist wieder ein Kandidat.
    /// </summary>
    public IReadOnlyList<DateiStand> Fertige(IReadOnlyList<DateiStand> sicht, DateTimeOffset jetzt)
    {
        var da = new HashSet<string>(sicht.Select(s => s.RelativerPfad), StringComparer.Ordinal);
        foreach (var weg in _gemeldet.Keys.Where(k => !da.Contains(k)).ToList()) _gemeldet.Remove(weg);
        foreach (var weg in _wartezone.Keys.Where(k => !da.Contains(k)).ToList()) _wartezone.Remove(weg);

        var fertig = new List<DateiStand>();
        foreach (var d in sicht.Where(s => !IstMarker(s.Name)).OrderBy(s => s.RelativerPfad, StringComparer.Ordinal))
        {
            if (_gemeldet.TryGetValue(d.RelativerPfad, out var k) && k == Kennung(d)) continue;
            if (_konfig.Fertig.Art == DateiFertigArt.Marker)
            {
                if (d.MarkerDa) fertig.Add(d);
                continue;
            }
            if (!_wartezone.TryGetValue(d.RelativerPfad, out var w) || w.Groesse != d.Groesse || w.Geaendert != d.GeaendertAm)
            {
                _wartezone[d.RelativerPfad] = (d.Groesse, d.GeaendertAm, jetzt);
                if (_konfig.Fertig.Fenster > TimeSpan.Zero) continue;
                w = _wartezone[d.RelativerPfad];
            }
            if (jetzt - w.Seit >= _konfig.Fertig.Fenster) fertig.Add(d);
        }
        return fertig;
    }
}
