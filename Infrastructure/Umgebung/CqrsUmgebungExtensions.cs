using Infrastructure.Einblick;
using Infrastructure.Eingang;
using Abstractions;
using Infrastructure.Akteure;
using Infrastructure.Deadlines;
using Infrastructure.Dienste;
using Infrastructure.Extensions;
using Infrastructure.Funktionen;
using Infrastructure.GrpcClient;
using Infrastructure.Monitoring;
using Infrastructure.Persistence;
using Infrastructure.Pipeline;
using Infrastructure.Prozess;
using Infrastructure.Quellen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Komposition;

namespace Infrastructure.Umgebung;

/// <summary>
/// Der Host in einer Zeile (docs/konzept-werkbank.md §4.2): WAS existiert, liefern die generierten Tabellen (Aggregate, Pull-Pfade,
/// Prozesse/Flüsse, Funktionen, Quellen, Fristen, Akteur-Rechte); WAS aktiv ist und mit welchen Werten, sagt die Umgebung. Alle
/// Implementierungen sind einkompiliert — ein Umgebungswechsel ist nie ein Code-Wechsel.
/// </summary>
public static class CqrsUmgebungExtensions
{
    /// <summary>
    /// Lädt die Umgebung aus <c>--umgebung &lt;name&gt;</c> (bzw. <c>CQRS_UMGEBUNG</c>) und verdrahtet den Host danach. Die Hosts rufen das
    /// generierte <c>AddCqrs(args)</c> ihrer Komposition (<c>&lt;Wurzel&gt;.Laufzeit</c>), das die Anwendung mitgibt.
    /// </summary>
    public static UmgebungKonfig AddCqrs(WebApplicationBuilder builder, string[] args, IAnwendung anwendung)
    {
        try
        {
            var name = UmgebungLader.NameAus(args)
                       ?? throw new BootFehler($"Keine Umgebung angegeben: {UmgebungLader.Argument} <name> oder {UmgebungLader.Variable}.", "host:Umgebung");
            var umgebung = UmgebungLader.Lade(builder.Configuration, name, args);
            AddCqrs(builder, umgebung, anwendung);
            return umgebung;
        }
        catch (Exception e) { CqrsBoot.Melde(e); throw; }
    }

