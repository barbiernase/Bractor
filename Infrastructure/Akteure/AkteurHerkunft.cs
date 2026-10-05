using Abstractions;

namespace Infrastructure.Akteure;

/// <summary>
/// Von welchem Akteur kommt, was das Framework gerade erzeugt? (<c>docs/konzept-akteure.md</c> §8.2/§8.4) — zur Laufzeit:
/// <list type="number">
/// <item><b>Kette:</b> der Akteur des auslösenden Events (Header <c>akteur</c> im Log → <see cref="EventEnvelope.UserId"/>) —
///   der Konsument setzt ihn als <see cref="ImAuftrag"/> um den Handle (<see cref="Aus"/>), der Emit stempelt ihn.</item>
/// <item><b>Akteur-Wechsel:</b> ein Handle mit dem Dienst eines Akteurs (<c>IAkteurDienst&lt;A&gt;</c>) — generiert gesetzt.</item>
/// <item><b>Ingress ohne Kette</b> (Datei-/Timer-Trigger, Selbst-Tick ohne Herkunft): der EINE Akteur, der den Typ per
///   <c>IDarf</c> hineingeben darf (<see cref="EindeutigerHalter"/>); mehrere/keiner → keiner (<c>system</c>).</item>
/// </list>
/// Der Fachcode sieht davon nichts (Invariante 5).
/// </summary>
public static class AkteurHerkunft
{
    private static readonly Lazy<Dictionary<Type, string?>> Halter = new(() =>
    {
        var d = new Dictionary<Type, string?>();
        foreach (var a in GeneratedAkteurRechte.Alle.Values)
            foreach (var t in a.Commands.Concat(a.Queries).Concat(a.Trigger).Concat(a.TransientEvents))
                d[t] = d.ContainsKey(t) ? null : a.Name;   // zweiter Halter → mehrdeutig
        return d;
    });

    /// <summary>Der eine Akteur, der <paramref name="typ"/> per <c>IDarf</c> hineingeben darf — sonst null.</summary>
    public static string? EindeutigerHalter(Type typ) => Halter.Value.GetValueOrDefault(typ);

    /// <summary>Setzt <paramref name="akteur"/> als „im Auftrag von" bis zum Dispose — nichts, wenn es kein echter Akteur ist.</summary>
    public static IDisposable? Aus(string? akteur) => ImAuftrag.IstAkteur(akteur) ? ImAuftrag.Von(akteur!) : null;
}
