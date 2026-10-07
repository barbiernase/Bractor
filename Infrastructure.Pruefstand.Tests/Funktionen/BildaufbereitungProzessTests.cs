using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using Domain.Bildaufbereitung;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Aggregate;     // KommandoVerarbeitet
using Infrastructure.Funktionen;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Ebene 1 — der ECHTE <see cref="BildaufbereitungProzess"/> der Domäne (nicht die Test-Welt): Rohbild → ƒ Verkleinern →
/// ƒ Histogramm → MeldeBildVerfuegbar, mit echtem Prozess-Manager und Funktions-Ausführer; die Funktionen sind Fakes
/// (OpenCV läuft live im Host). Bewiesen: die Kette läuft in Reihenfolge, die Paar-Id und die Datei-Metadaten kommen über den
/// Join mit dem Auslöser an, und eine unlesbare Datei beendet den Prozess ohne Meldung.
/// </summary>
public class BildaufbereitungProzessTests
{
    private sealed class Welt
    {
        public LogImSpeicher Log { get; } = new();
        public ProzessManager Manager { get; }
        public FunktionsAusfuehrer Ausfuehrer { get; }
        public List<ICommand> Gesendet { get; } = new();
        public List<string> Reihenfolge { get; } = new();
        public Func<VerkleinereBild, OneOf<BildVerkleinert, BildNichtLesbar>> Verkleinern { get; set; } =
            a => new BildVerkleinert(a.QuellPfad + "_klein.png", 683, a.Hoehe);
        private readonly ConcurrentQueue<Guid> _weckungen = new();
        public readonly Guid Korrelation = Guid.NewGuid();
        public readonly Guid Paar = Guid.NewGuid();
        private const string Name = nameof(BildaufbereitungProzess);

        public Welt()
        {
            var bindung = new FunktionsBindung(typeof(IBildVerkleinerung), Slots: 1, Wiederholungen: 0);
            Ausfuehrer = new FunktionsAusfuehrer(Log,
                (auftrag, _) =>
                {
                    lock (Reihenfolge) Reihenfolge.Add(auftrag.GetType().Name);
                    return Task.FromResult(auftrag switch
                    {
                        VerkleinereBild v => (IEvent)Verkleinern(v).Value,
                        GleicheHistogrammAus h => new HistogrammAusgeglichen(h.QuellPfad + "_ausgeglichen.png", 683, 512, Log.Jetzt),
                        _ => throw new NotSupportedException(auftrag.GetType().Name),
                    });
                },
                (k, _) => { _weckungen.Enqueue(k); return Task.CompletedTask; },
                _ => bindung, wiederholPause: TimeSpan.FromMilliseconds(1));
            Manager = new ProzessManager(Log,
                new Dictionary<string, ProzessRegeln> { [Name] = new BildaufbereitungProzess().Regeln },
                DispatchAsync, rufe: (k, a, v, akt, ct) => Ausfuehrer.BeauftrageAsync(k, a, v, akt, ct),
                jetzt: _ => Task.FromResult(Log.Jetzt));
        }

        private async Task DispatchAsync(Guid korrelation, ICommand cmd, Guid vorgang, System.Threading.CancellationToken ct)
        {
            Gesendet.Add(cmd);
            var stream = Log.Stream(cmd.AggregateId);
            if (stream.Any(e => e.CausationId == vorgang.ToString())) return;
            var m = (MeldeBildVerfuegbar)cmd;
            await Log.AppendEventsAsync(cmd.AggregateId, stream.Count,
                new IEvent[] { new BildVerfuegbar(m.Version, m.Meta, m.Pfad, []), new KommandoVerarbeitet(vorgang) },
                korrelation.ToString(), vorgang.ToString(), "ImagePair");
        }

        public async Task<ProzessManager.ManagerStatus> LaufeAsync(RohbildEingegangen roh)
        {
            await Log.AppendEventsAsync(Paar, 0, new IEvent[] { roh }, Korrelation.ToString(), null, "ImagePair");
            await Manager.StarteAsync(Korrelation, Name, Paar, 1);
            for (var i = 0; i < 30; i++)
            {
                await Ausfuehrer.WarteAufAlleAsync();
                while (_weckungen.TryDequeue(out _)) { }
                var st = await Manager.LadeStatusAsync(Korrelation);
                if (st.Beendet) return st;
                await Manager.WakeAsync(Korrelation);
            }
            return await Manager.LadeStatusAsync(Korrelation);
        }
    }

    [Fact]
    public async Task Rohbild_laeuft_durch_Verkleinern_und_Histogramm_und_wird_mit_Paar_Id_und_Metadaten_verfuegbar_gemeldet()
    {
        var w = new Welt();
        var st = await w.LaufeAsync(new RohbildEingegangen(w.Paar, BildVersion.Dc2, "/in/a.tiff", "a.tiff", 4711));

        st.Beendet.Should().BeTrue();
        st.Erfolg.Should().BeTrue();
        w.Reihenfolge.Should().Equal(nameof(VerkleinereBild), nameof(GleicheHistogrammAus));
        w.Gesendet.Should().ContainSingle().Which.Should().BeEquivalentTo(new MeldeBildVerfuegbar(
            w.Paar, BildVersion.Dc2,
            new BildMeta("a.tiff", 4711, 683, 512, w.Log.Jetzt),
            "/in/a.tiff_klein.png_ausgeglichen.png"));
    }

    [Fact]
    public async Task Eine_unlesbare_Datei_beendet_den_Prozess_ohne_Meldung_und_ohne_zweite_Funktion()
    {
        var w = new Welt { Verkleinern = a => new BildNichtLesbar(a.QuellPfad, "kaputt") };
        var st = await w.LaufeAsync(new RohbildEingegangen(w.Paar, BildVersion.Dc0, "/in/b.tiff", "b.tiff", 1));

        st.Beendet.Should().BeTrue();
        w.Reihenfolge.Should().Equal(nameof(VerkleinereBild));
        w.Gesendet.Should().BeEmpty();
    }
}
