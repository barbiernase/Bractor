using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Bildvorschau.Katalog;
using FluentAssertions;
using Infrastructure.Akteure;
using Infrastructure.Eingaenge;
using Infrastructure.Funktionen;
using Infrastructure.Quellen;
using Infrastructure.Umgebung;
using Xunit;
using A = Infrastructure.Komposition.Anwendung;
using QuellEingaenge = Infrastructure.Quellen.Eingaenge;

namespace Infrastructure.Pruefstand.Quellen;

/// <summary>
/// Die ANNAHME (docs/04 §4.7): eine annehmende Quellart (<c>IAnnahme&lt;Aufruf&gt;</c>, der Webhook) lauscht nicht — jeder Host nimmt ihre Aufrufe an,
/// übersetzt sie mit der Übersetzung der Akteur-Seite und gibt sie über dieselbe <see cref="Hineingabe"/> hinein wie ein Läufer. Store-frei: Emitter,
/// offene Eingänge und Wege sind Nähte. Bewiesen: der Start prüft Pfad (Pflicht, eindeutig) und verlangt keinen Rechner; ein Aufruf ohne passenden Pfad,
/// mit falschem Schlüssel oder ohne Kennung kommt nicht hinein; 202 erst, wenn die Nachricht durabel ist; dieselbe Kennung zweimal ergibt dieselben Ids;
/// scheitert das Hineingeben oder ist die Grenze voll, heißt es „später“.
/// </summary>
public class AnnahmeTests
{
    private sealed class Ziel : IState { public int Version { get; set; } public Guid Id { get; set; } }
    private sealed record Buche(Id<Ziel> AggregateId, string Inhalt) : ICommand<Ziel>;

    private static readonly Id<Ziel> Konto = Id<Ziel>.Neu();
    private const string Client = "Lieferportal", Akteur = "Lieferant", Instanz = "bildaufbereitung";

    /// <summary>Der Webhook, wie der Generator ihn in die Tabelle der Bausteine schreibt (die Beispielanwendung kennt die Quellarten des Frameworks).</summary>
    private static FunktionsBaustein Webhook => A.Funktionen.Bausteine.Single(b => b.Name == "Webhook");

    private static readonly UebersetzungPlan Buchen = new("Aufruf_Buche_1", typeof(Aufruf), typeof(Buche), x => new Buche(Konto, ((Aufruf)x).Inhalt));
    private static readonly UebersetzungPlan Lesen = new("Aufruf_LiesBild_1", typeof(Aufruf), typeof(LiesBild),
        x => new LiesBild(((Aufruf)x).Kennung, ((Aufruf)x).Inhalt, 1, DateTimeOffset.UnixEpoch));

    private static AkteurRechte Lieferant => new(Akteur, typeof(object), new HashSet<Type> { typeof(Buche), typeof(LiesBild) }, new HashSet<Type>(),
        new HashSet<Type>(), new HashSet<Type>());

    private static EingangKonfig Haken(string? pfad = "lieferant", string? schluessel = null, string? rechner = null)
    {
        var e = new EingangKonfig { Art = "Webhook", Akteur = Akteur, Rechner = rechner };
        if (pfad != null) e.Werte[Annahmevertrag.Pfad] = pfad;
        if (schluessel != null) e.Werte[Annahmevertrag.Schluessel] = schluessel;
        return e;
    }

    private static QuellEingaenge Aus(bool einzelknoten, params (string Name, EingangKonfig E)[] eingaenge)
    {
        var client = new ClientKonfig();
        foreach (var (n, e) in eingaenge) client.Eingaenge[n] = e;
        return QuellEingaenge.Aus(new Dictionary<string, ClientKonfig> { [Client] = client }, A.Funktionen.Bausteine, new Uebersetzungen([Buchen]),
            new Dictionary<string, AkteurRechte> { [Akteur] = Lieferant },
            new Dictionary<string, ClientVertrag> { [Client] = new(Client, typeof(object), "h", [Akteur]) }, einzelknoten);
    }

