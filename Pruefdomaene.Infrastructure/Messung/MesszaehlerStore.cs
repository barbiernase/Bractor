using Abstractions;
using Core;
using Pruefdomaene.Messung;
using Pruefdomaene.Projections.Messung;

namespace Pruefdomaene.Infrastructure.Messung;

/// <summary>
/// Der Messzähler-Store, ein Store der Klasse 3 (<see cref="DateiSink"/>): je Messung eine angehängte Zeile in <c>&lt;stelle&gt;</c>, ohne Bedingung —
/// nach einem Abbruch zwischen Schreiben und Fortschritt steht sie doppelt. Nur die ausdrücklich erlaubte <see cref="MesszaehlerProjektion"/> schreibt hier.
/// </summary>
public sealed class MesszaehlerStore : DateiSink, IMesszaehlerStore
{
    public MesszaehlerStore(StoreOrte orte) : base(orte.Verzeichnis(nameof(MesszaehlerStore))) { }

    public Task ZaehleAsync(Id<Messstelle> stelle) => HaengeAn($"{stelle.Wert:N}", "1");

    public Task<Messzaehler> LiesAsync(Id<Messstelle> stelle) => Task.FromResult(new Messzaehler(stelle, Zeilen($"{stelle.Wert:N}").Count));
}
