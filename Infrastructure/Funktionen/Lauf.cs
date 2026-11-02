using Abstractions;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DER LAUF (docs/04 §4.4, Entscheidungen 5, 7, 11): jede Nachricht im Graphen gehört zu einem Lauf, den eine Kopf-Nachricht startet; ein Strom öffnet
// je Element einen Unterlauf (Pfad + Nummer). Die Buchführung (wer wartet auf wen) liegt im Speicher des Graph-Actors der Instanz, haltbar ist allein
// der Kopf. Je Operation eines Knotens und Pfad ein AUFRUF: er wartet, bis jeder Port aufgelöst ist — T genau einer, T? höchstens einer (null, wenn
// feststeht, dass keiner kommt, oder nach dem Zeitlimit), OneOf der erste, IReadOnlyList alle aus den Unterläufen eine Ebene tiefer —, dann rechnet
// ihn ein Kind; danach ist er fertig (seine Ausgaben gehen längs der Kanten weiter), oder er kommt nie (ein Port bleibt leer). Ein Lauf ist fertig,
// wenn jeder Aufruf fertig ist oder nie kommt; er scheitert, wenn ein Knoten auch nach seinen Wiederholungen wirft.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Was ein Lauf vom Kopf mitbringt (für Herkunft, Ids und Spur): seine Kennung (die des Kopfs) und die Herkunft der Kette.</summary>
public sealed record LaufKontext(Guid Lauf, Guid Stream, int Version, DateTimeOffset Zeit, string? Akteur, string? Korrelation, string? Ursache)
{
    /// <summary>Die Herkunft, die das <c>Auf</c> der Funktion <paramref name="funktion"/> sieht (der Wirker ist die Funktion).</summary>
    public Herkunft Herkunft(string funktion) => new(Stream, Version, Zeit, ImAuftrag.IstAkteur(Akteur) ? Akteur : null, WirkerId.FuerFunktion(funktion));
}

/// <summary>Ein Aufruf zum Rechnen (Graph-Actor → Parent → Kind): Kennung, Pfad, Knoten, Operation (Nummer des <c>Auf</c>), die Ports als Objekte.</summary>
public sealed record KnotenAufruf(Guid Kennung, string Pfad, string Knoten, int Operation, object?[] Argumente, LaufKontext Kontext);

/// <summary>Eine Ausgabe eines Aufrufs an einen Knoten desselben Graphen: ihre Stelle (Ids, Unterlauf) und die Nachricht.</summary>
public sealed record KnotenAusgabe(int Stelle, object Nachricht);

/// <summary>
/// Das Ergebnis eines Aufrufs (Parent → Graph-Actor): seine Ausgaben an Knoten des Graphen (erst mit dem Erfolg sichtbar), der Fehler nach allen
/// Wiederholungen oder „abgebrochen“ (der Lauf brauchte ihn nicht mehr).
/// </summary>
/// <param name="Zustand">Der Schnappschuss der Zustände des Knotens nach dem Aufruf (null ohne Zustand) — gesichert, sobald der Lauf fertig ist.</param>
public sealed record KnotenErgebnis(KnotenAufruf Aufruf, IReadOnlyList<KnotenAusgabe> Ausgaben, string? Fehler, bool Abgebrochen = false, byte[]? Zustand = null);

/// <summary>An den Graph-Actor einer Quellinstanz: dieser Cluster-Knoten hält sie (die Quelle lauscht hier) bzw. nicht mehr.</summary>
public sealed record QuelleHalten(bool Halten);

/// <summary>An den Parent des Quellknotens: lauschen bzw. aufhören.</summary>
public sealed record Lausche(bool An);

/// <summary>
/// Die Quelle meldet ein Element (Kind des Quellknotens → Graph-Actor): seine laufende Nummer, der Schnappschuss ihres Zustands danach und die Zusage,
/// die der Graph-Actor erfüllt, sobald er den Lauf annimmt (Gegendruck: die Quelle meldet das nächste erst danach).
/// </summary>
public sealed record QuellMeldung(object Nachricht, long Nummer, byte[] Zustand, TaskCompletionSource Angenommen);

