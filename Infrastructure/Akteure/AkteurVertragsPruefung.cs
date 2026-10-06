using Abstractions;
using Infrastructure.Aggregate;

namespace Infrastructure.Akteure;

/// <summary>
/// Wer diese Session ist — vom Tor (Token) und/oder vom Vertrag am Handshake (<c>docs/konzept-akteure.md</c> §5.2). Lebt eine Session
/// lang; der Handshake kann den Akteur erst festlegen (ohne Tor sagt der Vertrag, wer da ist).
/// </summary>
public sealed class AkteurSitzung
{
    public AkteurSitzung(AkteurRechte? akteur) => Akteur = akteur;

    /// <summary>Die Befugnisse der Session (Tor: aus dem Token; ohne Tor: aus dem Vertrag; sonst null = offen).</summary>
    public AkteurRechte? Akteur { get; set; }

    /// <summary>Der angenommene Vertrag (null = der Client hat keinen genannt).</summary>
    public AkteurRechte? Vertrag { get; set; }
}

/// <summary>
/// „Ich bin Vertrag X" — der Handshake eines Clients, der gegen einen Akteur-Vertrag programmiert (<c>docs/konzept-akteure.md</c> §5.2/5.3).
/// Reine Logik (kein gRPC, kein Cluster): der <c>CqrsClientServiceImpl</c> ruft sie nur auf.
/// </summary>
public static class AkteurVertragsPruefung
{
    /// <param name="Vertrag">die Befugnisse des Vertrags-Akteurs (null = kein Vertrag genannt oder abgelehnt)</param>
    /// <param name="Ablehnung">warum der Handshake abgelehnt wird (null = angenommen)</param>
    /// <param name="Warnung">Hinweis bei angenommenem Vertrag (z. B. abweichender Hash im nicht-strengen Modus)</param>
    /// <param name="Client">der Client-Vertrag, wenn der Name einen Client nennt (<c>docs/konzept-akteure.md</c> §4) — dann ist
    /// <paramref name="Vertrag"/> schon die Schnittmenge Vertrag ∩ Token und gilt als Befugnis der ganzen Session.</param>
    public sealed record Ergebnis(AkteurRechte? Vertrag, string? Ablehnung, string? Warnung, ClientVertrag? Client = null);

    /// <summary>
    /// Prüft den genannten Vertrag: er muss in der generierten Tabelle stehen und einen Vertrag haben; mit Tor muss das Token den
    /// Vertrags-Akteur verkörpern; ein abweichender Hash (Client gegen einen anderen Stand gebaut) ist eine Warnung — im strengen
    /// Modus eine Ablehnung.
    /// </summary>
    public static Ergebnis Pruefe(string? vertrag, string? hash, AkteurRechte? token,
        IReadOnlyDictionary<string, AkteurRechte> rechte, bool streng, IReadOnlyDictionary<string, ClientVertrag>? clients = null)
    {
        if (string.IsNullOrEmpty(vertrag)) return new(null, null, null);
        // Client-Vertrag (docs/konzept-akteure.md §4): wirksam ist Vertrag ∩ Akteure des Tokens; ohne Tor alle verkörperten Akteure.
        if (clients != null && clients.TryGetValue(vertrag, out var client))
        {
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
        if (!rechte.TryGetValue(vertrag, out var r) || r.VertragTyp is null)
            return new(null, $"Vertrag '{vertrag}' ist unbekannt — kein Client (IClientVertrag) und kein Akteur mit IAkteurVertrag<…> dieses Namens", null);
        if (token is not null && token.Name != vertrag && token.Teile.All(t => t.Name != vertrag))
            return new(null, $"das Token verkörpert {token.Name}, nicht den Vertrags-Akteur {vertrag}", null);
        if (!string.Equals(hash ?? "", r.VertragHash, StringComparison.Ordinal))
        {
            var text = $"Vertrags-Hash des Clients '{hash}' ≠ Server '{r.VertragHash}' ({r.VertragTyp.Name}) — der Client ist gegen einen anderen "
                       + "Domänen-Stand gebaut (./codegen.sh --force, Worker neu starten)";
            return streng ? new(null, text, null) : new(r, null, text);
        }
        return new(r, null, null);
    }

    /// <summary>
    /// Wendet den Vertrag auf ein Handshake-Ergebnis an — Befugnis statt Selbstauskunft: Commands/Queries/Trigger wie beim Tor
    /// (<see cref="AkteurTor.Wende"/>), abonniert wird GENAU das, worauf der Vertrag reagiert. Rückgabe: die Abweichungen der
    /// Selbstauskunft vom Vertrag (fürs Log) und das Verweigerte.
    /// </summary>
    public static IReadOnlyList<string> Wende(CapabilitiesResult ergebnis, AkteurRechte vertrag,
        Func<string, Type?> loese, Func<Type, bool> internBedient, Func<string, bool> schonVergeben)
    {
        var soll = vertrag.Vertrag.Keys.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var abweichung = new List<string>();
        abweichung.AddRange(ergebnis.SubscribedEvents.Where(n => n != nameof(CommandFailed) && !soll.Contains(n)).Select(n => $"hört laut Vertrag nicht: {n}"));
        abweichung.AddRange(soll.Where(n => !ergebnis.SubscribedEvents.Contains(n)).Select(n => $"Selbstauskunft ohne Vertrags-Eingang: {n}"));
        abweichung.AddRange(AkteurTor.Wende(ergebnis, vertrag, loese, internBedient, schonVergeben).Where(v => !v.StartsWith("hören:", StringComparison.Ordinal)));
        ergebnis.SubscribedEvents.Clear();
        ergebnis.SubscribedEvents.AddRange(soll);
        return abweichung;
    }

    /// <summary>
    /// Die deterministische CommandId einer Zusage von außen (Akteur-Konzept §5.3): gleiche Ursache (Event an Stream + Version) und gleiche
    /// Stelle in der Zusage (Index, Command-Typ) → gleiche Id → die Inbox des Ziels dedupliziert. Dieselbe Ableitung wie
    /// beim internen Emit (<see cref="EmitId"/>).
    /// </summary>
    public static Guid CommandId(string? korrelation, Guid stream, int version, string ausloeser, int index, Type command, Guid ziel) =>
        EmitId.Ableiten(new EmitKausalität(Guid.TryParse(korrelation, out var k) ? k : Guid.Empty, stream,
            $"{version}:{ausloeser}#{index}:{command.Name}"), ziel);
}
