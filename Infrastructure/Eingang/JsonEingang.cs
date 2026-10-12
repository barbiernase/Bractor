using System.Text.Json;
using Abstractions;
using Domain.Projections;
using Infrastructure.Aggregate.ActorSystem;
using Infrastructure.Akteure;
using Infrastructure.Extensions;
using Infrastructure.Funktionen;
using Infrastructure.Mapping;
using Infrastructure.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Proto;

namespace Infrastructure.Eingang;

/// <summary>
/// Der JSON-Eingang des Hosts (docs/konzept-werkbank.md §6, Werkbank P5, Plan E5) — der Weg der Werkbank ins ECHTE System, nur auf dem
/// Einblick-Port und nur mit <c>Transport:JsonEingang</c> (Sim an, Prod aus):
/// <list type="bullet">
/// <item><c>POST /eingang/command</c>, <c>/eingang/query</c> — als Akteur: Token im Header <c>akteur-token</c> → <see cref="AkteurTor"/> →
///   Aggregat bzw. Reader. Derselbe Weg wie gRPC (Befugnis, Akteur im Envelope, Wirker <c>akteur:A</c>), nur ohne Proto: der Typ kommt über
///   die generierte Registry, die Felder über den generierten Wire-Kontext (keine Reflection).</item>
/// <item><c>GET /eingang/auftraege</c>, <c>POST /eingang/ergebnis</c> — Laufort <c>Hand</c>: die Werkbank beantwortet Aufträge
///   (<see cref="HandAusfuehrer"/>).</item>
/// </list>
/// </summary>
public static class JsonEingang
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CommandZeitlimit = TimeSpan.FromSeconds(15);

    public static void MapEingang(this WebApplication app, UmgebungKonfig u)
    {
        if (u.Transport.EinblickPort <= 0 || !u.Transport.JsonEingang) return;
        var g = app.MapGroup("/eingang").RequireHost($"*:{u.Transport.EinblickPort}");

        // Wer bin ich mit diesem Token, und was darf ich hineingeben? (Ohne Identitäten ist das Tor offen — wie am gRPC-Port.)
        g.MapGet("/rechte", (HttpContext http, IServiceProvider sp) =>
        {
            if (!Erkenne(http, sp, out var akteur, out var abgewiesen)) return abgewiesen!;
            MessageTypeMapping.Initialize();
            return Results.Json(new
            {
                akteur = akteur?.Name,
                offen = akteur is null,
                commands = (akteur?.Commands ?? GeneratedTypeRegistry.Commands.Values).Select(t => t.Name).Order(StringComparer.Ordinal),
                queries = (akteur?.Queries ?? GeneratedTypeRegistry.Queries.Values).Where(ProjectionQueryService.SupportedQueryTypes.Contains)
                    .Select(t => t.Name).Order(StringComparer.Ordinal),
            }, Json);
        });

        g.MapPost("/command", async (HttpContext http, JsonElement body, ActorSystem system, IServiceProvider sp, CancellationToken ct) =>
        {
            if (!Erkenne(http, sp, out var akteur, out var abgewiesen)) return abgewiesen!;
            if (!Lies(body, MessageCategory.Command, out var typ, out var nachricht, out var fehler)) return fehler!;
            var cmd = (ICommand)nachricht!;
            if (akteur != null && !akteur.DarfHinein(typ!))
                return Results.Json(new { ok = false, abgelehnt = $"Akteur '{akteur.Name}' darf {typ!.Name} nicht (kein IDarf<{typ.Name}>)" }, Json, statusCode: 403);

            string aggregat;
            try { aggregat = AggregateDispatcherExtensions.ResolveAggregateType(cmd); }
            catch (InvalidOperationException e) { return Results.Json(new { ok = false, fehler = e.Message }, Json, statusCode: 400); }
            // Wer hineingibt, stempelt das Tor; ohne Tor folgt die Herkunft dem Modell (der eine Akteur mit IDarf) — wie am gRPC-Port.
            var wer = akteur?.AkteurFuer(typ!) ?? AkteurHerkunft.EindeutigerHalter(typ!) ?? "system";
            var envelope = new CommandEnvelope
            {
                AggregateId = cmd.AggregateId,
                AggregateType = aggregat,
                Payload = cmd,
                // Eine Konsole kennt keine Version: ohne Angabe wie ein Emitter (keine behauptete Version, die Inbox dedupliziert).
                Modus = body.TryGetProperty("erwarteteVersion", out var v) && v.ValueKind == JsonValueKind.Number
                    ? new CommandModus.Client(v.GetInt32()) : new CommandModus.Emittiert(),
                UserId = wer,
                CorrelationId = body.TryGetProperty("korrelation", out var k) && k.GetString() is { Length: > 0 } kor ? kor : Guid.NewGuid().ToString(),
                Wirker = ImAuftrag.IstAkteur(wer) ? WirkerId.FuerAkteur(wer) : null,
            };

            var ergebnis = await ProtoActorAggregateDispatcher.FrageAsync(system, envelope, CommandZeitlimit, ct);
            if (ergebnis is null)
                return Results.Json(new { ok = false, fehler = $"keine Antwort von {aggregat}/{cmd.AggregateId} nach {CommandZeitlimit.TotalSeconds:0} s" }, Json, statusCode: 504);
            return Results.Json(new
            {
                ok = ergebnis.Success,
                akteur = wer,
                aggregat,
                aggregatId = cmd.AggregateId,
                korrelation = envelope.CorrelationId,
                version = ergebnis.NewVersion,
                events = ergebnis.Events.Select(Darstellung),
                ablehnung = ergebnis.RejectionEvent is { } r ? Darstellung(r) : null,
                fehler = ergebnis.ErrorMessage,
            }, Json);
        });

        g.MapPost("/query", async (HttpContext http, JsonElement body, IServiceProvider sp) =>
        {
            if (!Erkenne(http, sp, out var akteur, out var abgewiesen)) return abgewiesen!;
            if (!Lies(body, MessageCategory.Query, out var typ, out var nachricht, out var fehler)) return fehler!;
            if (!AkteurTor.Darf(akteur, typ!))
                return Results.Json(new { ok = false, abgelehnt = $"Akteur '{akteur!.Name}' darf {typ!.Name} nicht (kein IDarf<{typ.Name}>)" }, Json, statusCode: 403);
            if (!ProjectionQueryService.SupportedQueryTypes.Contains(typ!))
                return Results.Json(new { ok = false, fehler = $"{typ!.Name} beantwortet kein Reader des Systems" }, Json, statusCode: 400);
            try
            {
                var antwort = await sp.GetRequiredService<ProjectionQueryService>().ExecuteAsync((IQuery)nachricht!);
                return Results.Json(new { ok = true, antwort = Darstellung(antwort.Data) }, Json);
            }
            catch (Exception e) { return Results.Json(new { ok = false, fehler = e.Message }, Json, statusCode: 500); }
        });

        // Laufort Hand: die wartenden Aufträge samt Inhalt und den möglichen Ergebnis-Fällen (ihre Signatur).
        g.MapGet("/auftraege", (IServiceProvider sp) =>
        {
            var hand = sp.GetService<HandAusfuehrer>();
            return Results.Json(new
            {
                funktionen = hand?.Funktionen.Select(f => f.Name) ?? [],
                auftraege = (hand?.Offen ?? []).Select(a => new
                {
                    vorgang = a.Auftrag.Vorgang,
                    korrelation = a.Auftrag.Korrelation,
                    funktion = a.Funktion.Name,
                    auftrag = Darstellung(a.Auftrag.Auftrag),
                    akteur = a.Auftrag.Akteur,
                    ursache = a.Auftrag.Ursache,
                    seit = a.Seit,
                    ergebnisse = Ergebnisse(a.Funktion).Select(t => t.Name),
                }),
            }, Json);
        });

        g.MapPost("/ergebnis", async (JsonElement body, IServiceProvider sp, CancellationToken ct) =>
        {
            var hand = sp.GetService<HandAusfuehrer>();
            if (hand is null) return Results.Json(new { ok = false, fehler = "keine Funktion mit Laufort Hand in dieser Umgebung" }, Json, statusCode: 404);
            if (!body.TryGetProperty("vorgang", out var vg) || !Guid.TryParse(vg.GetString(), out var vorgang))
                return Results.Json(new { ok = false, fehler = "vorgang fehlt" }, Json, statusCode: 400);
            var auftrag = hand.Offen.FirstOrDefault(a => a.Auftrag.Vorgang == vorgang);
            if (auftrag is null) return Results.Json(new { ok = false, fehler = $"Auftrag {vorgang} wartet hier nicht (mehr)" }, Json, statusCode: 404);

            var fehler = body.TryGetProperty("fehler", out var f) && f.GetString() is { Length: > 0 } fe ? fe : null;
            IEvent? ergebnis = null;
            if (fehler is null)
            {
                var name = body.TryGetProperty("typ", out var t) ? t.GetString() : null;
                var typ = Ergebnisse(auftrag.Funktion).FirstOrDefault(x => x.Name == name);
                if (typ is null)
                    return Results.Json(new { ok = false, fehler = $"{name} ist kein Ergebnis von {auftrag.Funktion.Name} ({string.Join(", ", Ergebnisse(auftrag.Funktion).Select(x => x.Name))})" }, Json, statusCode: 400);
                try { ergebnis = (IEvent)EingangJson.Lies(body.TryGetProperty("daten", out var d) ? d : default, typ); }
                catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException)
                { return Results.Json(new { ok = false, fehler = $"{typ.Name} nicht lesbar: {e.Message}" }, Json, statusCode: 400); }
            }
            var ausgang = await hand.AntworteAsync(vorgang, ergebnis, fehler, ct);
            return Results.Json(new { ok = ausgang is ErgebnisAusgang.Geschrieben or ErgebnisAusgang.Abgelehnt, ausgang = ausgang.ToString() }, Json,
                statusCode: ausgang is ErgebnisAusgang.Unbekannt ? 404 : ausgang is ErgebnisAusgang.NichtGeschrieben ? 503 : 200);
        });
    }

    private static IReadOnlyCollection<Type> Ergebnisse(Type funktion)
        => GeneratedFunktionen.Ergebnisse.TryGetValue(funktion, out var e) ? e : Array.Empty<Type>();

    /// <summary>Das Token → Akteur (Tor). Ohne konfigurierte Identitäten ist das Tor offen (<paramref name="akteur"/> = null).</summary>
    private static bool Erkenne(HttpContext http, IServiceProvider sp, out AkteurRechte? akteur, out IResult? abgewiesen)
    {
        akteur = null;
        abgewiesen = null;
        if (sp.GetService<AkteurTor>() is not { } tor) return true;
        akteur = tor.Erkenne(http.Request.Headers[AkteurOptionen.TokenHeader].FirstOrDefault());
        if (akteur is not null) return true;
        abgewiesen = Results.Json(new { ok = false, fehler = "unbekanntes Akteur-Token (Abschnitt Identitaeten der Umgebung)" }, Json, statusCode: 401);
        return false;
    }

    /// <summary>Liest <c>{ "typ": "Name", "daten": { … } }</c> als Nachricht der erwarteten Kategorie.</summary>
    private static bool Lies(JsonElement body, MessageCategory erwartet, out Type? typ, out object? nachricht, out IResult? fehler)
    {
        typ = null;
        nachricht = null;
        fehler = null;
        var name = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("typ", out var t) ? t.GetString() : null;
        var (gefunden, art) = MessageTypeMapping.Resolve(name ?? "");
        if (gefunden is null || art != erwartet)
        {
            fehler = Results.Json(new { ok = false, fehler = $"'{name}' ist kein {erwartet}" }, Json, statusCode: 400);
            return false;
        }
        typ = gefunden;
        try { nachricht = EingangJson.Lies(body.TryGetProperty("daten", out var d) ? d : default, gefunden); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException)
        {
            fehler = Results.Json(new { ok = false, fehler = $"{gefunden.Name} nicht lesbar: {e.Message}" }, Json, statusCode: 400);
            return false;
        }
        return true;
    }

    private static object Darstellung(object wert) => new { typ = wert.GetType().Name, daten = EingangJson.Schreibe(wert) };
}

