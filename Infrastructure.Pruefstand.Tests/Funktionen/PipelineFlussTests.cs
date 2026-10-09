using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Aggregate;     // KommandoVerarbeitet / KommandoAbgelehnt
using Infrastructure.Funktionen;
using Infrastructure.Prozess;
using Infrastructure.Quellen;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen.Fluss;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// Ebene 1 — PIPELINE ALS FLUSS (docs/konzept-editor-pipelines.md §14) gegen den ECHTEN Dirigenten (ProzessManager) und den
// echten Funktions-Ausführer. Die Funktionen sind Fakes, das Aggregat ein Responder wie im emittierten Pfad. Bewiesen werden
// die Fluss-Formen: parallel (ein Port, mehrere Drähte), verzweigen (OneOf-Ports), ∧ (Alle), ∨ (Oder), Fehler-Ports
// (⏳ Zeitlimit, ✕ abgelehnt), Je-Rahmen mit Sammeln — und die Knoten-Identität (dieselbe Funktion zweimal im Fluss).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

// ── Katalog (domänenfrei) ──
public sealed record DateiDa(string Pfad, string Name) : IQuellNachricht { public string Kennung => Pfad; }

public sealed record DeuteName(string Name) : IAuftrag<INameDeuten>;
public sealed record Bilddatei(Guid PaarId, string Pfad) : IEvent;
public sealed record Unbekannt(string Name) : IEvent;
public interface INameDeuten : IFunktion { Task<OneOf<Bilddatei, Unbekannt>> RufeAsync(DeuteName a, IAusfuehrung x); }

public sealed record Verkleinere(string Pfad, int Hoehe) : IAuftrag<IVerkleinern>;
public sealed record Verkleinert(string Pfad, int Hoehe) : IEvent;
public interface IVerkleinern : IFunktion { Task<OneOf<Verkleinert>> RufeAsync(Verkleinere a, IAusfuehrung x); }

public sealed record GleicheAus(string Pfad) : IAuftrag<IAusgleich>;
public sealed record Ausgeglichen(string Pfad) : IEvent;
public interface IAusgleich : IFunktion { Task<OneOf<Ausgeglichen>> RufeAsync(GleicheAus a, IAusfuehrung x); }

// ── Aggregat-Welt ──
public sealed record LegePaarAn(Guid AggregateId, string Pfad) : ICommand;
public sealed record PaarAngelegt(string Pfad) : IEvent;
public sealed record MeldeBild(Guid AggregateId, string Vorschau, string Daumen) : ICommand;
public sealed record BildGemeldet(string Vorschau, string Daumen) : IEvent;

/// <summary>Beispiel 1: Datei → Name deuten → (Paar anlegen ∥ Vorschau → Kontrast ∥ Daumen) → ∧ melden.</summary>
public sealed class Bildeingang : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var datei    = p.Quelle<DateiDa>();
        var deuten   = datei.Rufe<INameDeuten>(d => new DeuteName(d.Name));
        var paar     = deuten.Bei<Bilddatei>().Sende<LegePaarAn>(b => new LegePaarAn(b.PaarId, b.Pfad));
        var vorschau = deuten.Bei<Bilddatei>().Rufe<IVerkleinern>(b => new Verkleinere(b.Pfad, 512));
        var kontrast = vorschau.Bei<Verkleinert>().Rufe<IAusgleich>(v => new GleicheAus(v.Pfad));
        var daumen   = deuten.Bei<Bilddatei>().Rufe<IVerkleinern>(b => new Verkleinere(b.Pfad, 128));
        p.Alle(kontrast.Bei<Ausgeglichen>(), daumen.Bei<Verkleinert>(), deuten.Bei<Bilddatei>())
         .Sende<MeldeBild>((k, t, b) => new MeldeBild(b.PaarId, k.Pfad, t.Pfad));
    });
}

// ── Beispiel 2: Bestellung (Verzweigung, ∨, Fehler-Ports) ──
public sealed record BestellungEingegangen(Guid BestellId, string Zahlart, decimal Betrag) : IQuellNachricht
{ public string Kennung => BestellId.ToString(); }

