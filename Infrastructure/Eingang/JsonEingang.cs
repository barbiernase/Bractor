using System.Text.Json;
using Abstractions;
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
using Infrastructure.Komposition;

namespace Infrastructure.Eingang;

/// <summary>
/// Der JSON-Eingang des Hosts (CLAUDE.md W6, Werkbank P5, docs/06 §6.6) — der Weg der Werkbank ins ECHTE System, nur auf dem
/// Einblick-Port und nur mit <c>Transport:JsonEingang</c> (Sim an, Prod aus):
/// <list type="bullet">
/// <item><c>POST /eingang/command</c>, <c>/eingang/query</c> — als Akteur: Token im Header <c>akteur-token</c> → <see cref="AkteurTor"/> →
///   Aggregat bzw. Reader oder Aggregat. Derselbe Weg wie gRPC (Befugnis, Akteur im Envelope, Wirker <c>akteur:A</c>), nur ohne Proto: der Typ kommt über
///   die generierte Registry, die Felder über den generierten Wire-Kontext (keine Reflection). Die Antwort auf einen Command nennt Ausgang und
///   Versionen; mit <c>"zeigeEvents": true</c> dazu die geschriebenen Events, gelesen aus dem Log. Die Antwort auf eine Query nennt den Stand je
///   gezeigter Id (<c>staende</c>) — fragt sie ein Aggregat, seine Version als OCC-Stand.</item>
/// <item><c>GET /eingang/auftraege</c>, <c>POST /eingang/ergebnis</c> — Laufort <c>Hand</c>: die Werkbank beantwortet die Eingänge einer Funktion
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
                commands = (akteur?.Commands ?? Anwendung.Typen.Commands.Values).Select(t => t.Name).Order(StringComparer.Ordinal),
                queries = (akteur?.Queries ?? Anwendung.Typen.Queries.Values).Where(Anwendung.QueryTypen.Contains)
                    .Select(t => t.Name).Order(StringComparer.Ordinal),
            }, Json);
        });

        g.MapPost("/command", async (HttpContext http, JsonElement body, ActorSystem system, IServiceProvider sp, CancellationToken ct) =>
        {
            if (!Erkenne(http, sp, out var akteur, out var abgewiesen)) return abgewiesen!;
            if (LiesOhneZiel(body) is { } ohneZiel) return await AnKopfAsync(body, ohneZiel, akteur, sp, ct);
            if (!Lies(body, MessageCategory.Command, out var typ, out var nachricht, out var fehler)) return fehler!;
            var cmd = (IAggregatCommand)nachricht!;
            if (akteur != null && !akteur.DarfHinein(typ!))
                return Results.Json(new { ok = false, abgelehnt = $"Akteur '{akteur.Name}' darf {typ!.Name} nicht (kein IDarf<{typ.Name}>)" }, Json, statusCode: 403);

            string aggregat;
            try { aggregat = AggregateDispatcherExtensions.ResolveAggregateType(cmd); }
            catch (InvalidOperationException e) { return Results.Json(new { ok = false, fehler = e.Message }, Json, statusCode: 400); }
            // Wer hineingibt, stempelt das Tor; ohne Tor folgt die Herkunft dem Modell (der eine Akteur mit IDarf) — wie am gRPC-Port.
            var wer = akteur?.AkteurFuer(typ!) ?? AkteurHerkunft.EindeutigerHalter(typ!) ?? "system";
            var envelope = new CommandEnvelope
            {
                // Wer wiederholt, schickt dieselbe CommandId — die Inbox des Aggregats macht daraus genau eine Wirkung (docs/11 §11.3).
                CommandId = body.TryGetProperty("commandId", out var id) && Guid.TryParse(id.GetString(), out var cid) ? cid : Guid.NewGuid(),
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
                return Results.Json(new { ok = false, fehler = $"keine Antwort von {aggregat}/{cmd.Ziel} nach {CommandZeitlimit.TotalSeconds:0} s" }, Json, statusCode: 504);
            // Die Events, die dieser Command geschrieben hat, nur auf Wunsch — aus dem Log, nicht aus der Antwort (sie nennt nur den Bereich):
            //   ein Lesezugriff je Command, den ein Treiber unter Last nicht braucht.
            var zeige = body.TryGetProperty("zeigeEvents", out var z) && z.ValueKind == JsonValueKind.True;
            var events = zeige ? await GeschriebenAsync(sp, cmd.Ziel, ergebnis, ct) : null;
            return Results.Json(new
            {
                ok = ergebnis.Success,
                akteur = wer,
                aggregat,
                aggregatId = cmd.Ziel,
                korrelation = envelope.CorrelationId,
                version = ergebnis.NewVersion,
                ersteVersion = ergebnis.ErsteVersion,
                events,
                ablehnung = ergebnis.RejectionEvent is { } r ? Darstellung(r) : null,
                fehler = ergebnis.ErrorMessage,
            }, Json);
        });

        // Ein Aufruf an einen Webhook (Katalogfunktion am Kopf eines Graphen, docs/04 §4.7): der Inhalt geht in das Postfach seines Pfads, der Webhook
        //   startet daraus einen Lauf. Die Kennung wählt der Absender (Kopf „kennung“), sonst eine neue; voll heißt „später“.
        g.MapPost("/webhook/{**pfad}", async (HttpContext http, string pfad, CancellationToken ct) =>
        {
            using var leser = new StreamReader(http.Request.Body);
            var inhalt = await leser.ReadToEndAsync(ct);
            var kennung = http.Request.Headers.TryGetValue("kennung", out var k) && k.ToString() is { Length: > 0 } kn ? kn : Guid.NewGuid().ToString();
            return Core.Katalog.Webhookpostfaecher.Lege(pfad, new Aufruf(kennung, inhalt))
                ? Results.Json(new { ok = true, kennung }, Json, statusCode: StatusCodes.Status202Accepted)
                : Results.Json(new { ok = false, fehler = "das Postfach ist voll — später" }, Json, statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        g.MapPost("/query", async (HttpContext http, JsonElement body, IServiceProvider sp) =>
        {
            if (!Erkenne(http, sp, out var akteur, out var abgewiesen)) return abgewiesen!;
            if (!Lies(body, MessageCategory.Query, out var typ, out var nachricht, out var fehler)) return fehler!;
            if (!AkteurTor.Darf(akteur, typ!))
                return Results.Json(new { ok = false, abgelehnt = $"Akteur '{akteur!.Name}' darf {typ!.Name} nicht (kein IDarf<{typ.Name}>)" }, Json, statusCode: 403);
            if (!Anwendung.QueryTypen.Contains(typ!))
                return Results.Json(new { ok = false, fehler = $"{typ!.Name} beantwortet kein Reader und kein Aggregat des Systems" }, Json, statusCode: 400);
            try
            {
                var antwort = await sp.GetRequiredService<IQueryDienst>().ExecuteAsync((IQuery)nachricht!);
                // Der Stand je gezeigter Id — bei einer Frage an ein Aggregat seine Version, der OCC-Stand für den nächsten Command (docs/03 §3.2).
                var staende = (antwort.Deps ?? []).Select(d => new { id = d.Id, aggregat = d.AggregateType, version = d.Version });
                return Results.Json(new { ok = true, antwort = Darstellung(antwort.Data), staende }, Json);
            }
            catch (Exception e) { return Results.Json(new { ok = false, fehler = e.Message }, Json, statusCode: 500); }
        });

        // Laufort Hand: die wartenden Eingänge samt Inhalt, Herkunft und den möglichen Ausgaben (ihre Signatur).
        g.MapGet("/auftraege", (IServiceProvider sp) =>
        {
            var hand = sp.GetService<HandAusfuehrer>();
            return Results.Json(new
            {
                funktionen = hand?.Bausteine.Select(b => b.Name) ?? [],
                eingaenge = (hand?.OffeneEingaenge ?? []).Select(e => new
                {
                    vorgang = e.Vorgang,
                    funktion = e.Funktion.Name,
                    eingang = Darstellung(e.Eingang),
                    stream = e.Stream,
                    version = e.Version,
                    akteur = e.Akteur,
                    seit = e.Seit,
                    ausgaben = e.Funktion.Eingang(e.Eingang.GetType())?.Ausgaben.Select(t => t.Name) ?? [],
                }),
            }, Json);
        });

        // Die Antwort ist eine Liste von Ausgaben [{typ, daten}] (oder ein Fehler) — die erste Antwort gewinnt (sie beendet den Lauf).
        g.MapPost("/ergebnis", (JsonElement body, IServiceProvider sp) =>
        {
            var hand = sp.GetService<HandAusfuehrer>();
            if (hand is null) return Results.Json(new { ok = false, fehler = "keine Funktion mit Laufort Hand in dieser Umgebung" }, Json, statusCode: 404);
            if (!body.TryGetProperty("vorgang", out var vg) || !Guid.TryParse(vg.GetString(), out var vorgang))
                return Results.Json(new { ok = false, fehler = "vorgang fehlt" }, Json, statusCode: 400);
            if (hand.OffeneEingaenge.FirstOrDefault(e => e.Vorgang == vorgang) is not { } offen)
                return Results.Json(new { ok = false, fehler = $"Eingang {vorgang} wartet hier nicht (mehr)" }, Json, statusCode: 404);
            var fehler = body.TryGetProperty("fehler", out var fb) && fb.GetString() is { Length: > 0 } feb ? feb : null;
            var moeglich = offen.Funktion.Eingang(offen.Eingang.GetType())?.Ausgaben ?? [];
            var ausgaben = new List<object>();
            if (fehler is null && body.TryGetProperty("ausgaben", out var liste) && liste.ValueKind == JsonValueKind.Array)
                foreach (var a in liste.EnumerateArray())
                {
                    var name = a.TryGetProperty("typ", out var tn) ? tn.GetString() : null;
                    var typ = moeglich.FirstOrDefault(x => x.Name == name);
                    if (typ is null)
                        return Results.Json(new { ok = false, fehler = $"{name} ist keine Ausgabe von {offen.Funktion.Name} ({string.Join(", ", moeglich.Select(x => x.Name))})" }, Json, statusCode: 400);
                    try { ausgaben.Add(EingangJson.Lies(a.TryGetProperty("daten", out var dd) ? dd : default, typ)); }
                    catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException)
                    { return Results.Json(new { ok = false, fehler = $"{typ.Name} nicht lesbar: {e.Message}" }, Json, statusCode: 400); }
                }
            var ausgang = hand.Antworte(vorgang, fehler is null ? ausgaben : null, fehler);
            return Results.Json(new { ok = ausgang is not Werkstattausgang.Unbekannt, ausgang = ausgang.ToString() }, Json,
                statusCode: ausgang is Werkstattausgang.Unbekannt ? 404 : 200);
        });
    }

    /// <summary>
    /// Die Events, die ein gewirkter Command geschrieben hat: der Bereich <c>ErsteVersion … NewVersion</c> seines Streams aus dem Log. Leer, wenn er
    /// nichts geschrieben hat (Noop, Wiederholung, Ablehnung, Fehler).
    /// </summary>
    private static async Task<IEnumerable<object>> GeschriebenAsync(IServiceProvider sp, Guid stream, CommandResult ergebnis, CancellationToken ct)
    {
        if (!ergebnis.Success || ergebnis.ErsteVersion <= 0) return [];
        var gelesen = await sp.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(stream, ergebnis.ErsteVersion, ct);
        return gelesen
            .Where(e => e.AggregateVersion <= ergebnis.NewVersion)
            .Select(e => Darstellung(e.Payload))
            .ToList();
    }


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
    /// <summary>Ein Command ohne Ziel (über die generierte Wire-Form der Commands ohne Ziel); null = der Typ ist keiner.</summary>
    private static object? LiesOhneZiel(JsonElement body)
    {
        var name = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("typ", out var t) ? t.GetString() : null;
        if (string.IsNullOrEmpty(name) || MessageTypeMapping.Resolve(name).Category != MessageCategory.Unknown) return null;
        var daten = body.TryGetProperty("daten", out var d) ? System.Text.Encoding.UTF8.GetBytes(d.GetRawText()) : "{}"u8.ToArray();
        try
        {
            var leser = new Utf8JsonReader(daten);
            leser.Read();
            return Anwendung.WirePoly.ReadNachricht(name, ref leser) as ICommand;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException or KeyNotFoundException) { return null; }
    }

    /// <summary>
    /// Ein Command ohne Ziel von einem Akteur (docs/04 §4.4): ein offener Eingang am Kopf der Instanz, die ihn nimmt (Kennung aus der Command-Id — wer
    /// wiederholt, schickt dieselbe), danach der schnelle Weg. Genau einmal am Kopf; „angenommen“, sobald die Zeile steht.
    /// </summary>
    private static async Task<IResult> AnKopfAsync(JsonElement body, object nachricht, AkteurRechte? akteur, IServiceProvider sp, CancellationToken ct)
    {
        var typ = nachricht.GetType();
        if (akteur != null && !akteur.DarfHinein(typ))
            return Results.Json(new { ok = false, abgelehnt = $"Akteur '{akteur.Name}' darf {typ.Name} nicht (kein IDarf<{typ.Name}>)" }, Json, statusCode: 403);
        var kennung = body.TryGetProperty("commandId", out var id) && Guid.TryParse(id.GetString(), out var cid) ? cid : Guid.NewGuid();
        var wer = akteur?.AkteurFuer(typ) ?? AkteurHerkunft.EindeutigerHalter(typ);
        var korrelation = body.TryGetProperty("korrelation", out var k) && k.GetString() is { Length: > 0 } kor ? kor : kennung.ToString();
        var eingaenge = Infrastructure.Eingaenge.Folgenbeilagen.VonAkteur((ICommand)nachricht, sp.GetRequiredService<Infrastructure.Funktionen.Verkettung>(),
            kennung, wer, korrelation);
        if (eingaenge.Count == 0)
            return Results.Json(new { ok = false, fehler = $"{typ.Name}: kein Graph nimmt ihn am Kopf (fehlt eine Instanz in der Umgebung?)" }, Json, statusCode: 400);
        await sp.GetRequiredService<Infrastructure.Eingaenge.IOffeneEingaenge>().LegeAsync(eingaenge.Select(e => e.Eingang).ToList(), kennung, ct);
        if (sp.GetService<Infrastructure.Funktionen.IFunktionsWege>() is { } wege) Infrastructure.Eingaenge.Folgenbeilagen.StelleZu(eingaenge, wege, kennung);
        return Results.Json(new { ok = true, kennung, akteur = wer, instanzen = eingaenge.Select(e => e.Eingang.Instanz) }, Json, statusCode: StatusCodes.Status202Accepted);
    }

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
    private static readonly JsonSerializerOptions Optionen = new(Anwendung.WireJson.Options) { PropertyNameCaseInsensitive = true };

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
