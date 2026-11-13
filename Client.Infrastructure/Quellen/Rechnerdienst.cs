using System.Text.Json;
using Abstractions;
using Client.Infrastructure.Connection;
using Cqrs.Leitung;
using Infrastructure.Akteure;
using Infrastructure.Funktionen;
using Infrastructure.Komposition;
using Infrastructure.Quellen;
using Infrastructure.Umgebung;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Client.Infrastructure.Quellen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DER RECHNERDIENST (docs/10 E18): ein Prozess des Frameworks je Rechner (Host.Rechner, `--umgebung <u> --rechner <r>`).
// Er nimmt aus der Umgebung die App-Instanzen seines Rechners und trägt ihre Quellen: je lauschender Quelle ein Läufer, je annehmender eine Annahme auf
// dem Port der Instanz. Hineingegeben wird über die LEITUNG wie von jeder App — mit dem Token der Instanz, mit abgeleiteten Ids, bis zur Quittung; auch
// auf einem Rechner des Backends. Kein Marten, kein Cluster: was die Quelle meldet, steht nicht im Log, und das Framework sichert für sie nichts.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Der LAUF der Quellen eines Rechners (in-process prüfbar): je App-Instanz mit Quellen eine Verbindung (Token der Instanz, Handshake mit dem Vertrag ihrer
/// App), darauf je lauschender Quelle ein <see cref="QuellLaeufer"/> und je annehmender die <see cref="Annahmen"/>. Bricht die Verbindung ab, enden die
/// Läufer der Instanz, sie verbindet neu und beginnt von vorn — was erneut gemeldet wird, verwirft das Backend.
/// </summary>
public sealed class Quellenlauf(Eingaenge eingaenge, IReadOnlyDictionary<string, AppInstanzKonfig> apps, IReadOnlyDictionary<string, ClientVertrag> appVertraege,
    IServiceProvider dienste, INutzlastTabelle nutzlast, Annahmen? annahmen = null, ILoggerFactory? logs = null, TimeSpan? antwortfrist = null)
{
    /// <summary>So lange wartet eine Instanz, bevor sie nach einer Unterbrechung neu verbindet.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

    private readonly ILogger? _logger = logs?.CreateLogger("Quellen");

    /// <summary>Die Läufer der laufenden Verbindungen (Ort → Läufer) — für Prüfstände und das Log.</summary>
    public IReadOnlyDictionary<string, QuellLaeufer> Laeufer => _laeufer;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, QuellLaeufer> _laeufer = new(StringComparer.Ordinal);

    /// <summary>Läuft, bis <paramref name="ct"/> endet: je Instanz mit Quellen eine Verbindung zu <paramref name="adresse"/> (gRPC des Backends).</summary>
    public Task LaufeAsync(string adresse, CancellationToken ct) =>
        Task.WhenAll(eingaenge.Alle.GroupBy(e => e.Instanz).Select(g => InstanzAsync(g.Key, g.ToList(), adresse, ct)));

    private async Task InstanzAsync(string instanz, List<EingangPlan> plaene, string adresse, CancellationToken ct)
    {
        var a = apps[instanz];
        var vertrag = appVertraege[a.App];
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var proxy = new GrpcProxy(nutzlast) { AkteurToken = a.Token is { Length: > 0 } t ? t : null };
                await proxy.ConnectAsync(adresse, vertrag.Name, vertrag.Hash, ct);
                _logger?.LogInformation("[Quellen] {Instanz} ({App}) verbunden mit {Adresse}: {Quellen}", instanz, a.App, adresse,
                    string.Join(", ", plaene.Select(p => p.Name + " (" + p.Art.Name + ")")));
                await using var abgabe = new Abgabe(proxy, antwortfrist, _logger);
                annahmen?.Setze(instanz, p => new Hineingabe(p, abgabe, _logger));
                using var lauf = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var laeufe = plaene.Where(p => !p.Annahme).Select(p =>
                {
                    var l = new QuellLaeufer(p, dienste, new Hineingabe(p, abgabe, _logger), _logger);
                    _laeufer[p.Ort] = l;
                    return l.LaufeAsync(lauf.Token);
                }).ToList();
                while (!ct.IsCancellationRequested && proxy.IsConnected)
                    try { await Task.Delay(Pause, ct); } catch (OperationCanceledException) { }
                annahmen?.Setze(instanz, null);
                await lauf.CancelAsync();
                await Task.WhenAll(laeufe);
                if (!ct.IsCancellationRequested) _logger?.LogWarning("[Quellen] {Instanz}: Verbindung unterbrochen — neu verbinden", instanz);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                annahmen?.Setze(instanz, null);
                _logger?.LogWarning("[Quellen] {Instanz}: keine Verbindung zu {Adresse} ({Grund}) — neu nach {Pause}", instanz, adresse,
                    e.GetBaseException().Message, Pause);
            }
            catch (OperationCanceledException) { return; }
            try { await Task.Delay(Pause, ct); }
            catch (OperationCanceledException) { return; }
        }
    }
}

