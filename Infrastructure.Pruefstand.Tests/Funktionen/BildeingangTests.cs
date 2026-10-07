using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.Bildaufbereitung;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Aggregate;     // KommandoVerarbeitet / KommandoAbgelehnt
using Infrastructure.Funktionen;
using Infrastructure.Prozess;
using Infrastructure.Quellen;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Ebene 1 — der ECHTE <see cref="Bildeingang"/> der Domäne als Pipeline-Fluss (docs/konzept-editor-pipelines.md §14): Datei →
/// Name deuten → Paar anlegen ∥ Verkleinern → Histogramm → ∧ Meldung. Echter Dirigent, echter Ausführer, Quelle über den
/// <see cref="QuellEingang"/>; die Funktionen sind Fakes (OpenCV läuft live im Host), das ImagePair ein Responder wie der emittierte
/// Pfad (Event + Inbox-Marke bzw. Ablehnungs-Marke). Bewiesen: erste UND zweite Datei eines Paars werden gemeldet (die zweite über
/// den ✕-Port des abgelehnten ErstelleImagePair), ein unbekannter Name und ein unlesbares Bild enden ohne Meldung.
/// </summary>
public class BildeingangTests
{
    private sealed class Welt
    {
        public LogImSpeicher Log { get; } = new();
        public ProzessManager Manager { get; }
        public FunktionsAusfuehrer Ausfuehrer { get; }
        public QuellEingang Eingang { get; }
        public List<ICommand> Gesendet { get; } = new();
        public ConcurrentQueue<string> Gerufen { get; } = new();
        public List<Guid> Vorgaenge { get; } = new();
        public Func<VerkleinereBild, IEvent> Verkleinern { get; set; } = a => new BildVerkleinert(a.QuellPfad + "_klein.png", 683, a.Hoehe);
        private readonly ConcurrentQueue<Guid> _weckungen = new();
        private const string Name = nameof(Bildeingang);

        public Welt()
        {
            Ausfuehrer = new FunktionsAusfuehrer(Log,
                (auftrag, _) =>
                {
                    Gerufen.Enqueue(auftrag.GetType().Name);
                    return Task.FromResult(auftrag switch
                    {
                        DeuteDateiname d => ImagePairFileName.Resolve(d.Dateiname) is { } r
                            ? (IEvent)new ImagePairDateiGedeutet(r.AggregateId, r.PairKey, r.Version, r.ProduziertAm, d.Pfad)
                            : new DateinameUnbekannt(d.Dateiname),
                        VerkleinereBild v => Verkleinern(v),
                        GleicheHistogrammAus h => new HistogrammAusgeglichen(h.QuellPfad + "_ausgeglichen.png", 683, 512, Log.Jetzt),
                        _ => throw new NotSupportedException(auftrag.GetType().Name),
                    });
                },
                (k, _) => { _weckungen.Enqueue(k); return Task.CompletedTask; },
                _ => new FunktionsBindung(typeof(IFunktion), Slots: 4), wiederholPause: TimeSpan.FromMilliseconds(1));
            var registry = new Dictionary<string, ProzessRegeln> { [Name] = new Bildeingang().Fluss.Regeln };
            Manager = new ProzessManager(Log, registry, DispatchAsync,
                rufe: (k, a, v, akt, ct) => Ausfuehrer.BeauftrageAsync(k, a, v, akt, ct), jetzt: _ => Task.FromResult(Log.Jetzt));
            Eingang = new QuellEingang(Log, registry,
                async (korr, stream, version, name, ct) => { Vorgaenge.Add(korr); await Manager.StarteAsync(korr, name, stream, version, ct); },
                _ => "KameraSystem");
        }

        /// <summary>Das ImagePair: ErstelleImagePair nur beim ersten Mal (sonst Ablehnungs-Marke), MeldeBildVerfuegbar → BildVerfuegbar.</summary>
        private async Task DispatchAsync(Guid korrelation, ICommand cmd, Guid vorgang, CancellationToken ct)
        {
            lock (Gesendet) Gesendet.Add(cmd);
            var stream = Log.Stream(cmd.AggregateId);
            if (stream.Any(e => e.CausationId == vorgang.ToString())) return;   // Framework-Inbox
            IEvent[] evs = cmd switch
            {
                ErstelleImagePair c when stream.Any(e => e.Payload is ImagePairErstellt) =>
                    [new KommandoAbgelehnt(vorgang, nameof(ImagePairExistiertBereits))],
                ErstelleImagePair c => [new ImagePairErstellt(c.AggregateId, c.PairKey, c.ProduziertAm, c.AufgenommenAm, c.UrsprungsPfad), new KommandoVerarbeitet(vorgang)],
                MeldeBildVerfuegbar m => [new BildVerfuegbar(m.Version, m.Meta, m.Pfad, []), new KommandoVerarbeitet(vorgang)],
                _ => throw new NotSupportedException(cmd.GetType().Name),
            };
            await Log.AppendEventsAsync(cmd.AggregateId, stream.Count, evs, korrelation.ToString(), vorgang.ToString(), "ImagePair");
        }

