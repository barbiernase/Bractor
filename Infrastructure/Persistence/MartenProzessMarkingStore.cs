using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure.Mapping;   // MessageTypeMapping (name→Type, reflection-frei aus GeneratedTypeRegistry)
using Marten;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Marten-<see cref="IProzessMarkingStore"/> (P5b): ein durables jsonb-Dokument je Prozess-Korrelation
/// (<c>es.mt_doc_prozess_marking_doc</c>), Identität = Korrelation, überschreibend. Das prozess-lokale
/// Analogon zum <see cref="MartenSnapshotStore"/> — best-effort, nicht-autoritativ: geht es verloren oder
/// passt der <see cref="ProzessMarking.RegelHash"/> nicht, faltet der Manager voll ab 0 (Invariante 1).
///
/// Serialisierungs-Naht: <see cref="MarkingKompakt"/> trägt polymorphe <see cref="IEvent"/>-Payloads (Wurzel-
/// und Downstream-Token). Statt sie als Interface ins Dokument zu geben (STJ/Marten könnten den konkreten Typ
/// nicht rekonstruieren), werden sie als <c>(Typ-Name, JSON)</c> abgelegt und über die generierte Registry
/// (<see cref="MessageTypeMapping.GetAllEventTypes"/>, reflection-frei) wieder aufgelöst. Ein unbekannter Typ
/// oder ein Deserialisierungs-Fehler ergibt einen Cache-Miss (null → Voll-Fold), nie eine Ausnahme nach oben.
///
/// Marten-Registrierung nötig (in der StoreOptions-Konfiguration):
///   opts.Schema.For&lt;ProzessMarkingDoc&gt;().Identity(x =&gt; x.Id);
/// </summary>
public sealed class MartenProzessMarkingStore : IProzessMarkingStore
{
    private readonly IDocumentStore _store;
    private readonly ILogger<MartenProzessMarkingStore>? _logger;

    // Name→Type der Event-Payloads, EINMAL aus der generierten Registry gebildet (kein Reflection-Scan, kein Dispatch).
    private static readonly Lazy<IReadOnlyDictionary<string, Type>> EventTypen = new(() =>
    {
        var map = new Dictionary<string, Type>();
        foreach (var t in MessageTypeMapping.GetAllEventTypes())
            if (t.FullName is { } fn) map[fn] = t;
        return map;
    });

    public MartenProzessMarkingStore(IDocumentStore store, ILogger<MartenProzessMarkingStore>? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger;
    }

    public async Task<ProzessMarking?> LadeAsync(Guid korrelation, CancellationToken ct)
    {
        try
        {
            await using var session = _store.QuerySession();
            var doc = await session.LoadAsync<ProzessMarkingDoc>(korrelation, ct);
            return doc is null ? null : ZuMarking(doc);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Prozess-Marking laden fehlgeschlagen für {Korrelation} — Cache-Miss (Voll-Fold heilt).", korrelation);
            return null;
        }
    }

    public async Task SchreibeAsync(ProzessMarking marking, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Store(ZuDoc(marking));
        await session.SaveChangesAsync(ct);
    }

    public async Task LöscheAsync(Guid korrelation, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Delete<ProzessMarkingDoc>(korrelation);
        await session.SaveChangesAsync(ct);
    }

    // ── Mapping ProzessMarking ↔ ProzessMarkingDoc (Payloads als (Typ, JSON)) ──

    private static ProzessMarkingDoc ZuDoc(ProzessMarking m) => new()
    {
        Id = m.Id,
        RegelHash = m.RegelHash,
        LogVersion = m.LogVersion,
        UpdatedAt = m.UpdatedAt,
        Auslöser = SerialisierePayload(m.Marking.AuslöserPayload),
        StreamCursor = m.Marking.StreamCursor.Select(kv => new StreamCursorDto { Stream = kv.Key, Version = kv.Value }).ToList(),
        Vorgänge = m.Marking.Vorgänge.Select(kv => new VorgangDto
        {
            VorgangId = kv.Key,
            Wirkung = kv.Value.Wirkung,
            Abgelehnt = kv.Value.Abgelehnt,
            Grund = kv.Value.Grund,
            TokenStream = kv.Value.TokenStream,
            TokenVersion = kv.Value.TokenVersion,
            TokenPayload = SerialisierePayload(kv.Value.TokenPayload),
        }).ToList(),
    };

    private static ProzessMarking ZuMarking(ProzessMarkingDoc d)
    {
        var kompakt = new MarkingKompakt
        {
            AuslöserPayload = DeserialisierePayload(d.Auslöser),
            StreamCursor = (d.StreamCursor ?? new()).ToDictionary(x => x.Stream, x => x.Version),
            Vorgänge = (d.Vorgänge ?? new()).ToDictionary(v => v.VorgangId, v => new VorgangMarke
            {
                Wirkung = v.Wirkung,
                Abgelehnt = v.Abgelehnt,
                Grund = v.Grund,
                TokenStream = v.TokenStream,
                TokenVersion = v.TokenVersion,
                TokenPayload = DeserialisierePayload(v.TokenPayload),
            }),
        };
        return new ProzessMarking { Id = d.Id, RegelHash = d.RegelHash, LogVersion = d.LogVersion, UpdatedAt = d.UpdatedAt, Marking = kompakt };
    }

    private static PayloadDto? SerialisierePayload(IEvent? payload)
        => payload is null || payload.GetType().FullName is not { } fn
            ? null
            : new PayloadDto { Typ = fn, Json = JsonSerializer.Serialize(payload, payload.GetType()) };

    private static IEvent? DeserialisierePayload(PayloadDto? dto)
    {
        if (dto is null || string.IsNullOrEmpty(dto.Typ)) return null;
        // Unbekannter Typ (z. B. Regel-/Deploy-Drift) → null: der Fold behandelt es als Cache-Miss (Voll-Fold heilt).
        if (!EventTypen.Value.TryGetValue(dto.Typ, out var typ)) return null;
        try { return JsonSerializer.Deserialize(dto.Json, typ) as IEvent; }
        catch { return null; }
    }
}

// ── Persistenz-DTOs (nur Primitive/Listen — keine polymorphen Felder, damit STJ/Marten sie sauber serialisiert) ──

/// <summary>Das durable jsonb-Dokument des Prozess-Marking-Cursors (Id = Korrelation). Reines POCO ohne Persistenz-Abhängigkeit.</summary>
public sealed class ProzessMarkingDoc
{
    public Guid Id { get; set; }
    public string RegelHash { get; set; } = "";
    public int LogVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public PayloadDto? Auslöser { get; set; }
    public List<StreamCursorDto> StreamCursor { get; set; } = new();
    public List<VorgangDto> Vorgänge { get; set; } = new();
}

public sealed class StreamCursorDto
{
    public Guid Stream { get; set; }
    public int Version { get; set; }
}

public sealed class VorgangDto
{
    public string VorgangId { get; set; } = "";
    public bool Wirkung { get; set; }
    public bool Abgelehnt { get; set; }
    public string Grund { get; set; } = "";
    public Guid TokenStream { get; set; }
    public int TokenVersion { get; set; }
    public PayloadDto? TokenPayload { get; set; }
}

/// <summary>Ein Event-Payload als (Typ-Name, JSON) — löst das polymorphe <see cref="IEvent"/> aus dem Dokument.</summary>
public sealed class PayloadDto
{
    public string Typ { get; set; } = "";
    public string Json { get; set; } = "";
}
