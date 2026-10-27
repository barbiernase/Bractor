using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Funktionen;

/// <summary>
/// Was eine Aktivierung eines Funktions-Actors hält (Plan Pub/Sub-Neustrukturierung §2.9, E6) — gebaut beim Aktivieren, freigegeben beim Stoppen,
/// Passivieren und vor einem Neustart, nach dem Vorbild des Bereichs eines Aggregat-Actors:
/// <list type="bullet">
/// <item>ein eigener DI-Bereich: die Dienste der Funktion (<c>IDienst</c>, im Bereich gebunden) leben genau so lange wie die Aktivierung — eine
///   Datenbank-Verbindung, ein geladenes Modell, ein Cache;</item>
/// <item>die Umsetzung im Host, über die generierte Fabrik in diesem Bereich gebaut (nie einmal je Knoten neben den Actors);</item>
/// <item>ihr Zustand (<c>IZustand&lt;T&gt;</c>, Plan §2.9): frisch beim Aktivieren, geladen und gesichert über die generierte Form
///   (<see cref="FunktionsZustandsform"/>) — der Actor lädt ihn, bevor das erste <c>Auf</c> läuft.</item>
/// </list>
/// Freigegeben wird in umgekehrter Reihenfolge: zuerst die Umsetzung (<see cref="IAsyncDisposable"/> bzw. <see cref="IDisposable"/>), dann der
/// Bereich mit seinen Diensten. Die nächste Aktivierung — auf diesem oder einem anderen Knoten — baut alles neu.
/// </summary>
public sealed class FunktionsAktivierung : IAsyncDisposable
{
    private readonly IServiceProvider _wurzel;
    private readonly Func<IServiceProvider, object?, object> _baue;
    private readonly FunktionsZustandsform? _form;
    private AsyncServiceScope? _bereich;
    private object? _umsetzung;
    private object? _zustand;

    /// <param name="wurzel">Der Container des Knotens; je Aktivierung wird daraus ein Bereich geöffnet.</param>
    /// <param name="baue">Die generierte Fabrik der Umsetzung (<see cref="FunktionsUmsetzung.Baue"/>).</param>
    /// <param name="zustand">Die Form des Zustands der Funktion; null = ohne Zustand.</param>
    public FunktionsAktivierung(IServiceProvider wurzel, Func<IServiceProvider, object?, object> baue, FunktionsZustandsform? zustand = null)
    {
        _wurzel = wurzel;
        _baue = baue;
        _form = zustand;
    }

    /// <summary>Hat die Funktion Zustand?</summary>
    public bool MitZustand => _form is not null;

    /// <summary>Lädt gesicherte Daten in den Zustand dieser Aktivierung (vor dem ersten <c>Auf</c>); null = Anfangszustand.</summary>
    public void Lade(byte[]? daten)
    {
        if (_form is null || _zustand is null) return;
        if (daten is null) _form.Anfang(_zustand);
        else _form.Lies(_zustand, daten);
    }

    /// <summary>Der Zustand dieser Aktivierung als Daten (zum Sichern, im Moment des Aufrufs).</summary>
    public byte[] Sichere() => _form is null || _zustand is null
        ? throw new InvalidOperationException("Die Aktivierung hat keinen Zustand.")
        : _form.Schreibe(_zustand);

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

    /// <summary>Gibt Umsetzung und Dienste frei (beim Stoppen, Passivieren, vor einem Neustart).</summary>
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