public sealed record PruefeZahlart(string Zahlart) : IAuftrag<IZahlart>;
public sealed record PerKarte : IEvent;
public sealed record PerRechnung : IEvent;
public interface IZahlart : IFunktion { Task<OneOf<PerKarte, PerRechnung>> RufeAsync(PruefeZahlart a, IAusfuehrung x); }

public sealed record Belaste(decimal Betrag) : IAuftrag<IKarte>;
public sealed record Belastet(string Transaktion) : IEvent;
public sealed record KarteAbgelehnt(string Grund) : IEvent;
public interface IKarte : IFunktion { Task<OneOf<Belastet, KarteAbgelehnt>> RufeAsync(Belaste a, IAusfuehrung x); }

public sealed record PruefeBonitaet(decimal Betrag) : IAuftrag<IBonitaet>;
public sealed record BonitaetOk : IEvent;
public interface IBonitaet : IFunktion { Task<OneOf<BonitaetOk>> RufeAsync(PruefeBonitaet a, IAusfuehrung x); }

public sealed record LegeBestellungAn(Guid AggregateId, string Weg) : ICommand;
public sealed record BestellungAngelegt(string Weg) : IEvent;
public sealed record LehneBestellungAb(Guid AggregateId, string Grund) : ICommand;
public sealed record BestellungAbgelehnt(string Grund) : IEvent;
public sealed record Benachrichtige(Guid AggregateId, string Text) : ICommand;
public sealed record Benachrichtigt(string Text) : IEvent;

public sealed class Bestellung : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var eingang = p.Quelle<BestellungEingegangen>();
        var zahlart = eingang.Rufe<IZahlart>(e => new PruefeZahlart(e.Zahlart));
        var karte   = p.Alle(zahlart.Bei<PerKarte>(), eingang).Rufe<IKarte>((_, e) => new Belaste(e.Betrag))
                       .Zeitlimit(TimeSpan.FromSeconds(30));
        var boni    = p.Alle(zahlart.Bei<PerRechnung>(), eingang).Rufe<IBonitaet>((_, e) => new PruefeBonitaet(e.Betrag));
        var anlegen = p.Alle(karte.Bei<Belastet>(), eingang).Sende<LegeBestellungAn>((b, e) => new LegeBestellungAn(e.BestellId, "karte:" + b.Transaktion))
                       .Oder(p.Alle(boni.Bei<BonitaetOk>(), eingang), (_, e) => new LegeBestellungAn(e.BestellId, "rechnung"));
        var ablehnen = p.Alle(karte.Bei<KarteAbgelehnt>(), eingang).Sende<LehneBestellungAb>((k, e) => new LehneBestellungAb(e.BestellId, k.Grund))
                       .Oder(p.Alle(karte.BeiZeitlimit(), eingang), (z, e) => new LehneBestellungAb(e.BestellId, "zeit"));
        // ✕-Port: lehnt das Aggregat das Anlegen ab, geht eine Benachrichtigung raus, statt den Vorgang scheitern zu lassen.
        p.Alle(anlegen.BeiAbgelehnt(), eingang).Sende<Benachrichtige>((a, e) => new Benachrichtige(e.BestellId, "nicht angelegt: " + a.Grund));
    });
}

// ── Beispiel 3: Video (Je-Rahmen mit ∧ je Element und Sammeln) ──
public sealed record VideoDa(string Pfad) : IQuellNachricht { public string Kennung => Pfad; }

public sealed record Zerlege(string Pfad) : IAuftrag<IZerlegen>;
public sealed record Zerlegt(IReadOnlyList<string> Bilder) : IEvent;
public interface IZerlegen : IFunktion { Task<OneOf<Zerlegt>> RufeAsync(Zerlege a, IAusfuehrung x); }

public sealed record Erkenne(string Bild) : IAuftrag<IErkennen>;
public sealed record Erkannt(string Bild, string Objekt) : IEvent;
public interface IErkennen : IFunktion { Task<OneOf<Erkannt>> RufeAsync(Erkenne a, IAusfuehrung x); }

public sealed record Miss(string Bild) : IAuftrag<IMessen>;
public sealed record Gemessen(string Bild, int Hell) : IEvent;
public interface IMessen : IFunktion { Task<OneOf<Gemessen>> RufeAsync(Miss a, IAusfuehrung x); }

