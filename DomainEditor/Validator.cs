namespace DomainEditor;

/// <summary>Ein Modell-Befund: Schweregrad, Code, lesbare Meldung. Spiegelt die Graph-Diagnostik.</summary>
public sealed record Befund(string Schweregrad, string Code, string Meldung)
{
    public bool IstFehler => Schweregrad == "error";
    public override string ToString() => $"[{Schweregrad}] {Code}: {Meldung}";
}

/// <summary>
/// Strukturelle Guardrails auf dem Modell — die FORM-Regeln, die auch der Compiler/Analyzer
/// später erzwingt, nur schon beim Modellieren (Kern-Idee 3: ummauerter Garten). Bewusst
/// leichtgewichtig; die volle Wahrheit bleibt Compiler + Analyzer + Debugger.
/// </summary>
public static class Validator
{
    public static IReadOnlyList<Befund> Prüfe(EditorModell modell)
    {
        var befunde = new List<Befund>();

        // Bekannte Event-/Command-Namen für Referenz-Prüfungen sammeln.
        var events = new HashSet<string>(modell.Aggregate.SelectMany(a => a.Events).Select(e => e.Name));
        var commands = new HashSet<string>(modell.Aggregate.SelectMany(a => a.Commands).Select(c => c.Name));

        foreach (var agg in modell.Aggregate)
        {
            foreach (var f in agg.Felder.Concat(agg.Commands.SelectMany(c => c.Felder)).Concat(agg.Events.SelectMany(e => e.Felder)))
                if (!Skalar.IstErlaubt(f.Typ))
                    befunde.Add(new("error", "EDIT-TYP", $"{agg.Name}.{f.Name}: Typ '{f.Typ}' ist kein erlaubter Skalar ({string.Join("/", Skalar.Erlaubt)})."));

            foreach (var c in agg.Commands)
            {
                // OneOf kann 1–5 Typargumente (Abstractions/OneOf.cs).
                if (c.Ergibt.Count is 0)
                    befunde.Add(new("error", "EDIT-ONEOF-LEER", $"{agg.Name}.Decide({c.Name}) hat keinen Ausgang — mindestens ein Event nötig."));
                else if (c.Ergibt.Count > 5)
                    befunde.Add(new("error", "EDIT-ONEOF-5", $"{agg.Name}.Decide({c.Name}) hat {c.Ergibt.Count} Ausgänge — OneOf erlaubt höchstens 5."));

                // Erstes Command-Feld ist per Konvention Guid AggregateId.
                var erstes = c.Felder.FirstOrDefault();
                if (erstes is null || erstes.Name != "AggregateId" || erstes.Typ != "Guid")
                    befunde.Add(new("warning", "EDIT-CMD-ID", $"{agg.Name}.{c.Name}: erstes Feld sollte 'Guid AggregateId' sein (ICommand-Konvention)."));

                foreach (var a in c.Ergibt)
                    if (!events.Contains(a.Event))
                        befunde.Add(new("error", "EDIT-EVENT-FEHLT", $"{agg.Name}.Decide({c.Name}) → '{a.Event}' ist kein bekanntes Event."));
            }
        }

        foreach (var saga in modell.Sagas)
        {
            if (!events.Contains(saga.TriggerEvent))
                befunde.Add(new("warning", "EDIT-SAGA-TRIGGER", $"Saga {saga.Name}: Trigger '{saga.TriggerEvent}' ist kein bekanntes Event des Modells."));

            foreach (var s in saga.Schritte)
            {
                if (s.Wenn.Count is 0)
                    befunde.Add(new("error", "EDIT-SAGA-WENN", $"Saga {saga.Name}: eine Transition ohne Auf/Wenn-Event."));
                else if (s.Wenn.Count > 3)
                    befunde.Add(new("error", "EDIT-SAGA-JOIN3", $"Saga {saga.Name}: Join über {s.Wenn.Count} Events — Und erlaubt höchstens 3."));

                // Spiegelt CQRS002: jeder gesendete Command braucht einen Decider-Handler.
                if (!commands.Contains(s.Sende))
                    befunde.Add(new("error", "EDIT-UNROUTED-SAGA-CMD", $"Saga {saga.Name} sendet '{s.Sende}', den kein Aggregat entscheidet (Runtime-Hang)."));
                if (s.Kompensation is not null && !commands.Contains(s.Kompensation))
                    befunde.Add(new("error", "EDIT-UNROUTED-SAGA-CMD", $"Saga {saga.Name} kompensiert mit '{s.Kompensation}', den kein Aggregat entscheidet."));
            }
        }

        return befunde;
    }
}