/// <summary>An einen Parent: die Aufrufe dieses Laufs (null: alle) werden nicht mehr gebraucht — wartende fallen weg, laufende bekommen ihren Abbruch.</summary>
public sealed record LaufAbbrechen(Guid Lauf, IReadOnlyCollection<Guid>? Aufrufe);

/// <summary>An den Graph-Actor: das Zeitlimit eines wartenden Aufrufs mit <c>T?</c>-Ports ist um.</summary>
public sealed record ZeitlimitAbgelaufen(Guid Lauf, string Pfad, string Knoten, int Operation);

/// <summary>Was ein Lauf nach draußen bewirkt (der Graph-Actor setzt es um).</summary>
public interface ILaufWirkung
{
    /// <summary>Ein Aufruf ist bereit: an den Parent seines Knotens.</summary>
    void Rechne(KnotenAufruf aufruf);

    /// <summary>Laufende Aufrufe eines Knotens werden nicht mehr gebraucht.</summary>
    void Breche(string knoten, Guid lauf, IReadOnlyCollection<Guid> aufrufe);

    /// <summary>Wecke nach <paramref name="zeitlimit"/> mit <paramref name="wecker"/>.</summary>
    void Warte(TimeSpan zeitlimit, ZeitlimitAbgelaufen wecker);
}

/// <summary>Wo ein Aufruf steht.</summary>
public enum Aufrufzustand { Wartet, Laeuft, Fertig, Nichts }

/// <summary>
/// Die Buchführung EINES Laufs (ohne Actor, ohne Nebenläufigkeit: der Graph-Actor ruft sie in seiner Mailbox). Pfade (der Kopf <c>""</c>, ein
/// Unterlauf <c>Knoten#Op.Stelle</c>, geschachtelt mit <c>/</c>), je Pfad die Aufrufe, je Aufruf die Werte seiner Ports.
/// </summary>
public sealed class Lauf(GraphInstanz instanz, LaufKontext kontext)
{
    private sealed class Pfad(string name, Pfad? eltern, int index, string? ursprung)
    {
        public string Name { get; } = name;
        public Pfad? Eltern { get; } = eltern;
        /// <summary>Die Stelle unter den Unterläufen des Eltern (Reihenfolge einer gesammelten Liste).</summary>
        public int Index { get; } = index;
        /// <summary>Der Knoten, dessen Strom den Unterlauf öffnete (null am Kopf).</summary>
        public string? Ursprung { get; } = ursprung;
        public Dictionary<(string, int), Aufruf> Aufrufe { get; } = new();
        public List<Pfad> Unterlaeufe { get; } = new();
    }

    private sealed class Aufruf(KnotenPlan knoten, OperationPlan op, Pfad pfad, Guid kennung)
    {
        public KnotenPlan Knoten { get; } = knoten;
        public OperationPlan Op { get; } = op;
        public Pfad Pfad { get; } = pfad;
        public Guid Kennung { get; } = kennung;
        public Aufrufzustand Zustand = Aufrufzustand.Wartet;
        public readonly object?[] Werte = new object?[op.Ports.Count];
        public readonly bool[] Da = new bool[op.Ports.Count];
        public readonly List<(int Index, int Folge, object Wert)>[] Listen = op.Ports.Select(_ => new List<(int, int, object)>()).ToArray();
        public bool Zeitlimit, WeckerGestellt;
    }

    private readonly GraphPlan _graph = instanz.Graph;
    private readonly Pfad _kopf = new("", null, 0, null);
    private readonly Dictionary<string, Pfad> _pfade = new(StringComparer.Ordinal);
    private object _kopfNachricht = null!;
    private int _offen, _folge;

    /// <summary>Die Kennung des Laufs (die des Kopfs).</summary>
    public Guid Kennung => kontext.Lauf;