public sealed record Bewerte(string Objekt, int Hell) : IAuftrag<IBewerten>;
public sealed record Bewertet(string Note) : IEvent;
public interface IBewerten : IFunktion { Task<OneOf<Bewertet>> RufeAsync(Bewerte a, IAusfuehrung x); }

public sealed record SpeichereBericht(Guid AggregateId, string Bericht) : ICommand;
public sealed record BerichtGespeichert(string Bericht) : IEvent;

public sealed class Video : IPipeline
{
    public static readonly Guid Akte = Guid.Parse("00000000-0000-0000-0000-0000000000f1");

    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var video    = p.Quelle<VideoDa>();
        var zerlegen = video.Rufe<IZerlegen>(v => new Zerlege(v.Pfad));
        var je       = zerlegen.Bei<Zerlegt>().Je(z => z.Bilder);
        var erkennen = je.Rufe<IErkennen>(b => new Erkenne(b));
        var messen   = je.Rufe<IMessen>(b => new Miss(b));
        var bewerten = p.Alle(erkennen.Bei<Erkannt>(), messen.Bei<Gemessen>()).Rufe<IBewerten>((e, m) => new Bewerte(e.Objekt, m.Hell));
        je.Sammle(bewerten.Bei<Bewertet>())
          .Sende<SpeichereBericht>((z, noten) => new SpeichereBericht(Akte, string.Join(",", noten.Select(n => n.Note))));
    });
}

// ── Beispiel 4: Frist als Rennen (Strom + Warten gegen ein Zeitlimit) ──
public sealed record LaufBegonnen : IEvent;
public sealed record LaufFertig(string Modell) : IEvent;
public sealed record LaufAbgebrochen : IEvent;
public sealed record MarkiereHaengend(Guid AggregateId) : ICommand;
public sealed record HaengendMarkiert : IEvent;

/// <summary>Wächter: endet der Lauf nicht binnen 6 h (Fertig ODER Abgebrochen im SELBEN Strom), wird er als hängend markiert.</summary>
public sealed class LaufWaechter : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var begonnen = p.Auf<LaufBegonnen>();
        var ende = begonnen.Strom().Warte<LaufFertig, LaufAbgebrochen>().Zeitlimit(TimeSpan.FromHours(6));
        p.Alle(ende.BeiZeitlimit(), begonnen.Strom()).Sende<MarkiereHaengend>((endeZeit, begonnenStrom) => new MarkiereHaengend(begonnenStrom.Id));
    });
}

/// <summary>
/// Die Welt eines Laufs: echter Dirigent + echter Ausführer + Log im Speicher. Funktionen und Aggregat sind Delegaten.
/// </summary>
internal sealed class FlussWelt
{
    public LogImSpeicher Log { get; } = new();
    public ProzessManager Manager { get; }
    public FunktionsAusfuehrer Ausfuehrer { get; }
    public ConcurrentQueue<IAuftrag> Gerufen { get; } = new();
    public List<ICommand> Gesendet { get; } = new();
    public HashSet<Type> NieAusfuehren { get; } = new();
    public HashSet<Type> LehneAb { get; } = new();
    public Guid Korrelation { get; } = Guid.NewGuid();
    private readonly string _name;
    private readonly Func<IAuftrag, IEvent> _funktionen;
    private readonly ConcurrentQueue<Guid> _weckungen = new();

    public FlussWelt(IPipeline pipeline, Func<IAuftrag, IEvent> funktionen)
    {
        _name = pipeline.GetType().Name;
        _funktionen = funktionen;
        Ausfuehrer = new FunktionsAusfuehrer(Log,
            (auftrag, _) => { Gerufen.Enqueue(auftrag); return Task.FromResult(_funktionen(auftrag)); },
            (k, _) => { _weckungen.Enqueue(k); return Task.CompletedTask; },
            a => new FunktionsBindung(typeof(IFunktion), Slots: 8),
            wiederholPause: TimeSpan.FromMilliseconds(1));
        Manager = new ProzessManager(Log,
            new Dictionary<string, ProzessRegeln> { [_name] = pipeline.Fluss.Regeln },
            DispatchAsync,
            rufe: (k, a, v, akt, ct) => NieAusfuehren.Contains(a.GetType()) ? Task.CompletedTask : Ausfuehrer.BeauftrageAsync(k, a, v, akt, ct),
            jetzt: _ => Task.FromResult(Log.Jetzt));
    }

