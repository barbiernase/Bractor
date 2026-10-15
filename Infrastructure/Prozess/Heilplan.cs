using System;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.Prozess;

/// <summary>
/// Der Heil-Plan des Prozess-Backstops (PF-4): wann ein offener Vorgang ohne Aktivität eine Heil-Weckung bekommt. Rein, im Speicher des
/// Knotens, Ebene-1-beweisbar.
///
/// Der Backstop heilt, was weder Signal noch Poll sehen: einen Vorgang, dessen Selbst-Weckung nach einem Send verloren ging und dessen
/// Ziel nichts schrieb (unbekannter Emit-Ausgang), oder einen Knoten-Absturz mitten im Vorgang. Früher weckte er alle 15 s jeden offenen
/// Vorgang. Jetzt: eine Heil-Weckung, wenn ein Vorgang <see cref="Anfang"/> lang still war; bleibt er danach still, verdoppelt sich der
/// Abstand bis <see cref="Hoechstens"/>. Jede Aktivität (Weckung durch Signal oder Poll) setzt den Abstand zurück. Zeitlimits und
/// Warte-Knoten laufen nicht hierüber, sondern über die Frist im Offen-Index (genau, durabel).
///
/// Nach einem Neustart ist der Plan leer: jeder offene Vorgang bekommt beim ersten Abgleich <see cref="Anfang"/> und damit eine Heil-Weckung.
/// </summary>
public sealed class Heilplan
{
    public static readonly TimeSpan Anfang = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan Hoechstens = TimeSpan.FromHours(1);

    private readonly Dictionary<Guid, (DateTimeOffset Faellig, TimeSpan Abstand)> _plan = new();
    private readonly object _sperre = new();

    public int Anzahl { get { lock (_sperre) return _plan.Count; } }

    /// <summary>Der Vorgang war eben aktiv: die nächste Heil-Weckung erst nach <see cref="Anfang"/> Stille.</summary>
    public void Aktiv(Guid korrelation, DateTimeOffset jetzt)
    {
        lock (_sperre) _plan[korrelation] = (jetzt + Anfang, Anfang);
    }

    /// <summary>Gleicht mit den offenen Vorgängen ab: neue kommen mit <see cref="Anfang"/> hinzu, beendete fallen heraus.</summary>
    public void Abgleich(IEnumerable<Guid> offene, DateTimeOffset jetzt)
    {
        var menge = offene as IReadOnlySet<Guid> ?? new HashSet<Guid>(offene);
        lock (_sperre)
        {
            foreach (var k in _plan.Keys.Where(k => !menge.Contains(k)).ToList()) _plan.Remove(k);
            foreach (var k in menge) _plan.TryAdd(k, (jetzt + Anfang, Anfang));
        }
    }

    /// <summary>Die jetzt fälligen Vorgänge; ihr nächster Termin rückt um den verdoppelten Abstand (höchstens <see cref="Hoechstens"/>).</summary>
    public IReadOnlyList<Guid> Faellige(DateTimeOffset jetzt)
    {
        lock (_sperre)
        {
            var faellig = _plan.Where(kv => kv.Value.Faellig <= jetzt).Select(kv => kv.Key).ToList();
            foreach (var k in faellig)
            {
                var abstand = TimeSpan.FromTicks(Math.Min(_plan[k].Abstand.Ticks * 2, Hoechstens.Ticks));
                _plan[k] = (jetzt + abstand, abstand);
            }
            return faellig;
        }
    }
}
