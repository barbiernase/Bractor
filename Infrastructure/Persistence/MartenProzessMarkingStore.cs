using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure.Mapping;   // GeneratedTypeRegistry.PersistableEvents (reflection-frei)
using Marten;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Marten-basierter <see cref="IProzessMarkingStore"/>: ein durables <see cref="ProzessMarkingDoc"/>
/// (jsonb) pro Korrelation. Eigene, kurze Session je Zugriff — bewusst <b>kein</b> Co-Commit mit der
/// Entscheidungs-Transaktion (die Entscheidungen bleiben OCC im Manager-Log). Reiner Fortschritts-Cache:
/// geht er verloren oder ist er inkonsistent, faltet der Manager wieder ab 0 und das Ziel dedupliziert —
/// kein Datenverlust, höchstens ein einmaliges Re-Emit (Konzept §3/§5). Exakt die Semantik des
/// <see cref="MartenEmittentenCursor"/>, nur mit verdichtetem Marking statt bloßer Position.
///
/// Die Event-Payloads der gecachten Tokens werden serializer-agnostisch als getaggtes JSON abgelegt
/// (<see cref="TokenDoc.Typ"/> = snake_case-Event-Name aus <c>GeneratedTypeRegistry</c>, reflection-frei):
/// das Doc enthält nur Strings/Primitive, egal welchen Doc-Serializer Marten nutzt. Auf dem Lade-Pfad
/// wird der Payload über die (generierte) Namens→Typ-Umkehrung rehydriert — findet sich ein Typ nicht,
/// gilt der Cache als ungültig (→ null → Voll-Fold), nie ein halb-rehydriertes Marking.
///
/// Marten-Registrierung nötig (in der StoreOptions-Konfiguration):
///   opts.Schema.For&lt;ProzessMarkingDoc&gt;().Identity(x =&gt; x.Id);
/// </summary>
public sealed class MartenProzessMarkingStore : IProzessMarkingStore
{
    private readonly IDocumentStore _store;
    private readonly ILogger<MartenProzessMarkingStore>? _logger;

    private static readonly JsonSerializerOptions Json = new();

    // Namens↔Typ-Umkehrung aus der generierten Registry (reflection-frei; Inv. 4).
    private static readonly IReadOnlyDictionary<Type, string> NameByType =
        GeneratedTypeRegistry.PersistableEvents.ToDictionary(e => e.Type, e => e.SnakeCaseName);
    private static readonly IReadOnlyDictionary<string, Type> TypeByName =
        GeneratedTypeRegistry.PersistableEvents.ToDictionary(e => e.SnakeCaseName, e => e.Type);

    public MartenProzessMarkingStore(IDocumentStore store, ILogger<MartenProzessMarkingStore>? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger;
    }

    public async Task<ProzessMarking?> LadeAsync(Guid korrelation, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<ProzessMarkingDoc>(korrelation, ct);
        if (doc is null) return null;
        try
        {
            return NachDomäne(doc);
        }
        catch (Exception ex)
        {
            // Rehydrierung fehlgeschlagen (unbekannter Typ / geänderte Payload-Form) → ungültig, Voll-Fold heilt.
            _logger?.LogWarning(ex, "[Prozess-Marking] Rehydrierung fehlgeschlagen ({Korr}) — Cache verworfen", korrelation);
            return null;
        }
    }

    public async Task SchreibeAsync(ProzessMarking marking, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Store(AusDomäne(marking));
        await session.SaveChangesAsync(ct);
    }

    // ── Domäne ↔ Doc ──

    private static ProzessMarkingDoc AusDomäne(ProzessMarking m) => new()
    {
        Id = m.Id,
        RegelHash = m.RegelHash,
        LogVersion = m.LogVersion,
        StreamCursor = m.StreamCursor.ToDictionary(kv => kv.Key.ToString("N"), kv => kv.Value),
        Auslöser = TokenAusDomäne(m.Marking.Auslöser),
        Ergebnisse = m.Marking.Ergebnisse.Select(v => new VorgangDoc
        {
            Vorgang = v.Vorgang,
            ErgebnisDa = v.ErgebnisDa,
            WirkungDa = v.WirkungDa,
            Wirkung = TokenAusDomäne(v.Wirkung),
            AbgelehntDa = v.AbgelehntDa,
            AbgelehntGrund = v.AbgelehntGrund,
        }).ToList(),
        UpdatedAt = m.UpdatedAt,
    };

    private static ProzessMarking NachDomäne(ProzessMarkingDoc d) => new()
    {
        Id = d.Id,
        RegelHash = d.RegelHash,
        LogVersion = d.LogVersion,
        StreamCursor = d.StreamCursor.ToDictionary(kv => Guid.ParseExact(kv.Key, "N"), kv => kv.Value),
        Marking = new MarkingKompakt
        {
            Auslöser = TokenNachDomäne(d.Auslöser),
            Ergebnisse = d.Ergebnisse.Select(v => new VorgangErgebnis
            {
                Vorgang = v.Vorgang,
                ErgebnisDa = v.ErgebnisDa,
                WirkungDa = v.WirkungDa,
                Wirkung = TokenNachDomäne(v.Wirkung),
                AbgelehntDa = v.AbgelehntDa,
                AbgelehntGrund = v.AbgelehntGrund,
            }).ToList(),
        },
        UpdatedAt = d.UpdatedAt,
    };

    private static TokenDoc? TokenAusDomäne(MarkingToken? t)
    {
        if (t is null) return null;
        if (!NameByType.TryGetValue(t.Payload.GetType(), out var name))
            throw new InvalidOperationException($"Event-Typ {t.Payload.GetType().Name} nicht in GeneratedTypeRegistry.");
        return new TokenDoc
        {
            Typ = name,
            Json = JsonSerializer.Serialize(t.Payload, t.Payload.GetType(), Json),
            Stream = t.Stream,
            Version = t.Version,
        };
    }

    private static MarkingToken? TokenNachDomäne(TokenDoc? d)
    {
        if (d is null) return null;
        if (!TypeByName.TryGetValue(d.Typ, out var typ))
            throw new InvalidOperationException($"Event-Typ '{d.Typ}' nicht in GeneratedTypeRegistry.");
        var payload = (IEvent)JsonSerializer.Deserialize(d.Json, typ, Json)!;
        return new MarkingToken { Payload = payload, Stream = d.Stream, Version = d.Version };
    }
}

/// <summary>Persistenz-DTO des <see cref="ProzessMarking"/> (nur Strings/Primitive → serializer-agnostisch).</summary>
public sealed class ProzessMarkingDoc
{
    public Guid Id { get; set; }
    public string RegelHash { get; set; } = "";
    public int LogVersion { get; set; }
    public Dictionary<string, int> StreamCursor { get; set; } = new();
    public TokenDoc? Auslöser { get; set; }
    public List<VorgangDoc> Ergebnisse { get; set; } = new();
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class TokenDoc
{
    public string Typ { get; set; } = "";
    public string Json { get; set; } = "";
    public Guid Stream { get; set; }
    public int Version { get; set; }
}

public sealed class VorgangDoc
{
    public Guid Vorgang { get; set; }
    public bool ErgebnisDa { get; set; }
    public bool WirkungDa { get; set; }
    public TokenDoc? Wirkung { get; set; }
    public bool AbgelehntDa { get; set; }
    public string AbgelehntGrund { get; set; } = "abgelehnt";
}