    /// <summary>Das Aggregat: Command → Event (Name + „t“) samt Inbox-Marke, oder die Ablehnungs-Marke.</summary>
    private async Task DispatchAsync(Guid korrelation, ICommand cmd, Guid vorgang, CancellationToken ct)
    {
        lock (Gesendet) Gesendet.Add(cmd);
        var stream = Log.Stream(cmd.AggregateId);
        if (stream.Any(e => e.CausationId == vorgang.ToString())) return;
        IEvent[] evs = LehneAb.Contains(cmd.GetType())
            ? new IEvent[] { new KommandoAbgelehnt(vorgang, cmd.GetType().Name + " abgelehnt") }
            : new[] { Antwort(cmd), new KommandoVerarbeitet(vorgang) };
        await Log.AppendEventsAsync(cmd.AggregateId, stream.Count, evs, korrelation.ToString(), vorgang.ToString(), "Akte");
    }

    private static IEvent Antwort(ICommand cmd) => cmd switch
    {
        LegePaarAn c => new PaarAngelegt(c.Pfad),
        MeldeBild c => new BildGemeldet(c.Vorschau, c.Daumen),
        LegeBestellungAn c => new BestellungAngelegt(c.Weg),
        LehneBestellungAb c => new BestellungAbgelehnt(c.Grund),
        Benachrichtige c => new Benachrichtigt(c.Text),
        MarkiereHaengend => new HaengendMarkiert(),
        SpeichereBericht c => new BerichtGespeichert(c.Bericht),
        _ => throw new NotSupportedException(cmd.GetType().Name),
    };

    public async Task<Guid> StarteAsync(IEvent quellNachricht)
    {
        var stream = Guid.NewGuid();
        await Log.AppendEventsAsync(stream, 0, new[] { quellNachricht }, Korrelation.ToString(), null, "Quelle");
        await Manager.StarteAsync(Korrelation, _name, stream, 1);
        return stream;
    }

    public async Task<ProzessManager.ManagerStatus> TreibeAsync(int max = 40, Guid? korrelation = null)
    {
        var korr = korrelation ?? Korrelation;
        for (var i = 0; i < max; i++)
        {
            await Ausfuehrer.WarteAufAlleAsync();
            while (_weckungen.TryDequeue(out _)) { }
            var st = await Manager.LadeStatusAsync(korr);
            if (st.Beendet) return st;
            await Manager.WakeAsync(korr);
        }
        return await Manager.LadeStatusAsync(korr);
    }

    public IReadOnlyList<T> Gesendete<T>() where T : ICommand { lock (Gesendet) return Gesendet.OfType<T>().Distinct().ToList(); }
    public IReadOnlyList<T> Aufträge<T>() where T : IAuftrag => Gerufen.OfType<T>().Distinct().ToList();
}

public class PipelineFlussTests
{
    private static readonly Guid Paar = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static IEvent BildFunktionen(IAuftrag a) => a switch
    {
        DeuteName d when d.Name.EndsWith(".tiff") => new Bilddatei(Paar, "/roh/" + d.Name),
        DeuteName d => new Unbekannt(d.Name),
        Verkleinere v => new Verkleinert($"{v.Pfad}_{v.Hoehe}.png", v.Hoehe),
        GleicheAus g => new Ausgeglichen(g.Pfad + "_kontrast.png"),
        _ => throw new NotSupportedException(a.GetType().Name),
    };

