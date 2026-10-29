using Abstractions;

namespace Infrastructure.Akteure;

/// <summary>
/// Wer diese Session ist — vom Tor (Token) und/oder vom Client-Vertrag am Handshake (<c>docs/konzept-akteure.md</c> §5.2). Lebt eine Session
/// lang; der Handshake kann den Akteur erst festlegen (ohne Tor sagt der Vertrag, wer da ist).
/// </summary>
public sealed class AkteurSitzung
{
    public AkteurSitzung(AkteurRechte? akteur) => Akteur = akteur;

    /// <summary>Die Befugnisse der Session (Tor: aus dem Token; ohne Tor: aus dem Vertrag; sonst null = offen).</summary>
    public AkteurRechte? Akteur { get; set; }

    /// <summary>Der angenommene Client-Vertrag (null = der Client hat keinen genannt).</summary>
    public AkteurRechte? Vertrag { get; set; }
}

/// <summary>
/// „Ich bin Client X" — der Handshake eines Clients, der gegen seinen Client-Vertrag programmiert (<c>docs/konzept-akteure.md</c> §4/§5.2).
/// Reine Logik (kein gRPC, kein Cluster): der <c>CqrsClientServiceImpl</c> ruft sie nur auf.
/// </summary>
public static class ClientVertragsPruefung
{
    /// <param name="Vertrag">die wirksamen Befugnisse (Vertrag ∩ Token; null = kein Vertrag genannt oder abgelehnt)</param>
    /// <param name="Ablehnung">warum der Handshake abgelehnt wird (null = angenommen)</param>
    /// <param name="Warnung">Hinweis bei angenommenem Vertrag (z. B. abweichender Hash im nicht-strengen Modus)</param>
    /// <param name="Client">der Client-Vertrag</param>
    public sealed record Ergebnis(AkteurRechte? Vertrag, string? Ablehnung, string? Warnung, ClientVertrag? Client = null);

    /// <summary>
    /// Prüft den genannten Client: er muss in der generierten Tabelle stehen; wirksam ist Vertrag ∩ Akteure des Tokens (ohne Tor alle verkörperten
    /// Akteure); ein abweichender Hash (Client gegen einen anderen Stand gebaut) ist eine Warnung — im strengen Modus eine Ablehnung.
    /// </summary>
    public static Ergebnis Pruefe(string? vertrag, string? hash, AkteurRechte? token,
        IReadOnlyDictionary<string, AkteurRechte> rechte, bool streng, IReadOnlyDictionary<string, ClientVertrag> clients)
    {
        if (string.IsNullOrEmpty(vertrag)) return new(null, null, null);
        if (!clients.TryGetValue(vertrag, out var client))
            return new(null, $"Client '{vertrag}' ist unbekannt — kein Client-Vertrag (IClientVertrag) dieses Namens", null);
        if (client.Rechte(rechte, token) is not { } wirksam)
            return new(null, $"das Token verkörpert {token?.Name} — keinen der Akteure des Clients {vertrag} ({string.Join(", ", client.Verkoerpert)})", null);
        if (!string.Equals(hash ?? "", client.Hash, StringComparison.Ordinal))
        {
            var text = $"Client-Hash '{hash}' ≠ Server '{client.Hash}' ({client.Typ.Name}) — der Client ist gegen einen anderen "
                       + "Domänen-Stand gebaut (./codegen.sh --force, Client neu starten)";
            return streng ? new(null, text, null) : new(wirksam, null, text, client);
        }
        return new(wirksam, null, null, client);
    }

    /// <summary>
    /// Wendet den Vertrag auf ein Handshake-Ergebnis an — Befugnis statt Selbstauskunft: Commands/Queries wie beim Tor
    /// (<see cref="AkteurTor.Wende"/>), abonniert wird GENAU, was der Vertrag hört. Rückgabe: die Abweichungen der Selbstauskunft vom Vertrag
    /// (fürs Log) und das Verweigerte.
    /// </summary>
    public static IReadOnlyList<string> Wende(CapabilitiesResult ergebnis, AkteurRechte vertrag,
        Func<string, Type?> loese)
    {
        var soll = vertrag.Hoert.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var abweichung = new List<string>();
        // Nur verglichen, wenn der Client überhaupt eine Selbstauskunft schickt — ein generierter Client nennt nur den Vertrag.
        if (ergebnis.SubscribedEvents.Count > 0)
        {
            abweichung.AddRange(ergebnis.SubscribedEvents.Where(n => n != nameof(CommandFailed) && !soll.Contains(n)).Select(n => $"hört laut Vertrag nicht: {n}"));
            abweichung.AddRange(soll.Where(n => !ergebnis.SubscribedEvents.Contains(n)).Select(n => $"Selbstauskunft ohne Vertrags-Eingang: {n}"));
        }
        abweichung.AddRange(AkteurTor.Wende(ergebnis, vertrag, loese).Where(v => !v.StartsWith("hören:", StringComparison.Ordinal)));
        ergebnis.SubscribedEvents.Clear();
        ergebnis.SubscribedEvents.AddRange(soll);
        return abweichung;
    }
}
