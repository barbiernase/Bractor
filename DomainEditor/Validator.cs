namespace DomainEditor;

/// <summary>Ein Modell-Befund: Schweregrad, Code, lesbare Meldung. Spiegelt die Graph-Diagnostik.</summary>
public sealed record Befund(string Schweregrad, string Code, string Meldung)
{
    public bool IstFehler => Schweregrad == "error";
    public override string ToString() => $"[{Schweregrad}] {Code}: {Meldung}";
}

/// <summary>
/// Strukturelle Guardrails auf dem record-zentrischen Modell — die FORM-Regeln, die auch
/// Compiler/Analyzer erzwingen, nur schon beim Modellieren. Leichtgewichtig; die volle Wahrheit
/// bleibt Compiler + Analyzer + Debugger.
/// </summary>
public static class Validator
{
    private static readonly HashSet<string> Framework = new(StringComparer.Ordinal)
    {
        "Guid", "decimal", "int", "long", "double", "float", "bool", "string",
        "DateTime", "DateTimeOffset", "TimeSpan", "byte", "short", "object",
    };

    public static IReadOnlyList<Befund> Prüfe(EditorModell modell)
    {
        var befunde = new List<Befund>();

        var commandNamen = new HashSet<string>(modell.Records.Where(r => r.Kind == RecordArt.Command).Select(r => r.Name), StringComparer.Ordinal);
        var eventNamen = new HashSet<string>(modell.Records.Where(r => r.Kind is RecordArt.Event or RecordArt.Rejection).Select(r => r.Name), StringComparer.Ordinal);
        var persistentEvents = new HashSet<string>(modell.Records.Where(r => r.Kind == RecordArt.Event).Select(r => r.Name), StringComparer.Ordinal);

        // Bekannte Typen: Framework + alle Records + alle Enums.
        var bekannt = new HashSet<string>(Framework, StringComparer.Ordinal);
        foreach (var r in modell.Records) bekannt.Add(r.Name);
        foreach (var e in modell.Enums) bekannt.Add(e.Name);

        // Doppelte Record-Namen.
        foreach (var g in modell.Records.GroupBy(r => r.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            befunde.Add(new("error", "EDIT-DUP-RECORD", $"Record '{g.Key}' ist {g.Count()}× definiert — Namen müssen eindeutig sein."));

        // Feldtypen prüfen (nur Hinweis bei unbekannt — reiche Typen sind erlaubt).
        foreach (var r in modell.Records)
        {
            var erstes = r.Felder.FirstOrDefault();
            if (r.Kind == RecordArt.Command && (erstes is null || erstes.Name != "AggregateId" || erstes.Typ != "Guid"))
                befunde.Add(new("warning", "EDIT-CMD-ID", $"Command '{r.Name}': erstes Feld sollte 'Guid AggregateId' sein (ICommand-Konvention)."));

            foreach (var f in r.Felder)
                if (!TypBekannt(f.Typ, bekannt))
                    befunde.Add(new("info", "EDIT-TYP-UNBEKANNT", $"{r.Name}.{f.Name}: Typ '{f.Typ}' ist kein Skalar/Record/Enum der Domäne — kompiliert nur, wenn der Typ existiert."));
        }

        // Aggregat-Komposition.
        foreach (var agg in modell.Aggregate)
        {
            foreach (var d in agg.Decider)
            {
                if (!commandNamen.Contains(d.Command))
                    befunde.Add(new("error", "EDIT-DECIDE-CMD", $"{agg.Name}.Decide: '{d.Command}' ist kein Command-Record."));
                if (d.Ergibt.Count is 0)
                    befunde.Add(new("error", "EDIT-ONEOF-LEER", $"{agg.Name}.Decide({d.Command}) hat keinen Ausgang — mindestens ein Event nötig."));
                else if (d.Ergibt.Count > 5)
                    befunde.Add(new("error", "EDIT-ONEOF-5", $"{agg.Name}.Decide({d.Command}) hat {d.Ergibt.Count} Ausgänge — OneOf erlaubt höchstens 5."));
                foreach (var a in d.Ergibt)
                    if (!eventNamen.Contains(a.Event))
                        befunde.Add(new("error", "EDIT-EVENT-FEHLT", $"{agg.Name}.Decide({d.Command}) → '{a.Event}' ist kein Event/Ablehnungs-Record."));
            }
            foreach (var a in agg.Applier)
                if (!persistentEvents.Contains(a.Event))
                    befunde.Add(new("error", "EDIT-APPLY-EVENT", $"{agg.Name}.Apply: '{a.Event}' ist kein persistenter Event-Record."));
        }

        // Sagas.
        foreach (var saga in modell.Sagas)
        {
            if (!eventNamen.Contains(saga.TriggerEvent))
                befunde.Add(new("warning", "EDIT-SAGA-TRIGGER", $"Saga {saga.Name}: Trigger '{saga.TriggerEvent}' ist kein bekanntes Event."));
            foreach (var s in saga.Schritte)
            {
                if (s.Wenn.Count is 0)
                    befunde.Add(new("error", "EDIT-SAGA-WENN", $"Saga {saga.Name}: eine Transition ohne Auf/Wenn-Event."));
                else if (s.Wenn.Count > 3)
                    befunde.Add(new("error", "EDIT-SAGA-JOIN3", $"Saga {saga.Name}: Join über {s.Wenn.Count} Events — Und erlaubt höchstens 3."));
                if (!commandNamen.Contains(s.Sende))
                    befunde.Add(new("error", "EDIT-UNROUTED-SAGA-CMD", $"Saga {saga.Name} sendet '{s.Sende}', der kein Command-Record ist (Runtime-Hang)."));
                if (s.Kompensation is not null && !commandNamen.Contains(s.Kompensation))
                    befunde.Add(new("error", "EDIT-UNROUTED-SAGA-CMD", $"Saga {saga.Name} kompensiert mit '{s.Kompensation}', der kein Command-Record ist."));
            }
        }

        return befunde;
    }

    /// <summary>Basistyp bekannt? Streift <c>?</c> und Collection-Wrapper (List&lt;X&gt; …) ab.</summary>
    private static bool TypBekannt(string typ, HashSet<string> bekannt)
    {
        var t = typ.Trim().TrimEnd('?').Trim();
        var lt = t.IndexOf('<');
        if (lt >= 0 && t.EndsWith(">"))
        {
            var inner = t[(lt + 1)..^1];
            // Nur den (letzten) Typ-Parameter prüfen — reicht für List<X>/IReadOnlyList<X>.
            var arg = inner.Split(',').Last().Trim();
            return TypBekannt(arg, bekannt);
        }
        return bekannt.Contains(t);
    }
}