    /// <summary>Jeder Aufruf ist fertig oder kommt nie — und der Lauf ist nicht gescheitert.</summary>
    public bool Fertig => _offen == 0 && Gescheitert is null;

    /// <summary>Der Knoten und der Grund, wenn der Lauf gescheitert ist; sonst null.</summary>
    public (string Knoten, string Grund)? Gescheitert { get; private set; }

    /// <summary>Startet den Lauf mit der Kopf-Nachricht: jede Operation, die ihren Typ am Kopf nimmt, wird geprüft.</summary>
    public void Beginne(object nachricht, ILaufWirkung w)
    {
        _kopfNachricht = nachricht;
        _pfade[""] = _kopf;
        var typ = nachricht.GetType();
        foreach (var k in instanz.KopfFuer(typ).Select(_graph.KnotenNamens).OfType<KnotenPlan>())
            foreach (var op in k.Operationen.Where(o => o.Ports.Any(p => p.Erzeuger.Length == 0 && p.Typen.Contains(typ))))
                Pruefe(Hole(_kopf, k, op), w);
    }

    /// <summary>
    /// Startet den Lauf einer Quelle: der Quellknoten gilt am Kopf als fertiger Aufruf, der <paramref name="nachricht"/> ausgab — sie geht an seine Abnehmer.
    /// </summary>
    public void BeginneMitQuelle(string knoten, int operation, object nachricht, ILaufWirkung w)
    {
        _kopfNachricht = nachricht;
        _pfade[""] = _kopf;
        var a = Hole(_kopf, knoten, operation);
        a.Zustand = Aufrufzustand.Laeuft;
        var beruehrt = new List<Aufruf>();
        Liefere(a, new KnotenAusgabe(0, nachricht), beruehrt, w);
        Ende(a, Aufrufzustand.Fertig, w);
        foreach (var b in beruehrt) Pruefe(b, w);
    }

    /// <summary>Das Ergebnis eines Aufrufs: seine Ausgaben gehen an die Abnehmer (ein Strom öffnet je Element einen Unterlauf), dann ist er fertig.</summary>
    public void Ergebnis(KnotenErgebnis r, ILaufWirkung w)
    {
        if (Gescheitert is not null || r.Abgebrochen || !_pfade.TryGetValue(r.Aufruf.Pfad, out var pfad)
            || !pfad.Aufrufe.TryGetValue((r.Aufruf.Knoten, r.Aufruf.Operation), out var a) || a.Zustand != Aufrufzustand.Laeuft) return;
        if (r.Fehler is not null) { Scheitere(a.Knoten.Name, r.Fehler, w); return; }
        var beruehrt = new List<Aufruf>();
        foreach (var aus in r.Ausgaben) Liefere(a, aus, beruehrt, w);
        Ende(a, Aufrufzustand.Fertig, w);
        foreach (var b in beruehrt) Pruefe(b, w);
    }

    /// <summary>Das Zeitlimit eines wartenden Aufrufs ist um: seine offenen <c>T?</c>-Ports werden null.</summary>
    public void Zeitlimit(ZeitlimitAbgelaufen z, ILaufWirkung w)
    {
        if (Gescheitert is not null || !_pfade.TryGetValue(z.Pfad, out var pfad) || !pfad.Aufrufe.TryGetValue((z.Knoten, z.Operation), out var a)) return;
        a.Zeitlimit = true;
        Pruefe(a, w);
    }

    /// <summary>Der Lauf scheitert: laufende Aufrufe bekommen ihren Abbruch, wartende kommen nie.</summary>
    public void Scheitere(string knoten, string grund, ILaufWirkung w)
    {
        if (Gescheitert is not null) return;
        Gescheitert = (knoten, grund);
        var laufend = _pfade.Values.SelectMany(p => p.Aufrufe.Values).Where(x => x.Zustand == Aufrufzustand.Laeuft).ToList();
        foreach (var g in laufend.GroupBy(x => x.Knoten.Name)) w.Breche(g.Key, Kennung, g.Select(x => x.Kennung).ToList());
        foreach (var x in _pfade.Values.SelectMany(p => p.Aufrufe.Values).Where(x => x.Zustand is Aufrufzustand.Wartet or Aufrufzustand.Laeuft))
            x.Zustand = Aufrufzustand.Nichts;
        _offen = 0;
    }

