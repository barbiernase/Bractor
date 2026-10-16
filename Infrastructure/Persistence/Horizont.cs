using System.Diagnostics;

namespace Infrastructure.Persistence;

/// <summary>
/// Der Horizont der Vollständigkeit (docs/03 §3.3, docs/11 §11.4): bis zu welcher globalen Sequenz jedes Event endgültig ist — committet und
/// sichtbar oder nie (zurückgerollt, eine Lücke). Ohne Uhr: er folgt aus den Transaktionsnummern von Postgres.
///
/// Eine <b>Probe</b> hält zwei Zahlen fest: den Stand des Sequenzzählers und danach die Nummer einer eigenen, kurzen Transaktion. Jede
/// Transaktion, die eine Sequenz bis zu diesem Stand gezogen hat, hatte ihre Nummer schon vorher — im Anhänge-Modus <c>Quick</c> schreibt
/// Marten zuerst die Stream-Zeile, dann die Events mit <c>nextval</c> — und Nummern werden aufsteigend vergeben; ihre Nummer ist also kleiner
/// als die der Probe. Laufen keine kleineren Nummern mehr (die älteste laufende ist mindestens die der Probe), ist alles bis zum Stand
/// endgültig. Eine lange offene Transaktion hält den Horizont an, statt überholt zu werden: lieber warten als verlieren.
///
/// Reine Logik, ohne Datenbank (Prüfstand); die Abfragen stellt <see cref="MartenEventStore"/>. Je Event-Store ein Horizont, geteilt von allen
/// Lesern eines Prozesses (<see cref="Fuer"/>).
/// </summary>
public sealed class Horizont
{
    /// <summary>Höchstens so viele offene Proben; darüber fällt die älteste weg (nur langsamer frei, nie falsch).</summary>
    public const int HoechstensOffen = 64;

    private readonly object _sperre = new();
    private readonly List<(long Stand, ulong Nummer, long Seit)> _offen = new();
    private long _frei;
    private long _letzteFreigabe = long.MinValue;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Horizont> JeStore = new();

    /// <summary>Der Horizont des Event-Stores <paramref name="store"/> — derselbe für alle Leser dieses Prozesses.</summary>
    public static Horizont Fuer(object store) => JeStore.GetValue(store, _ => new Horizont());

    /// <summary>Bis hierher ist jede Sequenz endgültig.</summary>
    public long Frei { get { lock (_sperre) return _frei; } }

    /// <summary>Eine neue Probe (Zählerstand, Nummer der Probe-Transaktion) und die älteste in derselben Abfrage noch laufende Nummer.</summary>
    public long Probe(long stand, ulong nummer, ulong aeltesteLaufende)
    {
        lock (_sperre)
        {
            if (stand > _frei)
            {
                _offen.Add((stand, nummer, Stopwatch.GetTimestamp()));
                if (_offen.Count > HoechstensOffen) _offen.RemoveAt(0);
            }
            return PruefeGesperrt(aeltesteLaufende);
        }
    }

    /// <summary>Gibt jede offene Probe frei, deren kleinere Nummern alle beendet sind.</summary>
    public long Pruefe(ulong aeltesteLaufende)
    {
        lock (_sperre) return PruefeGesperrt(aeltesteLaufende);
    }

    private long PruefeGesperrt(ulong aeltesteLaufende)
    {
        var vorher = _frei;
        for (var i = _offen.Count - 1; i >= 0; i--)
        {
            var (stand, nummer, _) = _offen[i];
            if (nummer <= aeltesteLaufende && stand > _frei) _frei = stand;
        }
        _offen.RemoveAll(p => p.Stand <= _frei);
        if (_frei > vorher) _letzteFreigabe = Stopwatch.GetTimestamp();
        return _frei;
    }

    /// <summary>Solange eine Probe offen ist, höchstens eine neue in diesem Abstand je Prozess; dazwischen genügt die Prüfung (ein Takt, nie Richtigkeit).</summary>
    public static readonly TimeSpan ProbenAbstand = TimeSpan.FromMilliseconds(5);

    private long _letzteProbe = long.MinValue;

    /// <summary>Ob jetzt eine neue Probe fällig ist; wenn ja, gilt sie ab jetzt als genommen.</summary>
    public bool ProbeFaellig()
    {
        lock (_sperre)
        {
            if (_letzteProbe != long.MinValue && Stopwatch.GetElapsedTime(_letzteProbe) < ProbenAbstand) return false;
            _letzteProbe = Stopwatch.GetTimestamp();
            return true;
        }
    }

    /// <summary>Der höchste Stand, der noch auf seine Freigabe wartet (null: keiner).</summary>
    public long? HoechsteOffene
    {
        get { lock (_sperre) return _offen.Count == 0 ? null : _offen.Max(p => p.Stand); }
    }

    /// <summary>Ob eine Probe mit mindestens diesem Stand noch auf ihre Freigabe wartet.</summary>
    public bool Wartet(long stand)
    {
        lock (_sperre) return _frei < stand && _offen.Any(p => p.Stand >= stand);
    }

    /// <summary>Wie lange die letzte Freigabe her ist (ein Takt, nur für die Frische, nie für die Richtigkeit).</summary>
    public TimeSpan SeitFreigabe
    {
        get
        {
            lock (_sperre)
                return _letzteFreigabe == long.MinValue ? TimeSpan.MaxValue : Stopwatch.GetElapsedTime(_letzteFreigabe);
        }
    }

    /// <summary>Wie lange die älteste offene Probe schon wartet (null: keine) — eine lange offene Transaktion zeigt sich hier.</summary>
    public TimeSpan? AeltesteOffen
    {
        get { lock (_sperre) return _offen.Count == 0 ? null : Stopwatch.GetElapsedTime(_offen.Min(p => p.Seit)); }
    }
}
