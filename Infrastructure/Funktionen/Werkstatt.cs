using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Abstractions;

namespace Infrastructure.Funktionen;

/// <summary>
/// Ein Auftrag an einen Arbeiter draußen: die Kennung des Eingangs (gleich bei jeder Wiederholung — draußen der Schlüssel zum Entdoppeln von
/// Wirkungen), die Funktion, die Nachricht (ein Zwischenergebnis, eine Folge, ein Hinweis oder eine Antwort auf einen eigenen Command) und ihre Herkunft.
/// </summary>
public sealed record AuftragDraussen(Guid Vorgang, FunktionsBaustein Funktion, object Eingang, Herkunft Herkunft, string? Korrelation, bool Hinweis);

/// <summary>
/// Ein ARBEITER draußen (docs/04 §4.4): eine Sitzung eines Workers oder Clients, der die Funktion trägt, oder der Mensch an der Werkbank (Laufort
/// Hand). Er bekommt Aufträge und meldet ihre Ausgaben an die <see cref="Werkstatt"/> seines Knotens.
/// </summary>
public interface IArbeiter
{
    string Name { get; }

    /// <summary>So lange darf ein Auftrag ohne Regung (Ausgabe, Lebenszeichen) bleiben, danach gilt er als verloren; null = unbegrenzt (ein Mensch überlegt).</summary>
    TimeSpan? Geduld { get; }

    /// <summary>Reicht den Auftrag hinaus — verlierbar: kommt er nicht an, bleibt die Regung aus und der Eingang wird nach der Geduld erneut gerechnet.</summary>
    void Sende(AuftragDraussen auftrag);
}

/// <summary>Was mit einer gemeldeten Antwort draußen geschah.</summary>
public enum Werkstattausgang
{
    /// <summary>Der Auftrag läuft auf diesem Knoten nicht (schon fertig, nie vergeben, abgebrochen).</summary>
    Unbekannt,
    /// <summary>Angenommen — das Kind bringt die Ausgaben hinaus.</summary>
    Angenommen,
    /// <summary>Außerhalb der Signatur, außer der Reihe oder ein gemeldeter Fehler: der Eingang scheitert und wird wiederholt.</summary>
    Abgelehnt,
}

/// <summary>
/// Die WERKSTATT eines Knotens (docs/04 §4.4): die Arbeiter draußen je Funktion mit ihren Slots. Sie ersetzt den Vermittler — kein Pull, keine Lease,
/// kein Festhalten: ein Kind der Funktion rechnet einen Eingang, indem es ihn über <see cref="RechneAsync"/> einem Arbeiter mit freiem Platz gibt und
/// seine Ausgaben in Reihenfolge entgegennimmt (bei einem Strom in Stücken); hinausgebracht werden sie vom Kind wie die einer Umsetzung im Host
/// (Command-Ids aus Eingang, Funktion und Stelle). Scheitert der Arbeiter, meldet er sich ab oder regt er sich zu lange nicht, scheitert der Lauf —
/// der Parent rechnet den Eingang erneut, der durable Rand stellt ihn nach seiner Frist wieder zu. Es zählt nur die Antwort des Arbeiters, der den
/// Auftrag hält — die erste, weil das Kind den Lauf nach ihr beendet.
///
/// Die Slots aller Arbeiter einer Funktion sind die Parallelität ihres Parents auf diesem Knoten: meldet sich der erste Arbeiter an, entsteht er, geht
/// der letzte, endet er (<see cref="Beobachte"/>).
/// </summary>
public sealed class Werkstatt
{
    private sealed class Platz(IArbeiter arbeiter, int slots)
    {
        public IArbeiter Arbeiter { get; } = arbeiter;
        public int Slots { get; } = slots;
        public int Belegt;
    }

    private sealed record Regung(IReadOnlyList<object>? Ausgaben, int Ab, bool Fertig, string? Fehler);

    private sealed class Lauf(Platz platz, FunktionsEingang eingang)
    {
        public Platz Platz { get; } = platz;
        public FunktionsEingang Eingang { get; } = eingang;
        public Channel<Regung> Kanal { get; } = Channel.CreateUnbounded<Regung>(new UnboundedChannelOptions { SingleReader = true });
        /// <summary>1, sobald die Antwort vollständig (oder ein Fehler) gemeldet ist — jede weitere ist überholt.</summary>
        public int Beantwortet;
    }

    private static readonly Regung Lebt = new(null, 0, false, null);

