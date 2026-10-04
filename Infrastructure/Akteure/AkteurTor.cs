namespace Infrastructure.Akteure;

/// <summary>
/// Das Tor am gRPC-Handshake: wer sich anmeldet, wird EINEM Akteur zugeordnet; seine Session bekommt dessen
/// Befugnisse (<see cref="AkteurRechte"/>), und jede hineingehende Nachricht wird dagegen geprüft.
/// Reine Logik (kein gRPC, kein Cluster) — der <c>CqrsClientServiceImpl</c> ruft sie nur auf.
/// </summary>
public sealed class AkteurTor
{
    private readonly AkteurOptionen _optionen;
    private readonly IReadOnlyDictionary<string, AkteurRechte> _rechte;

    public AkteurTor(AkteurOptionen optionen, IReadOnlyDictionary<string, AkteurRechte> rechte)
    {
        _optionen = optionen;
        _rechte = rechte;
    }

    /// <summary>
    /// Ordnet ein (evtl. fehlendes) Token einem Akteur zu. <c>null</c> = abweisen (kein gültiges Token und kein
    /// Standard-Akteur konfiguriert).
    /// </summary>
    public AkteurRechte? Erkenne(string? token)
    {
        if (!string.IsNullOrEmpty(token) && _optionen.Tokens.TryGetValue(token, out var name))
            return _rechte[name];
        return _optionen.Standard is { } standard ? _rechte[standard] : null;
    }

    /// <summary>
    /// Wendet die Befugnisse auf ein Handshake-Ergebnis an. Statt Selbstauskunft gilt:
    /// <list type="bullet">
    /// <item>erlaubte Commands/Queries/Trigger = genau die <c>IDarf</c>-Mengen des Akteurs;</item>
    /// <item>abonnierte Events = Wunsch ∩ abgeleitete Hör-Menge;</item>
    /// <item>Zuständigkeit (beantworten/verarbeiten) nur, wenn der Akteur den Typ darf, das System ihn NICHT selbst
    ///   bedient (Lücke im Graphen) und noch kein anderer zuständig ist (Kardinalität eins).</item>
    /// </list>
    /// Rückgabe: das Verweigerte (fürs Log; der Client sieht es an den fehlenden Einträgen der Antwort).
    /// </summary>
    public static IReadOnlyList<string> Wende(
        CapabilitiesResult ergebnis,
        AkteurRechte akteur,
        Func<string, Type?> loese,
        Func<Type, bool> internBedient,
        Func<string, bool> schonVergeben)
    {
        var verweigert = new List<string>();

        static IEnumerable<string> Namen(IEnumerable<Type> typen) => typen.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal);

        Ersetze(ergebnis.AllowedCommands, Namen(akteur.Commands));
        Ersetze(ergebnis.AllowedQueries, Namen(akteur.Queries));
        Ersetze(ergebnis.AllowedTriggers, Namen(akteur.Trigger));

        Behalte(ergebnis.SubscribedEvents, n => loese(n) is { } t && akteur.DarfHoeren(t), "hören", verweigert);
        Behalte(ergebnis.HandlingQueries, n => DarfUebernehmen(n), "beantworten", verweigert);
        Behalte(ergebnis.HandlingTriggers, n => DarfUebernehmen(n), "verarbeiten", verweigert);
        return verweigert;

        bool DarfUebernehmen(string name) =>
            loese(name) is { } t && akteur.DarfHinein(t) && !internBedient(t) && !schonVergeben(name);
    }

    /// <summary>Darf diese Session <paramref name="typ"/> hineingeben? Ohne Akteur (Tor aus) immer ja.</summary>
    public static bool Darf(AkteurRechte? akteur, Type typ) => akteur is null || akteur.DarfHinein(typ);

    /// <summary>Darf diese Session das Event <paramref name="typ"/> abonnieren? Ohne Akteur (Tor aus) immer ja.</summary>
    public static bool DarfHoeren(AkteurRechte? akteur, Type typ) => akteur is null || akteur.DarfHoeren(typ);

    private static void Ersetze(List<string> ziel, IEnumerable<string> neu)
    {
        ziel.Clear();
        ziel.AddRange(neu);
    }

    private static void Behalte(List<string> liste, Func<string, bool> erlaubt, string was, List<string> verweigert)
    {
        foreach (var n in liste.Where(n => !erlaubt(n)).ToList())
        {
            liste.Remove(n);
            verweigert.Add($"{was}: {n}");
        }
    }
}

public static class AkteurVerweigerung
{
    /// <summary>
    /// Das targeted <see cref="Abstractions.CommandFailed"/> für einen Command, den der Akteur der Session nicht darf —
    /// derselbe Rückkanal wie „nicht zugestellt" (der Client kennt ihn schon). <c>null</c> ohne <c>OriginSessionId</c>.
    /// </summary>
    public static Abstractions.EventEnvelope? Baue(Abstractions.CommandEnvelope envelope, string akteur)
    {
        if (string.IsNullOrEmpty(envelope.OriginSessionId))
            return null;

        var command = envelope.Payload.GetType().Name;
        return new Abstractions.EventEnvelope
        {
            AggregateId = envelope.AggregateId,
            AggregateType = envelope.AggregateType,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CommandId.ToString(),
            UserId = envelope.UserId,
            Payload = new Abstractions.CommandFailed(command, $"Akteur '{akteur}' darf {command} nicht", envelope.AggregateId.ToString()),
            TargetSubscriberId = envelope.OriginSessionId,
        };
    }
}
