using System.Text;
using System.Text.Json.Nodes;
using Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Funktionen;

/// <summary>
/// Was eine Aktivierung eines Funktions-Actors hält (docs/04 §4.4, docs/10 E6) — gebaut beim Aktivieren, freigegeben beim Stoppen,
/// Passivieren und vor einem Neustart, nach dem Vorbild des Bereichs eines Aggregat-Actors:
/// <list type="bullet">
/// <item>ein eigener DI-Bereich: die gerufenen Funktionen (<c>IFunktion</c> ohne <c>Auf</c>, im Bereich gebunden) leben genau so lange wie die
///   Aktivierung — eine Datenbank-Verbindung, ein geladenes Modell, ein Cache;</item>
/// <item>die Umsetzung im Host, über die generierte Fabrik in diesem Bereich gebaut (nie einmal je Knoten neben den Actors);</item>
/// <item>ihr Zustand (<c>IZustand&lt;T&gt;</c>, docs/04 §4.4) und der Zustand jeder gerufenen Funktion in ihrer Konstruktor-Kette
///   (<see cref="AktivierungsZustaende"/>): frisch beim Aktivieren, geladen und gesichert über die generierten Formen (<see cref="FunktionsZustandsform"/>)
///   als EIN Dokument — je Objekt sein Zustand unter dem Namen seiner Funktion (<c>{"Schaetzer": …, "IZaehlwerk": …}</c>). Der Actor lädt ihn,
///   bevor das erste <c>Auf</c> läuft, und sichert ihn mit der Position;</item>
/// <item>ihr optionaler Lebenszyklus (<see cref="ILebenszyklus"/>, E10): <see cref="AktiviereAsync"/> nach dem Laden des Zustands,
///   <see cref="SichereAsync"/> ruft vor dem Serialisieren <c>VorDemSichernAsync</c>.</item>
/// </list>
/// Freigegeben wird in umgekehrter Reihenfolge: zuerst die Umsetzung (<see cref="IAsyncDisposable"/> bzw. <see cref="IDisposable"/>), dann der
/// Bereich mit seinen gerufenen Funktionen. Die nächste Aktivierung — auf diesem oder einem anderen Knoten — baut alles neu.
/// </summary>
public sealed class FunktionsAktivierung : IAsyncDisposable
{
    private readonly IServiceProvider _wurzel;
    private readonly Func<IServiceProvider, object?, object> _baue;
    private readonly FunktionsZustandsform? _form;
    private readonly string _name;
    private readonly bool _gerufenerZustand;
    private AsyncServiceScope? _bereich;
    private object? _umsetzung;
    private object? _zustand;
    private AktivierungsZustaende? _gerufene;

    /// <param name="wurzel">Der Container des Knotens; je Aktivierung wird daraus ein Bereich geöffnet.</param>
    /// <param name="baue">Die generierte Fabrik der Umsetzung (<see cref="FunktionsUmsetzung.Baue"/>).</param>
    /// <param name="zustand">Die Form des Zustands der Funktion; null = ohne eigenen Zustand.</param>
    /// <param name="name">Der Name der Funktion — ihr Schlüssel im gesicherten Dokument.</param>
    /// <param name="gerufenerZustand">Trägt eine gerufene Funktion in der Konstruktor-Kette Zustand (<see cref="GerufeneWahl.MitZustand"/>)?</param>
    public FunktionsAktivierung(IServiceProvider wurzel, Func<IServiceProvider, object?, object> baue, FunktionsZustandsform? zustand = null,
        string name = "", bool gerufenerZustand = false)
    {
        _wurzel = wurzel;
        _baue = baue;
        _form = zustand;
        _name = name;
        _gerufenerZustand = gerufenerZustand;
    }

    /// <summary>Hat die Aktivierung Zustand (die Funktion selbst oder eine gerufene in ihrer Konstruktor-Kette)?</summary>
    public bool MitZustand => _form is not null || _gerufenerZustand;

    /// <summary>Die Zustände dieser Aktivierung: der der Funktion (unter ihrem Namen), dann die der gerufenen Funktionen.</summary>
    private IEnumerable<(string Name, FunktionsZustandsform Form, object Zustand)> Zustaende()
    {
        if (_form is not null && _zustand is not null) yield return (_name, _form, _zustand);
        foreach (var (n, (f, z)) in _gerufene?.Alle ?? new Dictionary<string, (FunktionsZustandsform, object)>()) yield return (n, f, z);
    }

    /// <summary>
    /// Der Lebenszyklus der Funktion (<see cref="ILebenszyklus"/>, optional): nach dem Laden des Zustands, vor dem ersten <c>Auf</c>. Ohne
    /// Lebenszyklus nichts.
    /// </summary>
    public Task AktiviereAsync(CancellationToken ct) =>
        _umsetzung is ILebenszyklus l ? l.BeimAktivierenAsync(ct) : Task.CompletedTask;

    /// <summary>Vor jeder Sicherung, im Ruhepunkt (<see cref="ILebenszyklus.VorDemSichernAsync"/>); danach die Daten des Zustands.</summary>
    public async Task<byte[]> SichereAsync(CancellationToken ct)
    {
        if (_umsetzung is ILebenszyklus l) await l.VorDemSichernAsync(ct);
        return Sichere();
    }

    /// <summary>
    /// Lädt gesicherte Daten in die Zustände dieser Aktivierung (vor dem ersten <c>Auf</c>): je Objekt sein Eintrag; fehlt er (oder null = noch nie
    /// gesichert), der Anfangszustand.
    /// </summary>
    public void Lade(byte[]? daten)
    {
        var dok = daten is null ? null : JsonNode.Parse(daten) as JsonObject
            ?? throw new InvalidOperationException($"Der gesicherte Zustand von {_name} ist kein Objekt (je Funktion ihr Zustand).");
        foreach (var (name, form, zustand) in Zustaende())
            if (dok?[name] is { } eintrag) form.Lies(zustand, Encoding.UTF8.GetBytes(eintrag.ToJsonString()));
            else form.Anfang(zustand);
    }

    /// <summary>Die Zustände dieser Aktivierung als EIN Dokument (zum Sichern, im Moment des Aufrufs).</summary>
    public byte[] Sichere()
    {
        if (!MitZustand || _umsetzung is null) throw new InvalidOperationException("Die Aktivierung hat keinen Zustand.");
        var dok = new JsonObject();
        foreach (var (name, form, zustand) in Zustaende()) dok[name] = JsonNode.Parse(form.Schreibe(zustand));
        return Encoding.UTF8.GetBytes(dok.ToJsonString());
    }

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
            _gerufene = bereich.ServiceProvider.GetService<AktivierungsZustaende>();   // gefüllt beim Bau der Konstruktor-Kette
            _bereich = bereich;
        }
        catch
        {
            await bereich.DisposeAsync();
            throw;
        }
    }

    /// <summary>Gibt Umsetzung und gerufene Funktionen frei (beim Stoppen, Passivieren, vor einem Neustart).</summary>
    public async ValueTask DisposeAsync()
    {
        var umsetzung = Interlocked.Exchange(ref _umsetzung, null);
        _zustand = null;
        _gerufene = null;
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
