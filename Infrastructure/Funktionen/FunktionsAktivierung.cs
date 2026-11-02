using Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Funktionen;

/// <summary>
/// Was eine Aktivierung einer Funktion hält (docs/04 §4.4, docs/10 E6; eine je Kind) — gebaut beim Beginn des Kinds, freigegeben beim Stoppen, nach
/// dem Vorbild des Bereichs eines Aggregat-Actors:
/// <list type="bullet">
/// <item>ein eigener DI-Bereich: die Technik (was der Konstruktor nimmt, im Bereich gebunden) lebt genau so lange wie die Aktivierung — eine
///   Datenbank-Verbindung, ein geladenes Modell, ein Cache; sie lädt beim ersten Aufruf;</item>
/// <item>die Umsetzung im Host, über die generierte Fabrik in diesem Bereich gebaut (nie einmal je Knoten neben den Actors);</item>
/// <item>ihr Zustand (<c>IZustand&lt;T&gt;</c>, docs/04 §4.4) und der Zustand jeder Technik in ihrer Konstruktor-Kette
///   (<see cref="AktivierungsZustaende"/>): frisch bei jeder Aktivierung, ein Cache, nie gesichert.</item>
/// </list>
/// Freigegeben wird in umgekehrter Reihenfolge: zuerst die Umsetzung (<see cref="IAsyncDisposable"/> bzw. <see cref="IDisposable"/>), dann der
/// Bereich mit seiner Technik. Die nächste Aktivierung — auf diesem oder einem anderen Knoten — baut alles neu.
/// </summary>
public sealed class FunktionsAktivierung : IAsyncDisposable
{
    private readonly IServiceProvider _wurzel;
    private readonly Func<IServiceProvider, object?, object> _baue;
    private readonly FunktionsZustandsform? _form;
    private readonly string _name;
    private readonly bool _technikZustand;
    private AsyncServiceScope? _bereich;
    private object? _umsetzung;
    private object? _zustand;

    /// <param name="wurzel">Der Container des Knotens; je Aktivierung wird daraus ein Bereich geöffnet.</param>
    /// <param name="baue">Die generierte Fabrik der Umsetzung (<see cref="FunktionsUmsetzung.Baue"/>).</param>
    /// <param name="zustand">Die Form des Zustands der Funktion; null = ohne eigenen Zustand.</param>
    /// <param name="name">Der Name der Funktion — ihr Schlüssel im gesicherten Dokument.</param>
    /// <param name="technikZustand">Trägt eine Technik in der Konstruktor-Kette Zustand (<see cref="TechnikWahl.MitZustand"/>)?</param>
    public FunktionsAktivierung(IServiceProvider wurzel, Func<IServiceProvider, object?, object> baue, FunktionsZustandsform? zustand = null,
        string name = "", bool technikZustand = false)
    {
        _wurzel = wurzel;
        _baue = baue;
        _form = zustand;
        _name = name;
        _technikZustand = technikZustand;
    }

    /// <summary>Hat die Aktivierung Zustand (die Funktion selbst oder eine Technik in ihrer Konstruktor-Kette)?</summary>
    public bool MitZustand => _form is not null || _technikZustand;

    /// <summary>Die Umsetzung dieser Aktivierung (nach <see cref="BeginneAsync"/>).</summary>
    public object Umsetzung => _umsetzung ?? throw new InvalidOperationException("Die Aktivierung ist nicht begonnen oder schon freigegeben.");

    /// <summary>Ist die Umsetzung gebaut und noch nicht freigegeben?</summary>
    public bool Aktiv => _umsetzung is not null;

    /// <summary>Öffnet den Bereich und baut die Umsetzung (beim Aktivieren des Actors); scheitert der Bau, ist der Bereich wieder zu.</summary>
    public async ValueTask BeginneAsync()
    {
        if (_umsetzung is not null) return;
        var bereich = _wurzel.CreateAsyncScope();
        try
        {
            _zustand = _form?.Neu();
            _umsetzung = _baue(bereich.ServiceProvider, _zustand);
            _bereich = bereich;
        }
        catch
        {
            await bereich.DisposeAsync();
            throw;
        }
    }

    /// <summary>Gibt Umsetzung und Technik frei (beim Stoppen, Passivieren, vor einem Neustart).</summary>
    public async ValueTask DisposeAsync()
    {
        var umsetzung = Interlocked.Exchange(ref _umsetzung, null);
        _zustand = null;
        try
        {
            switch (umsetzung)
            {
                case IAsyncDisposable a: await a.DisposeAsync(); break;
                case IDisposable d: d.Dispose(); break;
            }
        }
        finally
        {
            if (_bereich is { } b)
            {
                _bereich = null;
                await b.DisposeAsync();
            }
        }
    }
}
