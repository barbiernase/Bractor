using System.Security.Cryptography;
using System.Text;

namespace Abstractions;

/// <summary>
/// Deterministische Identitäten der Prozess-Maschinerie (Spec 11.3). Determinismus TRÄGT die
/// Korrektheit: keine Zufalls-Ids, sodass Wiederholungen (doppeltes Signal, Poll, Crash-nach-Send)
/// als Duplikate erkennbar sind und verpuffen.
///
/// MD5 dient nur als stabile, gut streuende Ableitung — keine kryptografische Eigenschaft nötig
/// (identisch zur Begründung bei <see cref="ReaktionsId"/>).
/// </summary>
public static class ProzessId
{
    /// <summary>
    /// ProzessId = deterministisch aus (PlanTyp, auslösender StreamId, Version). Jede Wiederholung des
    /// Starts erzeugt DIESELBE Id → das Prozess-Aggregat antwortet beim zweiten <c>StarteProzess</c> mit
    /// Noop. Der Plan-Typ gehört in die Id, damit DASSELBE Event mehrere verschiedene Prozesse
    /// kollisionsfrei starten kann.
    /// </summary>
    public static Guid Für(string planTyp, Guid ausloeserStreamId, int ausloeserVersion)
        => Hash($"prozess:{planTyp}:{ausloeserStreamId:N}:{ausloeserVersion}");

    /// <summary>
    /// Vorgang-Id einer gefeuerten TRANSITION im Event-Regel-Modell (Spec §6): deterministisch aus der
    /// Kausalität <c>(Korrelation, auslösender Token-Stream, dessen Version, Command-Typ, Diskriminator)</c>.
    /// Deterministisch UND eindeutig pro Auslöser — so löst sich Fan-out ohne Zähler: N Commands desselben
    /// Typs aus DEMSELBEN Match bekommen über den Diskriminator (das Ziel-Aggregat) verschiedene Vorgänge,
    /// während ein Re-Feuern nach Crash exakt denselben Vorgang trifft (das Ziel dedupliziert).
    ///
    /// Der Diskriminator ist normalerweise die Ziel-<c>AggregateId</c> des Commands; er trennt parallele
    /// Fan-out-Kinder, ist aber bei einem Einzel-Command bedeutungslos-stabil.
    /// </summary>
    public static Guid FürTransition(
        Guid korrelation, Guid auslöserStreamId, int auslöserVersion, string commandTyp, string diskriminator)
        => Hash($"transition:{korrelation:N}:{auslöserStreamId:N}:{auslöserVersion}:{commandTyp}:{diskriminator}");

    /// <summary>
    /// Vorgang-Id eines KOMPENSATIONS-Commands — eigener Namensraum-Präfix, damit Gegenzug und
    /// Vorwärts-Transition derselben Kausalität nie kollidieren.
    /// </summary>
    public static Guid FürKompensation(
        Guid korrelation, Guid auslöserStreamId, int auslöserVersion, string commandTyp, string diskriminator)
        => Hash($"kompensation:{korrelation:N}:{auslöserStreamId:N}:{auslöserVersion}:{commandTyp}:{diskriminator}");

    private static Guid Hash(string input)
    {
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(Encoding.UTF8.GetBytes(input), hash);
        return new Guid(hash);
    }
}
