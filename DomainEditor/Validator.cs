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
    public static IReadOnlyList<Befund> Prüfe(EditorModell modell)
    {
        var befunde = new List<Befund>();

        var commandNamen = new HashSet<string>(modell.Records.Where(r => r.Kind == RecordArt.Command).Select(r => r.Name), StringComparer.Ordinal);
        var eventNamen = new HashSet<string>(modell.Records.Where(r => r.Kind is RecordArt.Event or RecordArt.Rejection).Select(r => r.Name), StringComparer.Ordinal);
        var persistentEvents = new HashSet<string>(modell.Records.Where(r => r.Kind == RecordArt.Event).Select(r => r.Name), StringComparer.Ordinal);

        // Bekannte Typen: die Wire-Skalare (aus dem Codegen, via Rahmen) + alle Records + alle Enums.
        var bekannt = new HashSet<string>(modell.Rahmen.Skalare, StringComparer.Ordinal);
        foreach (var r in modell.Records) bekannt.Add(r.Name);
        foreach (var e in modell.Enums) bekannt.Add(e.Name);

        // Doppelte Record-Namen.
        foreach (var g in modell.Records.GroupBy(r => r.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            befunde.Add(new("error", "EDIT-DUP-RECORD", $"Record '{g.Key}' ist {g.Count()}× definiert — Namen müssen eindeutig sein."));

        // Feldtypen prüfen (nur Hinweis bei unbekannt — reiche Typen sind erlaubt).
        foreach (var r in modell.Records)
        {
            // ICommand verlangt die Property (Position egal — Positions-Parameter ODER Property-Feld).
            var id = modell.Rahmen.AggregatIdFeld;
            if (r.Kind == RecordArt.Command && !r.Felder.Any(f => f.Name == id && f.Typ == "Guid"))
                befunde.Add(new("error", "EDIT-CMD-ID", $"Command '{r.Name}': Feld 'Guid {id}' fehlt (ICommand verlangt es)."));

            foreach (var f in r.Felder)
                if (!TypBekannt(f.Typ, bekannt))
                    befunde.Add(new("info", "EDIT-TYP-UNBEKANNT", $"{r.Name}.{f.Name}: Typ '{f.Typ}' ist kein Skalar/Record/Enum der Domäne — kompiliert nur, wenn der Typ existiert."));
        }

        // Eigenständige Decider/Applier (verweisen auf ihr Aggregat).
        var aggNamen = new HashSet<string>(modell.Aggregate.Select(a => a.Name), StringComparer.Ordinal);
        foreach (var d in modell.Decider)
        {
            if (!aggNamen.Contains(d.Aggregat))
                befunde.Add(new("error", "EDIT-DECIDE-AGG", $"Decider({d.Command}): '{d.Aggregat}' ist kein Aggregat."));
            if (!commandNamen.Contains(d.Command))
                befunde.Add(new("error", "EDIT-DECIDE-CMD", $"Decider: '{d.Command}' ist kein Command-Record."));
            if (d.Ergibt.Count is 0)
                befunde.Add(new("error", "EDIT-ONEOF-LEER", $"Decider({d.Command}) hat keinen Ausgang — mindestens ein Event nötig."));
            else if (modell.Rahmen.OneOfMax > 0 && d.Ergibt.Count > modell.Rahmen.OneOfMax)
                befunde.Add(new("error", "EDIT-ONEOF-MAX", $"Decider({d.Command}) hat {d.Ergibt.Count} Ausgänge — OneOf gibt es im Vertrag nur bis {modell.Rahmen.OneOfMax}."));
            foreach (var a in d.Ergibt)
                if (!eventNamen.Contains(a.Event))
                    befunde.Add(new("error", "EDIT-EVENT-FEHLT", $"Decider({d.Command}) → '{a.Event}' ist kein Event/Ablehnungs-Record."));
        }
        foreach (var a in modell.Applier)
        {
            if (!aggNamen.Contains(a.Aggregat))
                befunde.Add(new("error", "EDIT-APPLY-AGG", $"Applier({a.Event}): '{a.Aggregat}' ist kein Aggregat."));
            if (!persistentEvents.Contains(a.Event))
                befunde.Add(new("error", "EDIT-APPLY-EVENT", $"Applier: '{a.Event}' ist kein persistenter Event-Record."));
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
                else if (modell.Rahmen.UndMax > 0 && s.Wenn.Count > modell.Rahmen.UndMax)
                    befunde.Add(new("error", "EDIT-SAGA-JOIN-MAX", $"Saga {saga.Name}: Join über {s.Wenn.Count} Events — die DSL trägt höchstens {modell.Rahmen.UndMax}."));
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