    [Fact]
    public async Task Bildeingang_dieselbe_Funktion_zweimal_jeder_Draht_kennt_seinen_Knoten()
    {
        var w = new FlussWelt(new Bildeingang(), BildFunktionen);
        await w.StarteAsync(new DateiDa("/share/a.tiff", "a.tiff"));
        var st = await w.TreibeAsync();

        st.Beendet.Should().BeTrue();
        st.Erfolg.Should().BeTrue();
        // parallel aus EINEM Port: Paar anlegen, Vorschau (512) und Daumen (128) laufen alle.
        w.Gesendete<LegePaarAn>().Should().ContainSingle();
        w.Aufträge<Verkleinere>().Select(v => v.Hoehe).Should().BeEquivalentTo(new[] { 512, 128 });
        // Knoten-Identität: der Kontrast folgt NUR der Vorschau — nicht dem Daumen, obwohl beide „Verkleinert“ liefern.
        w.Aufträge<GleicheAus>().Should().ContainSingle().Which.Pfad.Should().Be("/roh/a.tiff_512.png");
        // ∧ über drei Drähte (inkl. Kontext aus dem Deuten): genau eine Meldung mit den richtigen Pfaden.
        var meldung = w.Gesendete<MeldeBild>().Should().ContainSingle().Subject;
        meldung.AggregateId.Should().Be(Paar);
        meldung.Vorschau.Should().Be("/roh/a.tiff_512.png_kontrast.png");
        meldung.Daumen.Should().Be("/roh/a.tiff_128.png");
    }

    [Fact]
    public async Task Bildeingang_Verzweigung_unbekannte_Datei_nimmt_den_anderen_Port()
    {
        var w = new FlussWelt(new Bildeingang(), BildFunktionen);
        await w.StarteAsync(new DateiDa("/share/notiz.txt", "notiz.txt"));
        var st = await w.TreibeAsync();

        st.Erfolg.Should().BeTrue();
        w.Gesendet.Should().BeEmpty();
        w.Aufträge<Verkleinere>().Should().BeEmpty();
    }

    private static Func<IAuftrag, IEvent> Bestellfunktionen(bool karteOk = true) => a => a switch
    {
        PruefeZahlart z when z.Zahlart == "karte" => new PerKarte(),
        PruefeZahlart => new PerRechnung(),
        Belaste => karteOk ? new Belastet("T-1") : new KarteAbgelehnt("gesperrt"),
        PruefeBonitaet => new BonitaetOk(),
        _ => throw new NotSupportedException(a.GetType().Name),
    };

    [Theory]
    [InlineData("karte", "karte:T-1")]
    [InlineData("rechnung", "rechnung")]
    public async Task Bestellung_Oder_jeder_Weg_legt_genau_einmal_an(string zahlart, string weg)
    {
        var id = Guid.NewGuid();
        var w = new FlussWelt(new Bestellung(), Bestellfunktionen());
        await w.StarteAsync(new BestellungEingegangen(id, zahlart, 42m));
        var st = await w.TreibeAsync();

        st.Erfolg.Should().BeTrue();
        w.Gesendete<LegeBestellungAn>().Should().ContainSingle().Which.Should().Be(new LegeBestellungAn(id, weg));
        w.Gesendete<LehneBestellungAb>().Should().BeEmpty();
    }

    [Fact]
    public async Task Bestellung_Ergebnis_Fall_der_Funktion_fuehrt_auf_den_Ablehnungs_Weg()
    {
        var id = Guid.NewGuid();
        var w = new FlussWelt(new Bestellung(), Bestellfunktionen(karteOk: false));
        await w.StarteAsync(new BestellungEingegangen(id, "karte", 42m));
        (await w.TreibeAsync()).Erfolg.Should().BeTrue();

        w.Gesendete<LehneBestellungAb>().Should().ContainSingle().Which.Grund.Should().Be("gesperrt");
        w.Gesendete<LegeBestellungAn>().Should().BeEmpty();
    }

    [Fact]
    public async Task Bestellung_Zeitlimit_Port_verdrahtet_wird_zum_eigenen_Weg_statt_zu_scheitern()
    {
        var id = Guid.NewGuid();
        var w = new FlussWelt(new Bestellung(), Bestellfunktionen());
        w.NieAusfuehren.Add(typeof(Belaste));      // die Karte antwortet nie
        await w.StarteAsync(new BestellungEingegangen(id, "karte", 42m));
        (await w.TreibeAsync(max: 5)).Beendet.Should().BeFalse("die Karte läuft noch, das Limit ist nicht erreicht");

        w.Log.Jetzt += TimeSpan.FromSeconds(31);
        var st = await w.TreibeAsync();

        st.Beendet.Should().BeTrue();
        st.Erfolg.Should().BeTrue("der ⏳-Port ist verdrahtet — der Vorgang nimmt den Zeit-Weg und scheitert nicht");
        w.Gesendete<LehneBestellungAb>().Should().ContainSingle().Which.Grund.Should().Be("zeit");
        w.Gesendete<LegeBestellungAn>().Should().BeEmpty();
        w.Manager.LadeStatusAsync(w.Korrelation).Result.Umgeleitet.Values.Should().ContainSingle(u => u.Art == "zeitlimit");
    }

