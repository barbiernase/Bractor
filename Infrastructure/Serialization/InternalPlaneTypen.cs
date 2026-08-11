using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;
using Infrastructure.Mapping;               // GeneratedTypeRegistry (generiert)
using Infrastructure.Projections;           // Wake, WakeAck
using Infrastructure.Prozess;               // ProzessWake
using Infrastructure.PubSub.Messages;       // Subscribe, Publish, Ack, …

namespace Infrastructure.Serialization;

/// <summary>
/// Die Typ-Registry der <b>internen Ebene</b> (Node↔Node) — der Diskriminator-Tisch, gegen den der
/// <see cref="InternalPlaneSerializer"/> beim Cross-Node-Transport auflöst (Invariante 3: Routing über
/// Typen, nie ein handgebauter Identitäts-String).
///
/// Zwei Quellen, bewusst getrennt:
/// <list type="bullet">
///   <item><b>Offene Domänen-Menge (generiert):</b> alle konkreten <see cref="ICommand"/>, <see cref="IEvent"/>,
///     <see cref="IStateChangeSignal"/>, <see cref="IPipelineTrigger"/> — gespeist aus
///     <see cref="GeneratedTypeRegistry"/> (der <c>TypeRegistryGenerator</c> zählt sie compile-time auf).
///     Wächst mit der Domäne, ohne dass hier etwas anzufassen ist — kein Drift (Invariante 4).</item>
///   <item><b>Geschlossene Framework-Menge (fest):</b> die Transport-Hüllen und Broker-Records, die die
///     Cluster-Actors über <c>RequestAsync</c>/<c>Send</c> austauschen. Ein kleiner, stabiler Satz —
///     hier explizit gelistet, vom Boot-Check (<see cref="InternalPlaneCoverage"/>) gegengeprüft.</item>
/// </list>
///
/// Der Diskriminator ist der <see cref="Type.FullName"/> — global eindeutig und über die Nodes stabil
/// (beide Enden sind dasselbe Binary, dieselben Assemblies). Kein snake_case wie beim Event-Store: die
/// interne Ebene ist flüchtiger Draht, nichts wird hier persistiert.
///
/// NICHT enthalten (bewusst): <c>CommandModus</c> (geschlossene Union) und Proto-<c>PID</c> — die tragen
/// eigene Converter im <see cref="InternalPlaneSerializer"/>, sind nie Top-Level-Nachrichten. Ebenso die
/// reinen Diagnose-Records (<c>GetStatus</c>/<c>BrokerStatus</c>, tragen einen <see cref="Type"/>) — die
/// bleiben node-lokal.
/// </summary>
public static class InternalPlaneTypen
{
    /// <summary>
    /// Die feste Framework-Menge: Transport-Hüllen + Broker-Data-Plane. Die Domänen-Payloads kommen
    /// separat aus <see cref="GeneratedTypeRegistry"/> (siehe Ctor).
    /// </summary>
    public static readonly IReadOnlyList<Type> FrameworkTypen = new[]
    {
        // Aggregat-Pfad (CommandEnvelope → CommandResult)
        typeof(CommandEnvelope),
        typeof(CommandResult),
        // Event-/Signal-Transport (Broker → Subscriber via Send; Publish-Hülle)
        typeof(EventEnvelope),
        typeof(SignalEnvelope),
        // Pull-/Prozess-Weckung
        typeof(Wake),
        typeof(WakeAck),
        typeof(ProzessWake),
        // Pipeline
        typeof(PipelineAck),
        // Broker-Data-Plane (PubSub)
        typeof(Subscribe),
        typeof(Unsubscribe),
        typeof(Publish),
        typeof(Ack),
        typeof(Activate),
        typeof(GetSubscriberCount),
        typeof(SubscriberCountResponse),
    };

    private static readonly Dictionary<string, Type> _typNachName;
    private static readonly Dictionary<Type, string> _nameNachTyp;

    static InternalPlaneTypen()
    {
        _typNachName = new Dictionary<string, Type>(StringComparer.Ordinal);
        _nameNachTyp = new Dictionary<Type, string>();

        // (1) Offene Domänen-Menge — aus dem generierten Registry (reflection-frei aufgezählt).
        var domaene = GeneratedTypeRegistry.Commands.Values
            .Concat(GeneratedTypeRegistry.Events.Values)
            .Concat(GeneratedTypeRegistry.Signals.Values)
            .Concat(GeneratedTypeRegistry.Triggers.Values);

        // (2) Geschlossene Framework-Menge.
        foreach (var t in domaene.Concat(FrameworkTypen))
            Registriere(t);
    }

    private static void Registriere(Type t)
    {
        var name = t.FullName
            ?? throw new InvalidOperationException($"Typ ohne FullName nicht registrierbar: {t}");
        if (_typNachName.TryGetValue(name, out var vorhanden))
        {
            if (vorhanden == t) return;   // idempotent (ein Typ kann in mehreren Kategorien liegen)
            throw new InvalidOperationException(
                $"Diskriminator-Kollision auf der internen Ebene: '{name}' → {vorhanden} vs. {t}");
        }
        _typNachName[name] = t;
        _nameNachTyp[t] = name;
    }

    /// <summary>Kennt der Serializer diesen Typ (ist er Teil der internen Ebene)?</summary>
    public static bool Kennt(Type t) => _nameNachTyp.ContainsKey(t);

    /// <summary>Diskriminator (FullName) eines bekannten internen Typs.</summary>
    public static string Name(Type t) =>
        _nameNachTyp.TryGetValue(t, out var n)
            ? n
            : throw new NotSupportedException(
                $"Typ nicht auf der internen Ebene registriert: {t.FullName}. " +
                "Neuer Cross-Node-Nachrichtentyp? → InternalPlaneTypen.FrameworkTypen ergänzen " +
                "(Domänen-Payloads laufen automatisch über GeneratedTypeRegistry).");

    /// <summary>Typ zu einem Diskriminator (für die Deserialisierung).</summary>
    public static Type Typ(string diskriminator) =>
        _typNachName.TryGetValue(diskriminator, out var t)
            ? t
            : throw new NotSupportedException(
                $"Unbekannter interner Diskriminator: '{diskriminator}'.");

    /// <summary>Alle abgedeckten Typen (Test-/Boot-Check-Orakel).</summary>
    public static IReadOnlyCollection<Type> AlleTypen => _nameNachTyp.Keys;
}