    private Aufruf Hole(Pfad pfad, KnotenPlan k, OperationPlan op)
    {
        if (pfad.Aufrufe.TryGetValue((k.Name, op.Auf), out var a)) return a;
        a = new Aufruf(k, op, pfad, Kettenvertrag.Aufruf(Kennung, instanz.Ort(k.Name), pfad.Name, op.Auf));
        pfad.Aufrufe[(k.Name, op.Auf)] = a;
        _offen++;
        return a;
    }

    private Aufruf Hole(Pfad pfad, string knoten, int op)
    {
        var k = _graph.KnotenNamens(knoten)!;
        return Hole(pfad, k, k.Operation(op)!);
    }

    private static Aufruf? Finde(Pfad pfad, string knoten, int op) => pfad.Aufrufe.TryGetValue((knoten, op), out var a) ? a : null;

    private static bool Offen(Aufruf? a) => a is null || a.Zustand is Aufrufzustand.Wartet or Aufrufzustand.Laeuft;

    /// <summary>Eine Ausgabe an ihre Abnehmer: ein Strom öffnet einen Unterlauf; ein Sammel-Port bekommt sie eine Ebene höher, jeder andere am Pfad (der erste Wert gilt).</summary>
    private void Liefere(Aufruf von, KnotenAusgabe aus, List<Aufruf> beruehrt, ILaufWirkung w)
    {
        var typ = aus.Nachricht.GetType();
        var ziel = von.Pfad;
        if (von.Op.Stroeme.Contains(typ))
        {
            var name = (ziel.Name.Length == 0 ? "" : ziel.Name + "/") + $"{von.Knoten.Name}#{von.Op.Auf}.{aus.Stelle}";
            if (!_pfade.TryGetValue(name, out var unter))
            {
                unter = new Pfad(name, ziel, ziel.Unterlaeufe.Count, von.Knoten.Name);
                ziel.Unterlaeufe.Add(unter);
                _pfade[name] = unter;
            }
            ziel = unter;
        }
        foreach (var (k, o, i) in _graph.Abnehmer(von.Knoten.Name, von.Op.Auf, typ))
        {
            var port = _graph.KnotenNamens(k)!.Operation(o)!.Ports[i];
            if (port.Form == Portform.Alle)
            {
                if (ziel.Eltern is not { } oben) continue;
                var c = Hole(oben, k, o);
                c.Listen[i].Add((ziel.Index, _folge++, aus.Nachricht));
                beruehrt.Add(c);
            }
            else
            {
                var c = Hole(ziel, k, o);
                if (c.Da[i]) continue;                    // der erste Wert gilt (Zusammenfließen; im Rennen der Sieger)
                c.Da[i] = true;
                c.Werte[i] = aus.Nachricht;
                beruehrt.Add(c);
                if (port.Form == Portform.Erster) Rennen(c, i, w);
            }
        }
    }

