using System.Diagnostics;

namespace Verhalten.Tests.Geruest;

public static class Warte
{
    /// <summary>Fragt alle 200 ms, bis die Bedingung gilt; sonst Zeitüberschreitung mit Meldung (und, falls gegeben, dem Ende der Host-Ausgabe).</summary>
    public static async Task BisAsync(Func<Task<bool>> bedingung, TimeSpan frist, string meldung, Hostlauf? host = null)
    {
        var sw = Stopwatch.StartNew();
        Exception? letzter = null;
        while (sw.Elapsed < frist)
        {
            try
            {
                if (await bedingung()) return;
                letzter = null;
            }
            catch (Exception e) { letzter = e; }
            await Task.Delay(200);
        }
        throw new TimeoutException($"{meldung} (nach {frist.TotalSeconds:0} s)"
                                   + (letzter != null ? $"\nletzter Fehler: {letzter.Message}" : "")
                                   + (host != null ? $"\n── Host ──\n{host.LogEnde()}" : ""));
    }

    /// <summary>Nachzügler abwarten: was jetzt noch käme, wäre eine zweite Wirkung.</summary>
    public static Task NachlaufAsync(int sekunden = 3) => Task.Delay(TimeSpan.FromSeconds(sekunden));
}
