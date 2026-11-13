using System.Text.Json;
using Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Quellen;

/// <summary>
/// Der Endpunkt der ANNAHME (docs/04 §4.7, docs/06): <c>POST /eingang/webhook/{**pfad}</c> auf dem Einblick-Port JEDES Hosts — gemappt, sobald die
/// Umgebung einen Eingang aus einer annehmenden Quellart hat (sein eigener Schalter; der JSON-Eingang der Werkbank, <c>Transport:JsonEingang</c>, ist
/// davon unabhängig). Ein Aufruf: Schlüssel im Kopf <c>schluessel</c> (wenn der Eingang einen nennt), Kennung im Kopf <c>kennung</c> (Pflicht), der Körper
/// ist der Inhalt. Antwort 202 mit der Kennung erst, wenn die Nachricht durabel ist; 404 (kein Eingang unter dem Pfad), 401 (Schlüssel), 400 (Kennung
/// fehlt), 503 (voll oder gescheitert — später, mit derselben Kennung). Die Logik steht in <see cref="Annahmen"/>.
/// </summary>
public static class AnnahmeEndpunkt
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapAnnahme(this WebApplication app, UmgebungKonfig u)
    {
        var annahmen = app.Services.GetService<Annahmen>() ?? Annahmen.Keine;
        if (annahmen.Pfade.Count == 0 || u.Transport.EinblickPort <= 0) return;   // ohne Einblick-Port bricht schon BindeEingaenge den Start
        app.MapPost(Annahmevertrag.Route + "/{**pfad}", async (HttpContext http, string? pfad, CancellationToken ct) =>
        {
            using var leser = new StreamReader(http.Request.Body);
            var inhalt = await leser.ReadToEndAsync(ct);
            var schluessel = http.Request.Headers[Annahmevertrag.SchluesselKopf].FirstOrDefault();
            var kennung = http.Request.Headers[Annahmevertrag.KennungKopf].FirstOrDefault();
            var a = await annahmen.NimmAsync(pfad ?? "", schluessel, kennung, inhalt, ct);
            return Results.Json(a.Ok ? new { ok = true, kennung = a.Kennung, element = a.Element } : (object)new { ok = false, kennung = a.Kennung, fehler = a.Fehler },
                Json, statusCode: a.Status);
        }).RequireHost($"*:{u.Transport.EinblickPort}");
        Console.WriteLine($"[CQRS] Annahme: POST :{u.Transport.EinblickPort}{Annahmevertrag.Route}/ " + string.Join(", ", annahmen.Pfade));
    }
}
