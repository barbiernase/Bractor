using System.Globalization;
using System.Text;
using Abstractions;
using Core;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Projections.Abrechnung;

namespace Pruefdomaene.Infrastructure.Abrechnung;

/// <summary>
/// Der Rechnungsbild-Store, ein Store der Klasse 2 im Dateisystem (<see cref="DateiZiel"/>): je Rechnung die Freigabe und die Gutschrift, dazu die
/// Lage — jede Datei nur, wenn das auslösende Event später im Log steht als das gespeicherte (die Bündel über ein Feld und global lösen aus
/// verschiedenen Streams aus, also aus verschiedenen Partitionen).
/// </summary>
public sealed class RechnungsbildStore : DateiZiel, IRechnungsbildStore
{
    public RechnungsbildStore(StoreOrte orte) : base(orte.Verzeichnis(nameof(RechnungsbildStore))) { }

    public Task FreigegebenAsync(Id<Rechnung> rechnung, decimal betrag, Herkunft h)
        => LegeAbWennNeuer($"{rechnung.Wert:N}/freigabe", Text(betrag.ToString(CultureInfo.InvariantCulture)), h);

    public Task GutgeschriebenAsync(Id<Rechnung> rechnung, decimal gestellt, decimal gutgeschrieben, Herkunft h)
        => LegeAbWennNeuer($"{rechnung.Wert:N}/gutschrift",
            Text(gestellt.ToString(CultureInfo.InvariantCulture) + ";" + gutgeschrieben.ToString(CultureInfo.InvariantCulture)), h);

    public Task LageAsync(string sperrgrund, double ausreisser, Herkunft h)
        => LegeAbWennNeuer("lage", Text(ausreisser.ToString("R", CultureInfo.InvariantCulture) + ";" + sperrgrund), h);

    public Task<Rechnungsbild> HoleAsync(Id<Rechnung> rechnung)
    {
        var freigabe = Lies($"{rechnung.Wert:N}/freigabe") is { } f ? decimal.Parse(Encoding.UTF8.GetString(f), CultureInfo.InvariantCulture) : (decimal?)null;
        var gutschrift = Lies($"{rechnung.Wert:N}/gutschrift") is { } g ? Encoding.UTF8.GetString(g).Split(';') : null;
        return Task.FromResult(new Rechnungsbild(rechnung, freigabe,
            gutschrift is null ? null : decimal.Parse(gutschrift[0], CultureInfo.InvariantCulture),
            gutschrift is null ? null : decimal.Parse(gutschrift[1], CultureInfo.InvariantCulture)));
    }

    public Task<Lagebild> HoleAsync()
    {
        if (Lies("lage") is not { } l) return Task.FromResult(new Lagebild(null, null));
        var teile = Encoding.UTF8.GetString(l).Split(';', 2);
        return Task.FromResult(new Lagebild(teile[1], double.Parse(teile[0], CultureInfo.InvariantCulture)));
    }

    private static byte[] Text(string s) => Encoding.UTF8.GetBytes(s);
}