    private readonly ConcurrentDictionary<string, ImmutableList<Platz>> _plaetze = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, Lauf> _laeufe = new();
    private Action<string, int>? _beobachter;

    /// <summary>Die Slots aller Arbeiter der Funktion auf diesem Knoten.</summary>
    public int Slots(string funktion) => _plaetze.TryGetValue(funktion, out var p) ? p.Sum(x => x.Slots) : 0;

    /// <summary>Die Funktionen mit mindestens einem Arbeiter auf diesem Knoten.</summary>
    public IReadOnlyList<string> Funktionen => _plaetze.Where(p => !p.Value.IsEmpty).Select(p => p.Key).ToList();

    /// <summary>
    /// Meldet jede Änderung der Slots einer Funktion (Funktion, Slots; 0 = kein Arbeiter mehr) — sofort für alle, die schon Arbeiter haben. Die Wurzel
    /// der Funktionen legt daraus die Parents an, passt ihre Parallelität an und beendet sie.
    /// </summary>
    public void Beobachte(Action<string, int> beobachter)
    {
        _beobachter = beobachter;
        foreach (var f in Funktionen) beobachter(f, Slots(f));
    }

    /// <summary>Ein Arbeiter trägt die Funktion mit so vielen Slots (ein zweites Anmelden derselben Funktion ersetzt die Slots).</summary>
    public void Anmelden(IArbeiter arbeiter, string funktion, int slots)
    {
        var platz = new Platz(arbeiter, Math.Max(1, slots));
        _plaetze.AddOrUpdate(funktion, _ => ImmutableList.Create(platz), (_, l) => l.RemoveAll(p => ReferenceEquals(p.Arbeiter, arbeiter)).Add(platz));
        _beobachter?.Invoke(funktion, Slots(funktion));
    }

    /// <summary>Der Arbeiter ist fort (seine Sitzung endete): seine Plätze verschwinden, seine laufenden Aufträge scheitern sofort.</summary>
    public void Abmelden(IArbeiter arbeiter)
    {
        foreach (var (funktion, plaetze) in _plaetze.ToArray())
        {
            if (!plaetze.Any(p => ReferenceEquals(p.Arbeiter, arbeiter))) continue;
            _plaetze.AddOrUpdate(funktion, _ => ImmutableList<Platz>.Empty, (_, l) => l.RemoveAll(p => ReferenceEquals(p.Arbeiter, arbeiter)));
            _beobachter?.Invoke(funktion, Slots(funktion));
        }
        foreach (var lauf in _laeufe.Values.Where(l => ReferenceEquals(l.Platz.Arbeiter, arbeiter)))
            if (Interlocked.Exchange(ref lauf.Beantwortet, 1) == 0) lauf.Kanal.Writer.TryWrite(new Regung(null, 0, true, $"{arbeiter.Name} ist fort"));
    }

    /// <summary>
    /// Rechnet einen Eingang draußen: ein Arbeiter der Funktion mit dem meisten freien Platz bekommt ihn, seine Ausgaben kommen in Reihenfolge zurück
    /// (die Fälle der Signatur des Eingangs). Ohne Arbeiter, mit einem gemeldeten Fehler, ohne Regung über die Geduld hinaus oder wenn der Arbeiter
    /// geht, wirft der Lauf — der Eingang wird wiederholt.
    /// </summary>
    public async IAsyncEnumerable<object> RechneAsync(FunktionsBaustein funktion, Guid vorgang, object nachricht, Herkunft herkunft, string? korrelation,
        bool hinweis, [EnumeratorCancellation] CancellationToken ct)
    {
        var eingang = funktion.Eingang(nachricht.GetType())
            ?? throw new InvalidOperationException($"{funktion.Name} hört {nachricht.GetType().Name} nicht.");
        var platz = Waehle(funktion.Name) ?? throw new InvalidOperationException($"{funktion.Name}: kein Arbeiter draußen auf diesem Knoten.");
        var lauf = new Lauf(platz, eingang);
        if (!_laeufe.TryAdd(vorgang, lauf)) throw new InvalidOperationException($"{funktion.Name}: {vorgang} läuft auf diesem Knoten schon.");
        Interlocked.Increment(ref platz.Belegt);
        try
        {
            platz.Arbeiter.Sende(new AuftragDraussen(vorgang, funktion, nachricht, herkunft, korrelation, hinweis));
            var erwartet = 0;
            while (true)
            {
                var r = await LiesAsync(lauf, ct);
                if (ReferenceEquals(r, Lebt)) continue;
                if (r.Fehler is not null) throw new InvalidOperationException($"{funktion.Name} ist draußen gescheitert: {r.Fehler}");
                var ausgaben = r.Ausgaben ?? Array.Empty<object>();
                for (var i = 0; i < ausgaben.Count; i++)
                {
                    if (r.Ab + i < erwartet) continue;   // schon geliefert (ein Stück kam doppelt)
                    if (r.Ab + i > erwartet) throw new InvalidOperationException($"{funktion.Name}: Stück ab {r.Ab} außer der Reihe (erwartet {erwartet}).");
                    erwartet++;
                    yield return ausgaben[i];
                }
                if (r.Fertig) yield break;
            }
        }
        finally
        {
            _laeufe.TryRemove(new KeyValuePair<Guid, Lauf>(vorgang, lauf));
            Interlocked.Decrement(ref platz.Belegt);
        }
    }