    /// <summary>Ein Aufruf ist fertig oder kommt nie: seine Abnehmer erfahren, dass von ihm nichts mehr kommt.</summary>
    private void Ende(Aufruf a, Aufrufzustand zustand, ILaufWirkung w)
    {
        if (a.Zustand is Aufrufzustand.Fertig or Aufrufzustand.Nichts) return;
        a.Zustand = zustand;
        _offen--;
        foreach (var typ in a.Op.Ausgaben)
        {
            var strom = a.Op.Stroeme.Contains(typ);
            foreach (var (k, o, i) in _graph.Abnehmer(a.Knoten.Name, a.Op.Auf, typ))
            {
                var port = _graph.KnotenNamens(k)!.Operation(o)!.Ports[i];
                var pfad = port.Form == Portform.Alle ? (strom ? a.Pfad : a.Pfad.Eltern) : strom ? null : a.Pfad;
                if (pfad is not null) Pruefe(Hole(pfad, k, o), w);
            }
        }
        // Ein Strom-Ursprung ist fertig: jeder Sammel-Port, der aus seinen Unterläufen sammelt, erfährt es — auch wenn er nicht unmittelbar an ihm
        //   hängt (ein leerer Strom öffnet keinen Unterlauf, aus dem es ihn sonst erreichte).
        if (a.Op.Stroeme.Length > 0)
            foreach (var (k, o, _) in _graph.Sammler(a.Knoten.Name))
                Pruefe(Hole(a.Pfad, k, o), w);
    }

    /// <summary>Prüft einen wartenden Aufruf: sind alle Ports aufgelöst, rechnet er; bleibt ein Port leer, kommt er nie.</summary>
    private void Pruefe(Aufruf a, ILaufWirkung w)
    {
        if (a.Zustand != Aufrufzustand.Wartet || Gescheitert is not null) return;
        bool nie = false, warten = false, inhalt = false, nurOptional = true;
        var verfallen = new List<int>();
        for (var i = 0; i < a.Op.Ports.Count; i++)
        {
            var p = a.Op.Ports[i];
            if (p.Erzeuger.Length == 0 && !a.Da[i] && a.Pfad == _kopf && p.Typen.Contains(_kopfNachricht.GetType()))
            {
                a.Da[i] = true;
                a.Werte[i] = _kopfNachricht;
            }
            if (p.Form == Portform.Alle)
            {
                switch (Liste(a, i, out var liste))
                {
                    case Aufrufzustand.Fertig: a.Werte[i] = liste; inhalt = true; break;
                    case Aufrufzustand.Nichts: nie = true; break;
                    default: warten = true; nurOptional = false; break;
                }
            }
            else if (a.Da[i]) inhalt |= a.Werte[i] is not null;
            else if (ErzeugerFertig(a.Pfad, p))
            {
                if (p.Form == Portform.Hoechstens) a.Da[i] = true;   // null: es kommt keiner
                else nie = true;
            }
            else if (p.Form == Portform.Hoechstens && a.Zeitlimit)
            {
                a.Da[i] = true;                                       // null: die Frist ist um
                verfallen.Add(i);
            }
            else
            {
                warten = true;
                if (p.Form != Portform.Hoechstens) nurOptional = false;
            }
        }
        if (nie || !warten && !inhalt) { Ende(a, Aufrufzustand.Nichts, w); return; }
        if (warten)
        {
            var limit = instanz.Konfig(a.Knoten.Name).Zeitlimit;
            if (nurOptional && !a.WeckerGestellt && limit > TimeSpan.Zero)
            {
                a.WeckerGestellt = true;
                w.Warte(limit, new ZeitlimitAbgelaufen(Kennung, a.Pfad.Name, a.Knoten.Name, a.Op.Auf));
            }
            return;
        }
        a.Zustand = Aufrufzustand.Laeuft;
        w.Rechne(new KnotenAufruf(a.Kennung, a.Pfad.Name, a.Knoten.Name, a.Op.Auf, a.Werte.ToArray(), kontext));
        // Wer nach der Frist noch rechnet, wird nicht mehr gebraucht (wie der Verlierer eines Rennens).
        foreach (var e in verfallen.SelectMany(i => a.Op.Ports[i].Erzeuger).Where(e => !e.Strom))
            if (Finde(a.Pfad, e.Knoten, e.Operation) is { } x) Unnoetig(x, w);
    }

