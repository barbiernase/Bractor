using System.Security.Cryptography;
using System.Text;

namespace Abstractions;

/// <summary>
/// Woher der Eingang eines Bausteins kommt (Plan Pub/Sub-Neustrukturierung §3.1, §3.3): der Log-Eintrag — Stream, Version, Log-Zeit —, der
/// Akteur der Kette und die Ableitung neuer Ids. Ein Baustein bekommt sie als Parameter (<c>Auf(ZahlungUeberfaellig e, Herkunft h)</c>), nie über
/// eine Uhr oder einen Zufall: so rechnet derselbe Eingang beim zweiten Mal gleich (R11, R12), und eine neue Id ist beim Wiederholen dieselbe (R13).
/// </summary>
public sealed class Herkunft
{
    private readonly string _bereich;

    /// <param name="stream">Der Stream des Eingangs.</param>
    /// <param name="version">Seine Version im Stream.</param>
    /// <param name="zeit">Die Log-Zeit des Eingangs (die Datenbank-Uhr beim Schreiben), nie die Uhr des Hosts.</param>
    /// <param name="akteur">Der Akteur der Kette (Header <c>akteur</c>), oder null.</param>
    /// <param name="bereich">Wer ableitet (der Wirker, etwa <c>funktion:Mahnwesen</c>) — zwei Bausteine leiten aus demselben Eingang verschiedene Ids ab.</param>
    public Herkunft(Guid stream, int version, DateTimeOffset zeit, string? akteur, string bereich)
    {
        Stream = stream;
        Version = version;
        Zeit = zeit;
        Akteur = akteur;
        _bereich = bereich ?? throw new ArgumentNullException(nameof(bereich));
    }

    public Guid Stream { get; }
    public int Version { get; }
    public DateTimeOffset Zeit { get; }
    public string? Akteur { get; }

    /// <summary>Der Stream des Eingangs als Id des Aggregats <typeparamref name="T"/> — für ein Event, das <typeparamref name="T"/> entschieden hat
    /// (in einer Projektion: das Read-Model dieses Aggregats).</summary>
    public Id<T> Id<T>() where T : IState => new(Stream);

    /// <summary>
    /// Eine neue Id, abgeleitet aus Baustein, Eingang, Ziel-Typ und <paramref name="zweck"/> — derselbe Eingang ergibt beim Wiederholen dieselbe Id
    /// (statt <c>Guid.NewGuid()</c>, R13). Mehrere neue Ids desselben Typs aus einem Eingang unterscheidet der Zweck (etwa die Zeilennummer).
    /// </summary>
    public Id<T> NeueId<T>(string zweck = "") where T : IState
        => new(Ableite($"neueid:{_bereich}:{Stream:N}:{Version}:{typeof(T).FullName}:{zweck}"));

    /// <inheritdoc cref="NeueId{T}(string)"/>
    public Id<T> NeueId<T>(long nummer) where T : IState => NeueId<T>(nummer.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public override string ToString() => $"{Stream}:{Version}";

    /// <summary>Eine deterministische Id aus einem Text (MD5, wie die übrigen abgeleiteten Ids des Frameworks).</summary>
    public static Guid Ableite(string text)
    {
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(Encoding.UTF8.GetBytes(text), hash);
        return new Guid(hash);
    }
}