    private static void Scheitert(Func<QuellEingaenge> aus, string muster, string karte = "eingang:Lieferportal/haken") =>
        FluentActions.Invoking(aus).Should().Throw<BootFehler>().WithMessage(muster).Which.Karte.Should().Be(karte);

    // ── Der Start (Eingaenge.Aus) ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Der_Webhook_ist_eine_Annahme_die_Aufrufe_meldet_und_nicht_lauscht()
    {
        Webhook.Annahme.Should().BeTrue();
        Webhook.Quellart.Should().BeTrue();
        Webhook.Lausche.Should().BeNull("eine Annahme lauscht nicht — ihre Meldungen nimmt der Host an");
        Webhook.Meldet.Should().Equal(typeof(Aufruf));
    }

    [Fact]
    public void Eine_Annahme_braucht_im_Cluster_keinen_Rechner_jeder_Host_nimmt_an()
    {
        var e = Aus(false, ("haken", Haken("/lieferant/", "geheim"))).Alle.Should().ContainSingle().Subject;
        e.Annahme.Should().BeTrue();
        e.Rechner.Should().BeNull();
        e.Pfad.Should().Be("lieferant", "ohne führende und folgende Schrägstriche");
        e.Schluessel.Should().Be("geheim");
        Aus(false, ("haken", Haken())).Lauschende.Should().BeEmpty("eine Annahme hat keinen Läufer");
        Aus(false, ("haken", Haken())).Annehmende.Should().ContainSingle();
        Scheitert(() => Aus(false, ("haken", Haken(rechner: "r1"))), "*nimmt an*ein Rechner hat hier keine Bedeutung*");
    }

    [Fact]
    public void Der_Pfad_ist_Pflicht_und_je_Umgebung_eindeutig()
    {
        Scheitert(() => Aus(true, ("haken", Haken(pfad: null))), "*der Wert „Pfad“ fehlt*");
        Scheitert(() => Aus(true, ("haken", Haken(pfad: " / "))), "*der Wert „Pfad“ fehlt*");
        Scheitert(() => Aus(true, ("haken", Haken(pfad: "lie fer"))), "*ist kein Pfad*");
        Scheitert(() => Aus(true, ("anderer", Haken("lieferant")), ("haken", Haken("/lieferant"))),
            "*Pfad „lieferant“ nimmt schon der Eingang Lieferportal/anderer an*");
        Aus(true, ("anderer", Haken("a")), ("haken", Haken("b"))).Annehmende.Select(e => e.Pfad).Should().Equal("a", "b");
    }

    // ── Die Annahme eines Aufrufs (Annahmen.NimmAsync) ───────────────────────────────────────────────────────────────────

    /// <summary>Eine Welt: der Emitter als Naht (wahlweise angehalten oder scheiternd), die offenen Eingänge und die Wege im Speicher.</summary>
    private sealed class Welt : IOffeneEingaenge, IFunktionsWege
    {
        public readonly ConcurrentQueue<(IAggregatCommand Befehl, Guid Id, Guid Korrelation, string? Akteur, Spur.Kopf? Spur)> Gesendet = new();
        public readonly ConcurrentQueue<EingangOffen> Gelegt = new();
        public readonly ConcurrentDictionary<Guid, bool> Fertig = new();
        public Func<CancellationToken, Task> Antworte = _ => Task.CompletedTask;

        public Annahmen Annahmen(EingangPlan plan) =>
            new([plan], p => new Hineingabe(p, SendeAsync, new Verkettung(new Dictionary<Type, string[]> { [typeof(LiesBild)] = [Instanz] }), this, this,
                versuche: 2));

        public static EingangPlan Plan(UebersetzungPlan u, string? schluessel = null, int offen = 0) =>
            new(Client, "haken", Webhook, Akteur, new Dictionary<Type, UebersetzungPlan[]> { [typeof(Aufruf)] = [u] }, new KnotenKonfig(), null, offen,
                "lieferant", schluessel);

