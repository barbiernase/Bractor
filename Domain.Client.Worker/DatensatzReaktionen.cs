using Abstractions;
using Client.Infrastructure.Reaktionen;
using Domain.Akteure;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Projections;
using Domain.Projections.ImagePair;

// Dieser Client IST der DatensatzWorker (docs/konzept-akteure.md §4): er verkörpert den Datensatzpfleger — was er hört und darf, steht dort.
[assembly: IstClient<DatensatzWorker>]

namespace Domain.Client.Worker;

/// <summary>
/// Die Reaktionen auf Datensätze (docs/plan-katalog-und-graphen.md §1, Schritt 6): ein Decide gibt nur Fakten aus; was danach geschieht, stößt dieser
/// Client an. Auf <see cref="RangeAngefordert"/> sucht er die Paare der Kriterien (Seite für Seite) und gibt sie dem Datensatz
/// (<see cref="NimmRangeAuf"/>, auch leer — das Decide sagt dann „Range leer“). Auf <see cref="EinfrierenAngefordert"/> liest er je Mitglied den
/// Label-Stand und die Bildpfade, teilt deterministisch auf (<see cref="SplitZuteiler"/>) und schließt ab (<see cref="SchliesseEinfrierenAb"/>).
/// Genau einmal trägt der Läufer: dieselbe Position ergibt dieselbe Command-Id.
/// </summary>
public static class DatensatzReaktionen
{
    public const int SeitenGroesse = 500;

    public static Reaktionen Registriere(Reaktionen r) => r.Auf<RangeAngefordert>(LoeseRangeAufAsync).Auf<EinfrierenAngefordert>(FriereEinAsync);

    /// <summary>
    /// Der Worker, bis <paramref name="ct"/> endet: verbindet mit dem Host (<paramref name="adresse"/>, Token des Datensatzpflegers), lässt die Reaktionen
    /// laufen und verbindet nach einer Unterbrechung neu — ab dem gesicherten Stand. Derselbe Lauf als Prozess (Program) und in Prüfständen.
    /// </summary>
    public static async Task LaufeAsync(string adresse, string? token, IReaktionsStand stand, string name, CancellationToken ct, TimeSpan? takt = null,
        Action<string>? melde = null)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var proxy = new global::Client.Infrastructure.Connection.GrpcProxy(Leitung.GeneratedNutzlast.Instanz) { AkteurToken = token };
                await proxy.ConnectAsync(adresse, Microsoft.Extensions.DependencyInjection.GeneratedWiring_Domain_Client_Worker.Vertrag,
                    Microsoft.Extensions.DependencyInjection.GeneratedWiring_Domain_Client_Worker.VertragHash);
                melde?.Invoke($"verbunden mit {adresse} — Arbeit angenommen");
                await Registriere(new Reaktionen(proxy, stand, name, takt: takt)).LaufeAsync(ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                melde?.Invoke($"unterbrochen ({e.Message}) — neu verbinden");
                try { await Task.Delay(TimeSpan.FromSeconds(1), ct); } catch (OperationCanceledException) { }
            }
        }
    }

    public static async Task LoeseRangeAufAsync(RangeAngefordert e, ReaktionsKontext k)
    {
        var ids = new List<Id<ImagePair.ImagePair>>();
        for (var seite = 1; ; seite++)
        {
            var treffer = (await k.FrageAsync<ImagePairSuchergebnis>(new SucheImagePairs(ZuFilter(e.Kriterien, seite, SeitenGroesse)))).Data;
            ids.AddRange(treffer.Items.Select(m => m.Id));
            if (treffer.Items.Count == 0 || ids.Count >= treffer.GesamtAnzahl) break;
        }
        k.Sende(new NimmRangeAuf(new Id<Datensatz.Datensatz>(k.Ereignis.AggregateId), ids, new RangeHerkunft(e.Kriterien, ids.Count)));
    }

    public static async Task FriereEinAsync(EinfrierenAngefordert e, ReaktionsKontext k)
    {
        var paare = new Dictionary<Guid, ImagePairAntwort>();
        foreach (var id in e.Mitglieder)
            if ((await k.FrageAsync<IQueryResponse>(new GetImagePair(id))).Data is ImagePairAntwort a) paare[id.Wert] = a;
        var splits = SplitZuteiler.Zuteilen(paare.Select(kv => (kv.Key, Label(kv.Value))).ToList(), e.Split);
        var mitglieder = paare.OrderBy(kv => kv.Key)
            .Select(kv => new DatensatzMitglied(new Id<ImagePair.ImagePair>(kv.Key), Label(kv.Value), splits[kv.Key], kv.Value.Dc0Pfad ?? "", kv.Value.Dc2Pfad ?? ""))
            .ToList();
        k.Sende(new SchliesseEinfrierenAb(new Id<Datensatz.Datensatz>(k.Ereignis.AggregateId), mitglieder));
    }

    /// <summary>Das Trainings-Label eines Paares: das physische Produkt-Label ist die stärkste Wahrheit, dann das menschliche Paar-Label, zuletzt die KI.</summary>
    public static Klassifikation Label(ImagePairAntwort m) =>
        m.PhysischesProduktLabel ?? m.MenschBildpaarLabel ?? m.KiBildpaarKlassifikation ?? Klassifikation.KeineAnomalie;

    private static ImagePairFilter ZuFilter(RangeKriterien k, int seite, int seitenGroesse) => new(
        Von: k.Von, Bis: k.Bis, KiKlassifikation: k.KiKlassifikation, MenschLabel: k.MenschLabel, ProduktLabel: k.ProduktLabel,
        NurKomplette: k.NurKomplette, HatKiKlassifikation: k.HatKiKlassifikation, HatMenschLabel: k.HatMenschLabel,
        NurNichtInspizierte: k.NurNichtInspizierte, Seite: seite, SeitenGroesse: seitenGroesse);
}
