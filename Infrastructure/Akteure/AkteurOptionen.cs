using Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Akteure;

/// <summary>
/// Composition-Root-Abbildung „wer meldet sich an → welcher Akteur". Die EINZIGE String-Stelle des Akteur-Konzepts
/// — sie gehört zum Betrieb, nicht zur Domäne. Ohne <see cref="AkteurExtensions.AddAkteure(IServiceCollection, Action{AkteurOptionen})"/>
/// bleibt der gRPC-Pfad unverändert offen (opt-in).
///
/// Identität ist hier ein Token im gRPC-Header <see cref="TokenHeader"/>. Das ist bewusst KEINE vollständige
/// Authentifizierung (TLS/OIDC/mTLS: <c>docs/konzept-client-haertung.md</c> T1) — nur die Naht, an der sie andockt.
/// </summary>
public sealed class AkteurOptionen
{
    /// <summary>gRPC-Request-Header, der das Akteur-Token trägt.</summary>
    public const string TokenHeader = "akteur-token";

    private readonly Dictionary<string, string> _tokens = new(StringComparer.Ordinal);

    /// <summary>Token → Akteur-Name (einfacher Typname, wie in <see cref="GeneratedAkteurRechte"/>).</summary>
    public IReadOnlyDictionary<string, string> Tokens => _tokens;

    /// <summary>Akteur für Verbindungen ohne/mit unbekanntem Token; <c>null</c> = solche Verbindungen werden abgewiesen.</summary>
    public string? Standard { get; private set; }

    /// <summary>Typisiert: wer <paramref name="token"/> vorzeigt, ist <typeparamref name="TAkteur"/>.</summary>
    public AkteurOptionen Token<TAkteur>(string token) where TAkteur : IAkteur
        => Token(token, typeof(TAkteur).Name);

    /// <summary>Aus der Konfiguration (Akteur-Name als String) — wird beim Start gegen die generierte Tabelle geprüft.</summary>
    public AkteurOptionen Token(string token, string akteur)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Leeres Akteur-Token.", nameof(token));
        _tokens[token] = akteur;
        return this;
    }

    /// <summary>Verbindungen ohne gültiges Token gelten als <typeparamref name="TAkteur"/> (statt abgewiesen).</summary>
    public AkteurOptionen Standardmaessig<TAkteur>() where TAkteur : IAkteur
        => Standardmaessig(typeof(TAkteur).Name);

    public AkteurOptionen Standardmaessig(string akteur)
    {
        Standard = akteur;
        return this;
    }

    /// <summary>
    /// Fail-fast beim Start: jeder genannte Akteur muss in der generierten Tabelle stehen (sonst Tippfehler in der
    /// Konfiguration → zur Laufzeit stilles Abweisen).
    /// </summary>
    internal void Pruefe(IReadOnlyDictionary<string, AkteurRechte> bekannt)
    {
        var unbekannt = _tokens.Values.Append(Standard).OfType<string>().Where(a => !bekannt.ContainsKey(a)).Distinct().ToList();
        if (unbekannt.Count > 0)
            throw new InvalidOperationException(
                $"Unbekannte Akteure in der Konfiguration: {string.Join(", ", unbekannt)}. "
                + $"Bekannt (aus IAkteur-Typen generiert): {string.Join(", ", bekannt.Keys.OrderBy(k => k))}.");
    }
}

public static class AkteurExtensions
{
    /// <summary>Schaltet die Akteur-Befugnisse am gRPC-Handshake ein.</summary>
    public static IServiceCollection AddAkteure(this IServiceCollection services, Action<AkteurOptionen> konfiguriere)
    {
        var optionen = new AkteurOptionen();
        konfiguriere(optionen);
        optionen.Pruefe(GeneratedAkteurRechte.Alle);
        services.AddSingleton(new AkteurTor(optionen, GeneratedAkteurRechte.Alle));
        return services;
    }

    /// <summary>
    /// Aus einer Konfigurations-Sektion (<c>"Akteure": { "Tokens": { "&lt;token&gt;": "Disponent" }, "Standard": "Gast" }</c>).
    /// Fehlt die Sektion, passiert nichts — der Pfad bleibt offen wie bisher.
    /// </summary>
    public static IServiceCollection AddAkteure(this IServiceCollection services, IConfigurationSection sektion)
    {
        if (!sektion.Exists()) return services;
        return services.AddAkteure(o =>
        {
            foreach (var t in sektion.GetSection("Tokens").GetChildren())
                if (t.Value is { } akteur) o.Token(t.Key, akteur);
            if (sektion["Standard"] is { Length: > 0 } standard) o.Standardmaessig(standard);
        });
    }
}