    public static WebApplicationBuilder AddCqrs(WebApplicationBuilder builder, UmgebungKonfig u, IAnwendung anwendung)
    {
        // Die Anwendung zuerst: alles Weitere liest ihre Tabellen (docs/07, Rolle Komposition).
        Anwendung.Setze(anwendung);
        var s = builder.Services;
        s.AddSingleton(anwendung);
        s.AddSingleton(u);

        // Ports: gRPC (HTTP/2) für Clients und Worker; Einblick (HTTP/1.1 + 2) für Werkbank, Webhook, /health.
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.ListenAnyIP(u.Transport.GrpcPort, l => l.Protocols = HttpProtocols.Http2);
            if (u.Transport.EinblickPort > 0) k.ListenAnyIP(u.Transport.EinblickPort, l => l.Protocols = HttpProtocols.Http1AndHttp2);
        });
        s.AddGrpc();

        // Das Siegel ZUERST (Hosted Services starten in Registrierungs-Reihenfolge): passt die DB nicht zur Umgebung, startet nichts.
        if (u.Cluster.Rolle != KnotenRolle.Migrator) s.AddHostedService<UmgebungsSiegel>();
        s.AddSingleton(new RedisPraefix(u.VersionIndex.Praefix));

        s.AddCqrsFramework(o =>
        {
            o.EventStoreConnectionString = UmgebungLader.Verbindung(u.EventStore);
            o.EventStoreSchema = u.EventStore.Schema;
            o.CommandTimeoutSeconds = u.EventStore.BefehlsTimeoutSekunden;
            o.EnableVersionTracking = u.VersionIndex.Betrieb != Betrieb.Aus;
            o.RedisConnectionString = u.VersionIndex.Endpunkt;
            o.RedisDatabase = u.VersionIndex.Datenbank;
            o.Einzelknoten = u.Cluster.Art == ClusterArt.Einzelknoten;
            o.ClusterName = u.Cluster.Name;
            o.ConsulAddress = u.Cluster.Consul;
            o.AdvertisedHost = u.Cluster.AdvertisedHost;
            o.SchemaRole = u.Cluster.Rolle switch
            {
                KnotenRolle.Migrator => MartenSchemaRole.Migrator,
                KnotenRolle.Mitglied => MartenSchemaRole.Member,
                _ => MartenSchemaRole.Standalone,
            };
            o.UseGeneratedJsonSerializer = u.Schreibpfad.GeneriertesJson;
            o.AppendBatching = u.Schreibpfad.GroupCommit;
            o.AppendBatchMaxSize = u.Schreibpfad.BatchGroesse;
            o.AppendBatchLingerMs = u.Schreibpfad.LingerMs;
            o.AppendDrainParallelism = u.Schreibpfad.Drain;
            o.SnapshotThreshold = u.Schreibpfad.SnapshotSchwelle;
            o.InboxHorizont = TimeSpan.FromDays(u.Schreibpfad.InboxHorizontTage);
            o.DbUhr = UmgebungsDbClock.Ausdruck(u);   // eine Uhr: IDbClock und Event-Zeitstempel (G-10)
            o.BuendelGroesse = u.Schreibpfad.BuendelGroesse;
            o.Passivierung = Infrastructure.Lebenszyklus.Passivierung.Aus(u.Lebenszyklus);
            o.Konsum = Infrastructure.Projections.Konsum.Aus(u.Konsum);
            o.EnableGrpc = anwendung.Nutzlast != null;   // gRPC nur mit Leitung; ohne bleiben JSON-Eingang und Quellen
        });
        // Die Uhr der Umgebung (Versatz nur in Sim) ersetzt die nackte DB-Uhr — die letzte Registrierung gewinnt.
        s.AddSingleton<IDbClock>(_ => new UmgebungsDbClock(u));

        // Passt die Leseseite zum Code (konzept-werkbank §12.3 E9)? Vor dem Pull-Pfad (Hosted Services starten in Registrierungs-Reihenfolge).
        if (u.Cluster.Rolle != KnotenRolle.Mitglied) s.AddHostedService<Infrastructure.Projections.ProjektionsStandDienst>();
        Anwendung.Aktuell.RegistrierePullPfade(s);
        s.AddGeneratedProzesse();
        Anwendung.Pipelines.RegisterAllPipelines(s);
        Anwendung.Pipelines.AddGeneratedPipelineEventPulls(s);
        s.AddBackendMonitoring();
        s.AddDeadlines(Anwendung.Pipelines.BaueFrist);
        s.AddAkteure(builder.Configuration.GetSection(UmgebungsAbschnitte.Identitaeten));
        s.AddQuellen(builder.Configuration);
        s.BindeFunktionen(u.Funktionen);
        s.BindeDienste(u, builder.Configuration);   // nach den Funktionen: gebunden wird, was deren Implementierungen brauchen
        s.AddEinblick(u);
        return builder;
    }

    /// <summary>
    /// Startet den Host: als Migrator nur das Schema anlegen und enden (Multi-Node-Kaltstart), sonst gRPC, Monitoring, Einblick, JSON-Eingang,
    /// die Webhooks der Quellen (aus der Konfig) und Lauf.
    /// </summary>
    public static async Task LaufeAsync(this WebApplication app, UmgebungKonfig u)
    {
        if (u.Cluster.Rolle == KnotenRolle.Migrator)
        {
            Console.WriteLine($"[CQRS] Schema-Migrator ({u.Umgebung.Name}) — lege alle Marten-Objekte eager + lock-gesichert an …");
            await CqrsSchemaMigrator.ApplyAllAsync(app.Services);
            Console.WriteLine("[CQRS] Schema vollständig migriert — Ende.");
            return;
        }
        try
        {
            if (Anwendung.Aktuell.Nutzlast != null) app.MapCqrsGrpcService();
            app.MapBackendMonitoring();
            app.MapEinblick(u);
            app.MapEingang(u);
            app.MapQuellWebhooks(u);   // Wege hinein aus der Konfig (Quellen:<Typ>:Webhook) — keine Zeile im Programm
        }
        catch (Exception e) { CqrsBoot.Melde(e); throw; }
        Console.WriteLine();
        Console.WriteLine($"  CQRS/ES Host · Umgebung {u.Umgebung.Name} ({u.Umgebung.Art}) · gRPC :{u.Transport.GrpcPort}"
            + (u.Transport.EinblickPort > 0 ? $" · Einblick :{u.Transport.EinblickPort}" + (u.Transport.JsonEingang ? " (+ JSON-Eingang)" : "") : "") + $" · DB {u.EventStore.Datenbank} · Cluster {u.Cluster.Name} ({u.Cluster.Art})");
        Console.WriteLine();
        try { await app.RunAsync(); }
        catch (Exception e) { CqrsBoot.Melde(e); throw; }
    }
}
