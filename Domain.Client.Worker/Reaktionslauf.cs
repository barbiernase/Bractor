using Abstractions;
using Client.Infrastructure.Reaktionen;
using Domain.Akteure;

// Dieser Client IST der ReaktionsWorker (docs/konzept-akteure.md §4): er verkörpert den Datensatzpfleger und den Teilmelder — was sie hören und
//   dürfen, steht dort.
[assembly: IstClient<ReaktionsWorker>]

namespace Domain.Client.Worker;

/// <summary>Der Lauf des ReaktionsWorkers: alle Reaktionen der Domäne an einer Verbindung, ein gesicherter Stand.</summary>
public static class Reaktionslauf
{
    public static Reaktionen Registriere(Reaktionen r) => SammelReaktionen.Registriere(DatensatzReaktionen.Registriere(r));

    /// <summary>
    /// Der Worker, bis <paramref name="ct"/> endet: verbindet mit dem Host (<paramref name="adresse"/>, Token seiner Akteure), lässt die Reaktionen
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
                melde?.Invoke($"verbunden mit {adresse}");
                await Registriere(new Reaktionen(proxy, stand, name, takt: takt)).LaufeAsync(ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                melde?.Invoke($"unterbrochen ({e.Message}) — neu verbinden");
                try { await Task.Delay(TimeSpan.FromSeconds(1), ct); } catch (OperationCanceledException) { }
            }
        }
    }
}