        public async Task<ProzessManager.ManagerStatus> DateiAsync(string name, long groesse = 4711)
        {
            await Eingang.NimmAufAsync(new DateiErkannt("/in/" + name, name, groesse, Log.Jetzt), default);
            var korr = Vorgaenge[^1];
            for (var i = 0; i < 40; i++)
            {
                await Ausfuehrer.WarteAufAlleAsync();
                while (_weckungen.TryDequeue(out _)) { }
                var st = await Manager.LadeStatusAsync(korr);
                if (st.Beendet) return st;
                await Manager.WakeAsync(korr);
            }
            return await Manager.LadeStatusAsync(korr);
        }

        public IReadOnlyList<MeldeBildVerfuegbar> Meldungen { get { lock (Gesendet) return Gesendet.OfType<MeldeBildVerfuegbar>().Distinct().ToList(); } }
    }

    private const string Dc0 = "2025_06_16_17_46_20_293_DC0.tiff", Dc2 = "2025_06_16_17_46_20_293_DC2.tiff";
    private static readonly Guid Paar = ImagePairFileName.ToDeterministicGuid("2025_06_16_17_46_20_293");

    [Fact]
    public async Task Eine_Datei_wird_gedeutet_das_Paar_angelegt_aufbereitet_und_mit_Metadaten_verfuegbar_gemeldet()
    {
        var w = new Welt();
        var st = await w.DateiAsync(Dc0);

        st.Erfolg.Should().BeTrue();
        w.Gerufen.Should().Equal(nameof(DeuteDateiname), nameof(VerkleinereBild), nameof(GleicheHistogrammAus));
        w.Gesendet.OfType<ErstelleImagePair>().Should().ContainSingle().Which.UrsprungsPfad.Should().Be("/in/" + Dc0);
        w.Meldungen.Should().ContainSingle().Which.Should().BeEquivalentTo(new MeldeBildVerfuegbar(
            Paar, BildVersion.Dc0, new BildMeta(Dc0, 4711, 683, 512, w.Log.Jetzt), "/in/" + Dc0 + "_klein.png_ausgeglichen.png"));
    }

    [Fact]
    public async Task Die_zweite_Datei_eines_Paars_nimmt_den_Ablehnungs_Port_und_wird_ebenfalls_gemeldet()
    {
        var w = new Welt();
        (await w.DateiAsync(Dc0)).Erfolg.Should().BeTrue();
        var st = await w.DateiAsync(Dc2);

        st.Erfolg.Should().BeTrue("ErstelleImagePair ist abgelehnt (Paar existiert), der ✕-Port ist verdrahtet — kein Scheitern");
        w.Meldungen.Select(m => m.Version).Should().BeEquivalentTo(new[] { BildVersion.Dc0, BildVersion.Dc2 });
        w.Meldungen.Should().OnlyContain(m => m.AggregateId == Paar);
        st.Umgeleitet.Values.Should().ContainSingle(u => u.Art == "abgelehnt" && u.Grund == nameof(ImagePairExistiertBereits));
    }

    [Fact]
    public async Task Dieselbe_Datei_zweimal_gesehen_startet_genau_einen_Vorgang()
    {
        var w = new Welt();
        await w.DateiAsync(Dc0);
        await w.DateiAsync(Dc0);

        w.Vorgaenge.Distinct().Should().ContainSingle();
        w.Meldungen.Should().ContainSingle();
    }

    [Fact]
    public async Task Ein_unbekannter_Name_und_ein_unlesbares_Bild_enden_ohne_Meldung()
    {
        var w = new Welt();
        (await w.DateiAsync("notiz.txt")).Erfolg.Should().BeTrue();
        w.Gerufen.Should().Equal(nameof(DeuteDateiname));

        var kaputt = new Welt { Verkleinern = a => new BildNichtLesbar(a.QuellPfad, "kaputt") };
        (await kaputt.DateiAsync(Dc2)).Beendet.Should().BeTrue();
        kaputt.Gerufen.Should().NotContain(nameof(GleicheHistogrammAus));
        kaputt.Meldungen.Should().BeEmpty();
    }
}