/// <summary>Der Rechnerdienst als Prozess: <c>Host.Rechner --umgebung &lt;u&gt; --rechner &lt;r&gt;</c> (bzw. <c>CQRS_UMGEBUNG</c>, <c>CQRS_RECHNER</c>).</summary>
public static class Rechnerdienst
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Lädt die Umgebung, nimmt die App-Instanzen dieses Rechners, prüft sie (<see cref="Eingaenge.Aus"/>), bindet die Werte und die Technik ihrer
    /// Quellen, nimmt auf den Ports der annehmenden Instanzen an und lässt die Quellen laufen, bis der Prozess endet. Ohne Quellen auf diesem Rechner endet
    /// er gleich.
    /// </summary>
    public static async Task<int> LaufeAsync(string[] args, IAnwendung anwendung)
    {
        WebApplication app;
        Quellenlauf lauf;
        string adresse;
        UmgebungKonfig u;
        try
        {
            var builder = WebApplication.CreateBuilder(args);
            var name = UmgebungLader.NameAus(args)
                       ?? throw new BootFehler($"Keine Umgebung angegeben: {UmgebungLader.Argument} <name> oder {UmgebungLader.Variable}.", "host:Umgebung");
            u = UmgebungLader.Lade(builder.Configuration, name, args);
            Anwendung.Setze(anwendung);
            if (!u.Rechner.ContainsKey(u.DieserRechner))
                throw new BootFehler($"Dieser Rechnerdienst läuft als Rechner {u.DieserRechner} ({Rechnerregeln.Argument} bzw. {Rechnerregeln.Variable}) — den gibt "
                    + $"es im Pool nicht (Rechner: {string.Join(", ", u.Rechner.Keys)}).", Rechnerregeln.Karte(u.DieserRechner));
            var apps = u.Apps.Where(a => a.Value.Rechner == u.DieserRechner).ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal);
            var tokens = CqrsUmgebungExtensions.Tokens(builder.Configuration.GetSection(UmgebungsAbschnitte.Identitaeten));
            var eingaenge = Eingaenge.Aus(apps, anwendung.Akteure.AppVertraege, anwendung.Funktionen.Bausteine,
                new Uebersetzungen(anwendung.Funktionen.Uebersetzungen), anwendung.Akteure.Rechte, tokens);
            if (eingaenge.Alle.Count == 0)
            {
                Console.WriteLine($"[Rechnerdienst] {u.DieserRechner} in {u.Umgebung.Name}: keine Quellen auf diesem Rechner — Ende.");
                return 0;
            }
            if (anwendung.Nutzlast is not { } nutzlast)
                throw new BootFehler("Die Anwendung hat keine Leitung (Nutzlast) — der Rechnerdienst gibt über die Leitung hinein.", "host:Umgebung");
            builder.Services.BindeTechnik(GraphInstanzen.Keine, eingaenge);
            var annahmen = new Annahmen();
            foreach (var e in eingaenge.Annehmende) annahmen.Nimm(e);
            // Auf einem Rechner mit Adresse localhost nur lokal, sonst auf allen Schnittstellen; ohne Annahme auf keinem festen Port.
            var lokal = u.Adresse(u.DieserRechner) is "localhost" or "127.0.0.1";
            builder.WebHost.UseUrls(annahmen.Ports.Count == 0 ? ["http://127.0.0.1:0"]
                : annahmen.Ports.Select(p => lokal ? $"http://localhost:{p}" : $"http://*:{p}").ToArray());
            app = builder.Build();
            app.MapPost(Annahmevertrag.Route + "/{**pfad}", async (HttpContext http, string? pfad, CancellationToken ct) =>
            {
                using var leser = new StreamReader(http.Request.Body);
                var inhalt = await leser.ReadToEndAsync(ct);
                var a = await annahmen.NimmAsync(pfad ?? "", http.Connection.LocalPort, http.Request.Headers[Annahmevertrag.SchluesselKopf].FirstOrDefault(),
                    http.Request.Headers[Annahmevertrag.KennungKopf].FirstOrDefault(), inhalt, ct);
                return Results.Json(a.Ok ? new { ok = true, kennung = a.Kennung, element = a.Element } : (object)new { ok = false, kennung = a.Kennung, fehler = a.Fehler },
                    Json, statusCode: a.Status);
            });
            app.MapGet("/health", () => Results.Text("ok"));
            adresse = u.GrpcAdresse();
            lauf = new Quellenlauf(eingaenge, apps, anwendung.Akteure.AppVertraege, app.Services, nutzlast, annahmen,
                app.Services.GetService<ILoggerFactory>());
            Console.WriteLine($"[Rechnerdienst] {u.DieserRechner} in {u.Umgebung.Name} → {adresse}: "
                + string.Join(", ", eingaenge.Alle.Select(e => e.Ort + " (" + e.Art.Name + (e.Annahme ? $", nimmt an :{e.Port}{Annahmevertrag.Route}/{e.Pfad}" : "")
                    + ")")));
        }
        catch (Exception e) { CqrsBoot.Melde(e); throw; }
        await app.StartAsync();
        await lauf.LaufeAsync(adresse, app.Lifetime.ApplicationStopping);
        await app.WaitForShutdownAsync();
        return 0;
    }
}