    [Fact]
    public async Task Bestellung_Abgelehnt_Port_des_Aggregats_fuehrt_weiter()
    {
        var id = Guid.NewGuid();
        var w = new FlussWelt(new Bestellung(), Bestellfunktionen());
        w.LehneAb.Add(typeof(LegeBestellungAn));
        await w.StarteAsync(new BestellungEingegangen(id, "rechnung", 42m));
        var st = await w.TreibeAsync();

        st.Erfolg.Should().BeTrue();
        w.Gesendete<Benachrichtige>().Should().ContainSingle().Which.Text.Should().StartWith("nicht angelegt: LegeBestellungAn abgelehnt");
    }

    [Fact]
    public async Task Ohne_verdrahteten_Port_scheitert_der_Vorgang_wie_bisher()
    {
        // Bildeingang hat keinen ✕-Port am Paar-Anlegen → eine Ablehnung beendet den Vorgang erfolglos.
        var w = new FlussWelt(new Bildeingang(), BildFunktionen);
        w.LehneAb.Add(typeof(LegePaarAn));
        await w.StarteAsync(new DateiDa("/share/a.tiff", "a.tiff"));
        var st = await w.TreibeAsync();

        st.Beendet.Should().BeTrue();
        st.Erfolg.Should().BeFalse();
    }

    private static IEvent VideoFunktionen(IAuftrag a) => a switch
    {
        Zerlege z when z.Pfad == "leer.mp4" => new Zerlegt(Array.Empty<string>()),
        Zerlege => new Zerlegt(new[] { "b0", "b1", "b2" }),
        Erkenne e => new Erkannt(e.Bild, "objekt-" + e.Bild),
        Miss m => new Gemessen(m.Bild, int.Parse(m.Bild[1..]) * 10),
        Bewerte b => new Bewertet($"{b.Objekt}:{b.Hell}"),
        _ => throw new NotSupportedException(a.GetType().Name),
    };

    [Fact]
    public async Task Video_Je_Rahmen_verbindet_nur_Ergebnisse_desselben_Elements_und_sammelt_geordnet()
    {
        var w = new FlussWelt(new Video(), VideoFunktionen);
        await w.StarteAsync(new VideoDa("film.mp4"));
        var st = await w.TreibeAsync();

        st.Erfolg.Should().BeTrue();
        w.Aufträge<Erkenne>().Should().HaveCount(3);
        w.Aufträge<Miss>().Should().HaveCount(3);
        // ∧ im Rahmen: je Element genau eine Bewertung, nie über Kreuz (3, nicht 9).
        w.Aufträge<Bewerte>().Should().BeEquivalentTo(new[]
        {
            new Bewerte("objekt-b0", 0), new Bewerte("objekt-b1", 10), new Bewerte("objekt-b2", 20),
        });
        w.Gesendete<SpeichereBericht>().Should().ContainSingle()
            .Which.Bericht.Should().Be("objekt-b0:0,objekt-b1:10,objekt-b2:20");
    }

    [Fact]
    public async Task Video_leere_Liste_sammelt_sofort()
    {
        var w = new FlussWelt(new Video(), VideoFunktionen);
        await w.StarteAsync(new VideoDa("leer.mp4"));
        var st = await w.TreibeAsync();

        st.Erfolg.Should().BeTrue();
        w.Gesendete<SpeichereBericht>().Should().ContainSingle().Which.Bericht.Should().BeEmpty();
    }