    private static async Task<Regung> LiesAsync(Lauf lauf, CancellationToken ct)
    {
        if (lauf.Platz.Arbeiter.Geduld is not { } geduld) return await lauf.Kanal.Reader.ReadAsync(ct);
        using var frist = CancellationTokenSource.CreateLinkedTokenSource(ct);
        frist.CancelAfter(geduld);
        try { return await lauf.Kanal.Reader.ReadAsync(frist.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"{lauf.Platz.Arbeiter.Name}: keine Regung in {geduld.TotalSeconds:0} s.");
        }
    }

    /// <summary>Der Platz mit dem meisten freien Raum (gemessen an seinen Slots); null = kein Arbeiter.</summary>
    private Platz? Waehle(string funktion) =>
        _plaetze.TryGetValue(funktion, out var p) && !p.IsEmpty
            ? p.OrderBy(x => (double)Volatile.Read(ref x.Belegt) / x.Slots).First()
            : null;

    /// <summary>
    /// Die Antwort eines Arbeiters auf einen Auftrag — ganz oder als Stück eines Stroms (<paramref name="ab"/> = Stelle der ersten Ausgabe,
    /// <paramref name="fertig"/> = der Lauf ist vollständig), oder ein Fehler. Eine Ausgabe außerhalb der Signatur oder mehr als eine ohne Strom
    /// lässt den Lauf scheitern. Es zählt nur die Antwort des Arbeiters, der den Auftrag hält, und von ihm die erste vollständige: jede andere (ein
    /// zweiter Arbeiter, eine späte Antwort, eine nach dem Neuverbinden) ist unbekannt.
    /// </summary>
    public Werkstattausgang Ergebnis(IArbeiter von, Guid vorgang, IReadOnlyList<object>? ausgaben, int ab, bool fertig, string? fehler)
    {
        if (!_laeufe.TryGetValue(vorgang, out var lauf) || !ReferenceEquals(lauf.Platz.Arbeiter, von) || Volatile.Read(ref lauf.Beantwortet) == 1)
            return Werkstattausgang.Unbekannt;
        if (fehler is null && ausgaben?.FirstOrDefault(a => !lauf.Eingang.Ausgaben.Contains(a.GetType())) is { } fremd)
            fehler = $"{fremd.GetType().Name} liegt außerhalb der Signatur ({string.Join(", ", lauf.Eingang.Ausgaben.Select(t => t.Name))})";
        if (fehler is null && !lauf.Eingang.Strom && (ab + (ausgaben?.Count ?? 0) > 1 || !fertig))
            fehler = $"auf {lauf.Eingang.Typ.Name} kommt höchstens eine Ausgabe, kein Strom";
        if ((fertig || fehler is not null) && Interlocked.Exchange(ref lauf.Beantwortet, 1) == 1) return Werkstattausgang.Unbekannt;
        lauf.Kanal.Writer.TryWrite(fehler is null ? new Regung(ausgaben, ab, fertig, null) : new Regung(null, 0, true, fehler));
        return fehler is null ? Werkstattausgang.Angenommen : Werkstattausgang.Abgelehnt;
    }

    /// <summary>Lebenszeichen eines laufenden Auftrags (ein langer Lauf) von dem Arbeiter, der ihn hält: die Geduld beginnt von vorn.</summary>
    public bool Regt(IArbeiter von, Guid vorgang) =>
        _laeufe.TryGetValue(vorgang, out var lauf) && ReferenceEquals(lauf.Platz.Arbeiter, von) && lauf.Kanal.Writer.TryWrite(Lebt);

    /// <summary>Läuft der Auftrag auf diesem Knoten?</summary>
    public bool Laeuft(Guid vorgang) => _laeufe.ContainsKey(vorgang);
}