        private async Task<Infrastructure.Emit.EmitAntwort> SendeAsync(IAggregatCommand c, Guid id, Guid korrelation, CancellationToken ct)
        {
            Gesendet.Enqueue((c, id, korrelation, ImAuftrag.Akteur, Spur.Aktuell));
            await Antworte(ct);
            return new Infrastructure.Emit.EmitAntwort(EmitAusgang.Zugestellt, Array.Empty<Antwortereignis>());
        }

        public Task<IReadOnlyList<OffenerEingang>> FaelligeAsync(int hoechstens, CancellationToken ct) => throw new NotSupportedException();
        public Task ErledigtAsync(IReadOnlyCollection<Guid> kennungen, CancellationToken ct) => throw new NotSupportedException();
        public Task VerschiebeAsync(IReadOnlyCollection<Guid> kennungen, TimeSpan nach, CancellationToken ct) => throw new NotSupportedException();
        public Task ParkeAsync(IReadOnlyCollection<(Guid Kennung, string Grund)> geparkt, CancellationToken ct) => throw new NotSupportedException();
        public Task LegeAsync(IReadOnlyList<EingangOffen> eingaenge, Guid stream, CancellationToken ct)
        {
            foreach (var e in eingaenge) Gelegt.Enqueue(e);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyCollection<Guid>> NochOffenAsync(IReadOnlyCollection<Guid> kennungen, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(kennungen.Where(k => !Fertig.ContainsKey(k)).ToList());

        public Task<EingangQuittung?> StelleZuAsync(EingangZustellen eingang, TimeSpan frist, CancellationToken ct) => throw new NotSupportedException();
        public void Melde(EingangZustellen eingang) { }
    }

    [Fact]
    public async Task Ein_Pfad_ohne_Eingang_ist_404_ein_falscher_Schluessel_401_eine_fehlende_Kennung_400()
    {
        var w = new Welt();
        var a = w.Annahmen(Welt.Plan(Buchen, schluessel: "geheim"));
        (await a.NimmAsync("anderer", "geheim", "k1", "x", default)).Status.Should().Be(404);
        (await a.NimmAsync("lieferant", "falsch", "k1", "x", default)).Status.Should().Be(401);
        (await a.NimmAsync("lieferant", null, "k1", "x", default)).Status.Should().Be(401, "der Eingang nennt einen Schlüssel — ohne kommt niemand hinein");
        (await a.NimmAsync("lieferant", "geheim", null, "x", default)).Status.Should().Be(400, "ohne Kennung gäbe es kein „genau einmal“");
        (await a.NimmAsync("lieferant", "geheim", " ", "x", default)).Status.Should().Be(400);
        w.Gesendet.Should().BeEmpty("nichts davon kommt hinein");
        (await a.NimmAsync("/lieferant/", "geheim", "k1", "x", default)).Status.Should().Be(202, "der Pfad wird wie beim Start verglichen");
        (await w.Annahmen(Welt.Plan(Buchen)).NimmAsync("lieferant", null, "k2", "x", default)).Status.Should().Be(202, "ohne Schlüssel am Eingang nimmt er jeden an");
    }

    [Fact]
    [Trait("Kategorie", "Garantie")]
    public async Task Dieselbe_Kennung_zweimal_ergibt_dieselben_Ids_im_Namen_des_Akteurs_mit_der_Spur_des_Eingangs()
    {
        var w = new Welt();
        var a = w.Annahmen(Welt.Plan(Buchen));
        var erste = await a.NimmAsync("lieferant", null, "auftrag-7", "{\"n\":1}", default);
        var zweite = await a.NimmAsync("lieferant", null, "auftrag-7", "{\"n\":1}", default);
        var andere = await a.NimmAsync("lieferant", null, "auftrag-8", "{\"n\":1}", default);

        erste.Status.Should().Be(202);
        erste.Kennung.Should().Be("auftrag-7", "die Antwort nennt die Kennung des Absenders");
        var element = Kettenvertrag.Element(Client + "/haken", new Aufruf("auftrag-7", "{\"n\":1}"));
        erste.Element.Should().Be(element).And.Be(zweite.Element, "dieselbe Kennung ist dasselbe Element");
        andere.Element.Should().NotBe(element);
        var ids = w.Gesendet.Select(x => x.Id).ToList();
        ids[0].Should().Be(ids[1], "eine Wiederholung trägt dieselbe Command-Id — die Inbox des Aggregats macht daraus eine Wirkung");
        ids[0].Should().Be(Kettenvertrag.Befehl(element, Client + "/haken", 0, w.Gesendet.First().Befehl));
        ids[2].Should().NotBe(ids[0]);
        var (_, _, korrelation, akteur, spur) = w.Gesendet.First();
        korrelation.Should().Be(element);
        akteur.Should().Be(Akteur, "der Host gibt im Namen des Akteurs des Eingangs hinein");
        spur!.Wirker.Should().Be(WirkerId.FuerEingang(Client, "haken"), "der Wirker ist der Eingang (W9)");
    }

    [Fact]
    [Trait("Kategorie", "Garantie")]
    public async Task Ein_Command_ohne_Ziel_wird_ein_offener_Eingang_dieselbe_Kennung_legt_dieselbe_Zeile()
    {
        var w = new Welt();
        var a = w.Annahmen(Welt.Plan(Lesen));
        (await a.NimmAsync("lieferant", null, "bild-1", "/bilder/a.tiff", default)).Status.Should().Be(202);
        (await a.NimmAsync("lieferant", null, "bild-1", "/bilder/a.tiff", default)).Status.Should().Be(202);
        w.Gelegt.Select(e => e.Kennung).Distinct().Should().ContainSingle("dieselbe Kennung ergibt dieselbe Zeile — sie wird nicht doppelt gelegt");
        w.Gelegt.First().Instanz.Should().Be(Instanz);
        w.Gelegt.First().Akteur.Should().Be(Akteur);
    }

    [Fact]
    public async Task Angenommen_heisst_es_erst_wenn_die_Nachricht_durabel_ist()
    {
        var w = new Welt();
        var frei = new TaskCompletionSource();
        w.Antworte = ct => frei.Task.WaitAsync(ct);
        var a = w.Annahmen(Welt.Plan(Buchen));
        var laeuft = a.NimmAsync("lieferant", null, "k1", "x", default);
        for (var i = 0; i < 100 && w.Gesendet.IsEmpty; i++) await Task.Delay(10);
        w.Gesendet.Should().ContainSingle("der Command ist unterwegs");
        await Task.Delay(100);
        laeuft.IsCompleted.Should().BeFalse("ohne Antwort des Aggregats ist nichts durabel — keine 202");
        frei.SetResult();
        (await laeuft).Status.Should().Be(202);
    }

    [Fact]
    public async Task Scheitert_das_Hineingeben_heisst_es_spaeter_und_eine_volle_Grenze_auch()
    {
        var w = new Welt { Antworte = _ => throw new InvalidOperationException("Datenbank weg") };
        var a = w.Annahmen(Welt.Plan(Buchen));
        var r = await a.NimmAsync("lieferant", null, "k1", "x", default);
        r.Status.Should().Be(503, "der Absender wiederholt mit derselben Kennung");
        r.Fehler.Should().Contain("Datenbank weg").And.Contain("später");

        var v = new Welt();
        var voll = v.Annahmen(Welt.Plan(Lesen, offen: 1));
        (await voll.NimmAsync("lieferant", null, "b1", "/a", default)).Status.Should().Be(202);
        (await voll.NimmAsync("lieferant", null, "b2", "/b", default)).Status.Should().Be(503, "ein Element ist offen, die Grenze ist 1 — je Host gezählt");
        v.Fertig[v.Gelegt.Single().Kennung] = true;
        (await voll.NimmAsync("lieferant", null, "b2", "/b", default)).Status.Should().Be(202, "ist das offene fertig, ist wieder Platz");
    }

    [Fact]
    public void Der_Schluessel_wird_zeitkonstant_verglichen()
    {
        Annahmen.Gleich("geheim", "geheim").Should().BeTrue();
        Annahmen.Gleich("geheim", "geheiM").Should().BeFalse();
        Annahmen.Gleich("geheim", "geheim-länger").Should().BeFalse();
        Annahmen.Gleich("geheim", null).Should().BeFalse();
    }
}