    [Fact]
    public async Task Quelle_nimmt_dieselbe_Nachricht_genau_einmal_auf_und_startet_den_Fluss()
    {
        var w = new FlussWelt(new Bildeingang(), BildFunktionen);
        var starts = new List<Guid>();
        var eingang = new QuellEingang(w.Log,
            new Dictionary<string, ProzessRegeln> { [nameof(Bildeingang)] = new Bildeingang().Fluss.Regeln, ["Fremd"] = new Video().Fluss.Regeln },
            async (korr, stream, version, name, ct) => { starts.Add(korr); await w.Manager.StarteAsync(korr, name, stream, version, ct); },
            typ => typ == typeof(DateiDa) ? "Kamera" : null);

        var datei = new DateiDa("/share/b.tiff", "b.tiff");
        var s1 = await eingang.NimmAufAsync(datei, default);
        var s2 = await eingang.NimmAufAsync(datei with { }, default);   // Re-Scan / zweiter Knoten

        s1.Should().Be(s2);
        w.Log.Stream(s1).Should().ContainSingle().Which.UserId.Should().Be("Kamera");
        starts.Distinct().Should().ContainSingle().Which.Should().Be(ProzessId.Für(nameof(Bildeingang), s1, 1));
        eingang.PipelinesFür(typeof(DateiDa)).Should().Equal(nameof(Bildeingang));

        var st = await w.TreibeAsync(korrelation: starts[0]);
        st.Erfolg.Should().BeTrue();
        w.Gesendete<MeldeBild>().Should().ContainSingle();
        w.Gesendete<LegePaarAn>().Should().ContainSingle("die zweite Aufnahme derselben Datei startet keinen zweiten Vorgang");
    }

    [Fact]
    public async Task Warten_endet_still_wenn_das_Event_im_Strom_vor_dem_Zeitlimit_kommt()
    {
        var w = new FlussWelt(new LaufWaechter(), a => throw new NotSupportedException());
        var lauf = await w.StarteAsync(new LaufBegonnen());
        (await w.TreibeAsync(max: 3)).Beendet.Should().BeFalse("der Wächter wartet");

        w.Log.Jetzt += TimeSpan.FromHours(2);
        await w.Log.AppendEventsAsync(lauf, 1, new IEvent[] { new LaufFertig("m.pt") }, Guid.NewGuid().ToString(), null, "Lauf");
        w.Log.Jetzt += TimeSpan.FromHours(5);   // die Frist ist jetzt vorbei — aber das Ende kam vorher
        var st = await w.TreibeAsync();

        st.Erfolg.Should().BeTrue();
        w.Gesendet.Should().BeEmpty("das Rennen hat das Ende gewonnen — keine Markierung");
    }

    [Fact]
    public async Task Warten_nimmt_den_Zeitlimit_Port_und_der_Strom_liefert_die_Aggregat_Id()
    {
        var w = new FlussWelt(new LaufWaechter(), a => throw new NotSupportedException());
        var lauf = await w.StarteAsync(new LaufBegonnen());
        w.Log.Jetzt += TimeSpan.FromHours(6) + TimeSpan.FromSeconds(1);
        var st = await w.TreibeAsync();

        st.Erfolg.Should().BeTrue("⏳ ist verdrahtet");
        w.Gesendete<MarkiereHaengend>().Should().ContainSingle().Which.AggregateId.Should().Be(lauf, "quelle.Strom() trägt die Stream-Id");
    }

    [Fact]
    public async Task Ein_Event_nach_dem_Zeitlimit_zaehlt_nicht()
    {
        var w = new FlussWelt(new LaufWaechter(), a => throw new NotSupportedException());
        var lauf = await w.StarteAsync(new LaufBegonnen());
        w.Log.Jetzt += TimeSpan.FromHours(7);
        // Das Ende wird erst nach der Frist geschrieben, die Weckung kommt danach: die Frist hat gewonnen.
        await w.Log.AppendEventsAsync(lauf, 1, new IEvent[] { new LaufAbgebrochen() }, Guid.NewGuid().ToString(), null, "Lauf");
        (await w.TreibeAsync()).Erfolg.Should().BeTrue();

        w.Gesendete<MarkiereHaengend>().Should().ContainSingle();
    }

