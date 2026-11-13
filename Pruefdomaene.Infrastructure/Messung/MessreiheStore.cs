using System.Globalization;
using Abstractions;
using Core;
using Pruefdomaene.Messung;
using Pruefdomaene.Projections.Messung;
using Pruefwerk.Katalog;

namespace Pruefdomaene.Infrastructure.Messung;

/// <summary>
/// Der Messreihen-Store, ein Store der Klasse 2 im Dateisystem (<see cref="DateiZiel"/>): je Messpunkt eine Datei
/// <c>&lt;stelle&gt;/punkte/&lt;version&gt;</c> (die Event-Identität im Schlüssel — eine Wiederholung schreibt dieselbe Datei), der letzte Wert in
/// <c>&lt;stelle&gt;/letzter</c> nur, wenn die Version größer ist als die gespeicherte (eine zweite Aktivierung mit älterem Stand dreht ihn nicht zurück).
/// </summary>
public sealed class MessreiheStore : DateiZiel, IMessreiheStore, IHoleReihe
{
    public MessreiheStore(StoreOrte orte) : base(orte.Verzeichnis(nameof(MessreiheStore))) { }

    public async Task SchreibeAsync(Id<Messstelle> stelle, double wert, Herkunft h)
    {
        var inhalt = System.Text.Encoding.UTF8.GetBytes(wert.ToString("R", CultureInfo.InvariantCulture));
        await LegeAb($"{stelle.Wert:N}/punkte/{h.Version:D10}", inhalt);
        await LegeAbWennNeuer($"{stelle.Wert:N}/letzter", inhalt, h);
    }

    public Task<Messreihe> HoleAsync(Id<Messstelle> stelle)
    {
        var werte = Liste($"{stelle.Wert:N}/punkte").Select(p => Zahl(Lies(p))).OfType<double>().ToList();
        return Task.FromResult(new Messreihe(stelle, werte, Zahl(Lies($"{stelle.Wert:N}/letzter"))));
    }

    /// <summary>Die Lese-Fähigkeit des Katalogs Pruefwerk (der Schätzer liest so): dieselben Werte, in seinen eigenen Typen.</summary>
    public async Task<Reihe> HoleAsync(Guid stelle) => new((await HoleAsync(new Id<Messstelle>(stelle))).Werte);

    private static double? Zahl(byte[]? b)
        => b is null ? null : double.Parse(System.Text.Encoding.UTF8.GetString(b), CultureInfo.InvariantCulture);
}