/// <summary>
/// Nachrichten ↔ JSON über den generierten Wire-Kontext (<see cref="CqrsWireJsonContext"/>: Commands, Events, Aufträge, Queries, Antworten;
/// Enums als Namen). Der Typ kommt aus der generierten Registry, die Metadaten aus dem Source-Generator — keine Reflection (Inv. 4).
/// </summary>
public static class EingangJson
{
    // Die Optionen des generierten Kontexts (Resolver, Converter, Enums als Namen), nur Feldnamen ohne Groß/Klein-Unterschied —
    //   ein Client schreibt sie gern camelCase. Der Resolver bleibt der Source-Generator.
    private static readonly JsonSerializerOptions Optionen = new(CqrsWireJsonContext.Default.Options) { PropertyNameCaseInsensitive = true };

    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo Info(Type typ)
        => Optionen.TryGetTypeInfo(typ, out var info) ? info
           : throw new NotSupportedException($"{typ.Name} steht nicht im Wire-Kontext (codegen.sh — Proto/STJ neu erzeugen).");

    /// <summary>Liest <paramref name="daten"/> (ein JSON-Objekt; fehlt es, gilt <c>{}</c>) als <paramref name="typ"/>.</summary>
    public static object Lies(JsonElement daten, Type typ)
    {
        var quelle = daten.ValueKind == JsonValueKind.Undefined ? JsonDocument.Parse("{}").RootElement : daten;
        return quelle.Deserialize(Info(typ)) ?? throw new JsonException($"{typ.Name}: leer");
    }

    public static JsonElement Schreibe(object wert) => JsonSerializer.SerializeToElement(wert, Info(wert.GetType()));
}
