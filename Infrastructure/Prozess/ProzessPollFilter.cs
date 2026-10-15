using System;
using System.Collections.Generic;
using Abstractions;

namespace Infrastructure.Prozess;

/// <summary>Eine Weckung aus dem Prozess-Poll: der Manager der Korrelation, beim Start mit dem Prozess-Namen (wie <see cref="ProzessWake"/>).</summary>
public sealed record ProzessPollWeckung(Guid Korrelation, Guid Stream, int Version, string? ProzessNameFürStart);

/// <summary>
/// Die Entscheidung des Prozess-Polls (PF-4), welches Event welchen Manager weckt. Reine Funktion → Ebene-1-beweisbar.
///
/// Ein Event eines Auslöser-Typs startet seinen Vorgang (Korrelation deterministisch aus Prozess, Stream, Version) — aber nur, wenn es den
/// Vorgang noch nicht gibt oder er offen ist: ein beendeter braucht keine Weckung (nach einem Neustart ab 0 sonst eine Weckung je Vorgang
/// der Historie). Jedes andere Event weckt nur, wenn seine <c>CorrelationId</c> zu einem OFFENEN Vorgang gehört — der Manager stempelt sie
/// beim Feuern in die Ziel-Events, so auch in das terminale Ergebnis, das Auslöser keiner Regel ist (Befund 2). Ein fremdes Event
/// (Client-Korrelation, beendeter Vorgang) weckt niemanden. Was der Poll so übersieht (ein Vorgang, der während der Seite erst offen wurde),
/// heilt der Backstop.
/// </summary>
public static class ProzessPollFilter
{
    public static ProzessPollWeckung? Weckung(
        EventEnvelope env, IReadOnlyDictionary<Type, string> auslöserZuProzess, IReadOnlySet<Guid> offene)
    {
        if (auslöserZuProzess.TryGetValue(env.Payload.GetType(), out var prozess))
            return new ProzessPollWeckung(ProzessId.Für(prozess, env.AggregateId, env.AggregateVersion), env.AggregateId, env.AggregateVersion, prozess);
        return Guid.TryParse(env.CorrelationId, out var korr) && offene.Contains(korr)
            ? new ProzessPollWeckung(korr, env.AggregateId, env.AggregateVersion, null)
            : null;
    }

    /// <summary>Ein Start weckt, wenn der Vorgang offen ist oder es seinen Stream noch nicht gibt.</summary>
    public static bool StartNoetig(Guid korrelation, IReadOnlySet<Guid> offene, IReadOnlySet<Guid> vorhanden)
        => offene.Contains(korrelation) || !vorhanden.Contains(korrelation);
}
