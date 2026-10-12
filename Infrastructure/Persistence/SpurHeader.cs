using Abstractions;

namespace Infrastructure.Persistence;

/// <summary>
/// Schreibt und liest die Spur-Header (<see cref="Spur"/>: <c>ursache</c>, <c>wirker</c>, <c>stand</c>) — an EINER Stelle für den
/// Einzel-Append (<see cref="MartenEventStore"/>, je Session) und den Group-Commit (<see cref="MartenEventBatchWriter"/>, je Event).
/// Fehlt ein Wert, wird nichts geschrieben.
/// </summary>
internal static class SpurHeader
{
    public static void Setze(Marten.IDocumentSession session, Spur.Kopf? kopf) => Setze(session.SetHeader, kopf);

    public static void Setze(JasperFx.Events.IEvent ev, Spur.Kopf? kopf) => Setze(ev.SetHeader, kopf);

    private static void Setze(Action<string, object> setze, Spur.Kopf? kopf)
    {
        if (!string.IsNullOrEmpty(kopf?.Ursache)) setze(Spur.UrsacheHeader, kopf.Ursache!);
        if (!string.IsNullOrEmpty(kopf?.Wirker)) setze(Spur.WirkerHeader, kopf.Wirker!);
        if (!string.IsNullOrEmpty(Spur.Stand)) setze(Spur.StandHeader, Spur.Stand!);
    }

    public static (string? Ursache, string? Wirker, string? Stand) Lies(IReadOnlyDictionary<string, object>? headers)
    {
        if (headers == null) return (null, null, null);
        string? W(string k) => headers.TryGetValue(k, out var v) ? v?.ToString() : null;
        return (W(Spur.UrsacheHeader), W(Spur.WirkerHeader), W(Spur.StandHeader));
    }
}