    /// <summary>Kommt von den Erzeugern dieses Ports am Pfad nichts mehr? (Ein Strom liefert in einen Unterlauf nur beim Öffnen.)</summary>
    private static bool ErzeugerFertig(Pfad pfad, PortPlan p) =>
        p.Erzeuger.All(e => e.Strom || !Offen(Finde(pfad, e.Knoten, e.Operation)));

    /// <summary>
    /// Ein Sammel-Port (<c>IReadOnlyList&lt;T&gt;</c>): fertig, wenn der Ursprung des Stroms am Pfad fertig ist und in jedem seiner Unterläufe die
    /// Erzeuger — die Liste in der Reihenfolge der Unterläufe; nie, wenn der Ursprung nie kam; sonst warten.
    /// </summary>
    private Aufrufzustand Liste(Aufruf a, int i, out object?[]? liste)
    {
        liste = null;
        var p = a.Op.Ports[i];
        var ursprung = _graph.Ursprung(p);
        var quellen = _graph.KnotenNamens(ursprung)!.Operationen.Where(o => o.Stroeme.Length > 0).Select(o => Finde(a.Pfad, ursprung, o.Auf)).ToList();
        if (quellen.Any(Offen)) return Aufrufzustand.Wartet;
        if (quellen.All(q => q!.Zustand == Aufrufzustand.Nichts)) return Aufrufzustand.Nichts;
        foreach (var unter in a.Pfad.Unterlaeufe.Where(u => u.Ursprung == ursprung))
            if (p.Erzeuger.Where(e => !(e.Strom && e.Knoten == ursprung)).Any(e => Offen(Finde(unter, e.Knoten, e.Operation)))) return Aufrufzustand.Wartet;
        liste = a.Listen[i].OrderBy(x => x.Index).ThenBy(x => x.Folge).Select(x => (object?)x.Wert).ToArray();
        return Aufrufzustand.Fertig;
    }

    /// <summary>
    /// Ein Rennen ist entschieden (der erste Fall eines OneOf-Ports ist da): die Erzeuger der übrigen Fälle am Pfad, die niemand mehr braucht und die
    /// nichts nach draußen geben, werden abgebrochen — und mit ihnen, was nur sie speiste.
    /// </summary>
    private void Rennen(Aufruf a, int i, ILaufWirkung w)
    {
        var sieger = a.Werte[i]!.GetType();
        foreach (var e in a.Op.Ports[i].Erzeuger.Where(e => e.Typ != sieger && !e.Strom))
            if (Finde(a.Pfad, e.Knoten, e.Operation) is { } x) Unnoetig(x, w);
    }

    private void Unnoetig(Aufruf x, ILaufWirkung w)
    {
        if (!Offen(x)) return;
        var imGraphen = _graph.ImGraphen(x.Knoten.Name);
        if (x.Op.Ausgaben.Any(t => !imGraphen.Contains(t)) || x.Op.Stroeme.Length > 0) return;   // gibt etwas nach draußen bzw. öffnet Unterläufe
        foreach (var typ in x.Op.Ausgaben)
            foreach (var (k, o, i) in _graph.Abnehmer(x.Knoten.Name, x.Op.Auf, typ))
            {
                var port = _graph.KnotenNamens(k)!.Operation(o)!.Ports[i];
                var c = port.Form == Portform.Alle ? (x.Pfad.Eltern is { } oben ? Finde(oben, k, o) : null) : Finde(x.Pfad, k, o);
                if (c is null || c.Zustand == Aufrufzustand.Wartet && !c.Da[i]) return;   // jemand wartet noch darauf
            }
        if (x.Zustand == Aufrufzustand.Laeuft) w.Breche(x.Knoten.Name, Kennung, [x.Kennung]);
        var oberhalb = x.Op.Ports.SelectMany(p => p.Erzeuger).Where(e => !e.Strom).Select(e => Finde(x.Pfad, e.Knoten, e.Operation)).OfType<Aufruf>().ToList();
        Ende(x, Aufrufzustand.Nichts, w);
        foreach (var u in oberhalb) Unnoetig(u, w);
    }
}