    [Fact]
    public void Warten_ohne_Zeitlimit_ist_ein_Formfehler()
    {
        var ohne = () => PipelineFluss.Definiere(p => p.Auf<LaufBegonnen>().Strom().Warte<LaufFertig>());
        ohne.Should().Throw<InvalidOperationException>().WithMessage("*ohne Zeitlimit*");
        new LaufWaechter().Fluss.Knoten.Select(k => k.Art)
            .Should().Equal(PipelineKnotenArt.Auf, PipelineKnotenArt.Warte, PipelineKnotenArt.Command);
    }

    [Fact]
    public void Ein_Fluss_ist_azyklisch_auch_wenn_dieselbe_Funktion_hintereinander_steht()
    {
        var fluss = PipelineFluss.Definiere(p =>
        {
            var d = p.Quelle<DateiDa>();
            var a = d.Rufe<IVerkleinern>(x => new Verkleinere(x.Pfad, 512));
            a.Bei<Verkleinert>().Rufe<IVerkleinern>(v => new Verkleinere(v.Pfad, 128));
        });
        fluss.Regeln.IstFluss.Should().BeTrue();
        var prüfe = () => ProzessAzyklizität.Prüfe("x", fluss.Regeln, _ => new[] { typeof(Verkleinert) });
        prüfe.Should().NotThrow();
        fluss.Knoten.Select(k => k.Art).Should().Equal(PipelineKnotenArt.Quelle, PipelineKnotenArt.Funktion, PipelineKnotenArt.Funktion);
    }

    [Fact]
    public void Formfehler_fallen_beim_Definieren_auf()
    {
        var zweiQuellen = () => PipelineFluss.Definiere(p => { p.Quelle<DateiDa>(); p.Quelle<VideoDa>(); });
        zweiQuellen.Should().Throw<InvalidOperationException>().WithMessage("*genau eine Quelle*");

        var ohneQuelle = () => PipelineFluss.Definiere(_ => { });
        ohneQuelle.Should().Throw<InvalidOperationException>().WithMessage("*braucht eine Quelle*");

        var zeitPortOhneLimit = () => PipelineFluss.Definiere(p =>
        {
            var k = p.Quelle<DateiDa>().Rufe<IVerkleinern>(x => new Verkleinere(x.Pfad, 1));
            k.BeiZeitlimit().Sende<LegePaarAn>(z => new LegePaarAn(Guid.Empty, z.Grund));
        });
        zeitPortOhneLimit.Should().Throw<InvalidOperationException>().WithMessage("*kein Zeitlimit*");

        Draht<DateiDa>? fremd = null;
        PipelineFluss.Definiere(p => fremd = p.Quelle<DateiDa>());
        var fremderDraht = () => PipelineFluss.Definiere(p =>
        {
            var eigen = p.Quelle<VideoDa>();
            p.Alle(eigen, fremd!).Sende<LegePaarAn>((v, d) => new LegePaarAn(Guid.Empty, d.Pfad));
        });
        fremderDraht.Should().Throw<InvalidOperationException>().WithMessage("*anderen Pipeline*");
    }

    [Fact]
    public void Der_Regel_Hash_unterscheidet_Fluesse_mit_gleichen_Typen_aber_anderer_Verdrahtung()
    {
        var a = PipelineFluss.Definiere(p =>
        {
            var d = p.Quelle<DateiDa>();
            var x = d.Rufe<IVerkleinern>(q => new Verkleinere(q.Pfad, 1));
            var y = d.Rufe<IVerkleinern>(q => new Verkleinere(q.Pfad, 2));
            x.Bei<Verkleinert>().Rufe<IAusgleich>(v => new GleicheAus(v.Pfad));
        });
        var b = PipelineFluss.Definiere(p =>
        {
            var d = p.Quelle<DateiDa>();
            var x = d.Rufe<IVerkleinern>(q => new Verkleinere(q.Pfad, 1));
            var y = d.Rufe<IVerkleinern>(q => new Verkleinere(q.Pfad, 2));
            y.Bei<Verkleinert>().Rufe<IAusgleich>(v => new GleicheAus(v.Pfad));
        });
        ProzessRegelHash.Berechne(a.Regeln).Should().NotBe(ProzessRegelHash.Berechne(b.Regeln));
    }
}
