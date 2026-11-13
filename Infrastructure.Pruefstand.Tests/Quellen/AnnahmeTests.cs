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
using Client.Infrastructure.Quellen;
using Infrastructure.Umgebung;
using Xunit;
using A = Infrastructure.Komposition.Anwendung;
using QuellEingaenge = Infrastructure.Quellen.Eingaenge;

namespace Infrastructure.Pruefstand.Quellen;

/// <summary>
/// Die ANNAHME (docs/04 §4.7, docs/10 E18): eine annehmende Quellart (<c>IAnnahme&lt;Aufruf&gt;</c>, der Webhook) lauscht nicht — der
/// Rechnerdienst ihrer App-Instanz nimmt ihre Aufrufe auf dem Port der Instanz an, übersetzt sie und gibt sie über dieselbe <see cref="Hineingabe"/> über die
/// Leitung hinein wie ein Läufer. Bewiesen: der Start prüft Pfad (Pflicht, eindeutig) und Port; ein Aufruf ohne passenden Pfad, auf fremdem Port, mit
/// falschem Schlüssel oder ohne Kennung kommt nicht hinein; 202 erst nach der Quittung; dieselbe Kennung zweimal ergibt dieselben Ids; ohne Verbindung oder
/// wenn das Hineingeben scheitert, heißt es „später“.
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

    /// <summary>Eine App des Lieferportals mit dem Webhook als Quelle <c>Haken</c> (wie der Generator sie in die Tabelle der Apps schreibt).</summary>
    private static ClientVertrag LieferApp => new("LieferApp", typeof(object), "h", [Akteur])
        { Client = Client, Quellen = [new AppQuelle("Haken", "Webhook", ["Aufruf_Buche_1"])] };

    private static AppInstanzKonfig Haken(string? pfad = "lieferant", string? schluessel = null, string rechner = "r1", int port = 5300)
    {
        var q = new QuelleKonfig();
        if (pfad != null) q.Werte[Annahmevertrag.Pfad] = pfad;
        if (schluessel != null) q.Werte[Annahmevertrag.Schluessel] = schluessel;
        return new AppInstanzKonfig { App = "LieferApp", Rechner = rechner, Port = port, Quellen = { ["Haken"] = q } };
    }

    private static QuellEingaenge Aus(params (string Name, AppInstanzKonfig A)[] instanzen) =>
        QuellEingaenge.Aus(instanzen.ToDictionary(x => x.Name, x => x.A), new Dictionary<string, ClientVertrag> { ["LieferApp"] = LieferApp },
            A.Funktionen.Bausteine, new Uebersetzungen([Buchen]), new Dictionary<string, AkteurRechte> { [Akteur] = Lieferant }, null);

    private static void Scheitert(Func<QuellEingaenge> aus, string muster, string karte = "eingang:haken/Haken") =>
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
    public void Eine_Annahme_nimmt_der_Rechnerdienst_auf_dem_Port_der_Instanz_an_sie_hat_keinen_Laeufer()
    {
        var e = Aus(("haken", Haken("/lieferant/", "geheim"))).Alle.Should().ContainSingle().Subject;
        e.Annahme.Should().BeTrue();
        e.Rechner.Should().Be("r1", "der Rechner der Instanz");
        e.Port.Should().Be(5300, "auf dem Port der Instanz nimmt ihr Rechnerdienst an");
        Scheitert(() => Aus(("haken", Haken(port: 0))), "*braucht einen Port*", "appinstanz:haken");
        e.Akteur.Should().Be(Akteur);
        e.Pfad.Should().Be("lieferant", "ohne führende und folgende Schrägstriche");
        e.Schluessel.Should().Be("geheim");
        Aus(("haken", Haken())).Lauschende.Should().BeEmpty("eine Annahme hat keinen Läufer");
        Aus(("haken", Haken())).Annehmende.Should().ContainSingle();
    }

    [Fact]
    public void Der_Pfad_ist_Pflicht_und_je_Umgebung_eindeutig()
    {
        Scheitert(() => Aus(("haken", Haken(pfad: null))), "*der Wert „Pfad“ fehlt*");
        Scheitert(() => Aus(("haken", Haken(pfad: " / "))), "*der Wert „Pfad“ fehlt*");
        Scheitert(() => Aus(("haken", Haken(pfad: "lie fer"))), "*ist kein Pfad*");
        Scheitert(() => Aus(("anderer", Haken("lieferant")), ("haken", Haken("/lieferant"))), "*Pfad „lieferant“ nimmt schon anderer/Haken an*");
        Aus(("anderer", Haken("a")), ("haken", Haken("b"))).Annehmende.Select(e => e.Pfad).Should().Equal("a", "b");
    }

    // ── Die Annahme eines Aufrufs (Annahmen.NimmAsync, im Rechnerdienst) ──────────────────────────────────────────────────

    private static EingangPlan Plan(UebersetzungPlan u, string? schluessel = null) =>
        new("haken", "Haken", Webhook, Akteur, new Dictionary<Type, UebersetzungPlan[]> { [typeof(Aufruf)] = [u] }, new KnotenKonfig(), "r1", "", "lieferant",
            schluessel, 5300);

    /// <summary>Die Annahme mit einer verbundenen Hineingabe über die Leitung im Speicher.</summary>
    private static (Annahmen Annahmen, Leitungsprobe Leitung, Abgabe Abgabe) Annahme(UebersetzungPlan u, string? schluessel = null)
    {
        var l = new Leitungsprobe();
        var abgabe = new Abgabe(l, TimeSpan.FromMilliseconds(200));
        var a = new Annahmen();
        a.Nimm(Plan(u, schluessel));
        a.Setze("haken", p => new Hineingabe(p, abgabe));
        return (a, l, abgabe);
    }

    [Fact]
    public async Task Ein_Pfad_ohne_Quelle_ist_404_ein_falscher_Schluessel_401_eine_fehlende_Kennung_400()
    {
        var (a, l, abgabe) = Annahme(Buchen, schluessel: "geheim");
        await using var _ = abgabe;
        (await a.NimmAsync("anderer", 5300, "geheim", "k1", "x", default)).Status.Should().Be(404);
        (await a.NimmAsync("lieferant", 5301, "geheim", "k1", "x", default)).Status.Should().Be(404, "auf einem anderen Port nimmt diese Quelle nicht an");
        (await a.NimmAsync("lieferant", 5300, "falsch", "k1", "x", default)).Status.Should().Be(401);
        (await a.NimmAsync("lieferant", 5300, null, "k1", "x", default)).Status.Should().Be(401, "die Quelle nennt einen Schlüssel — ohne kommt niemand hinein");
        (await a.NimmAsync("lieferant", 5300, "geheim", null, "x", default)).Status.Should().Be(400, "ohne Kennung gäbe es kein „genau einmal“");
        (await a.NimmAsync("lieferant", 5300, "geheim", " ", "x", default)).Status.Should().Be(400);
        l.Abgegeben.Should().BeEmpty("nichts davon kommt hinein");
        (await a.NimmAsync("/lieferant/", 5300, "geheim", "k1", "x", default)).Status.Should().Be(202, "der Pfad wird wie beim Start verglichen");
    }

    [Fact]
    [Trait("Kategorie", "Garantie")]
    public async Task Dieselbe_Kennung_zweimal_ergibt_dieselben_Ids_mit_dem_Element_als_Ursache()
    {
        var (a, l, abgabe) = Annahme(Buchen);
        await using var _ = abgabe;
        var erste = await a.NimmAsync("lieferant", 5300, null, "auftrag-7", "{\"n\":1}", default);
        var zweite = await a.NimmAsync("lieferant", 5300, null, "auftrag-7", "{\"n\":1}", default);
        var andere = await a.NimmAsync("lieferant", 5300, null, "auftrag-8", "{\"n\":1}", default);

        erste.Status.Should().Be(202);
        erste.Kennung.Should().Be("auftrag-7", "die Antwort nennt die Kennung des Absenders");
        var element = Kettenvertrag.Element("haken/Haken", new Aufruf("auftrag-7", "{\"n\":1}"));
        erste.Element.Should().Be(element).And.Be(zweite.Element, "dieselbe Kennung ist dasselbe Element");
        andere.Element.Should().NotBe(element);
        var ids = l.Abgegeben.Select(x => x.Id).ToList();
        ids[0].Should().Be(ids[1], "eine Wiederholung trägt dieselbe Command-Id — die Inbox des Aggregats macht daraus eine Wirkung");
        ids[0].Should().Be(Kettenvertrag.Befehl(element, "haken/Haken", 0, (IAggregatCommand)l.Abgegeben.First().Command));
        ids[2].Should().NotBe(ids[0]);
        l.Abgegeben.First().Ursache.Should().Be(Spur.Ursache(element, 0));
    }

    [Fact]
    public async Task Angenommen_heisst_es_erst_nach_der_Quittung()
    {
        var (a, l, abgabe) = Annahme(Buchen);
        await using var _ = abgabe;
        var frei = new TaskCompletionSource();
        l.Antworte = _ => frei.Task;
        var laeuft = a.NimmAsync("lieferant", 5300, null, "k1", "x", default);
        for (var i = 0; i < 100 && l.Abgegeben.IsEmpty; i++) await Task.Delay(10);
        l.Abgegeben.Should().ContainSingle("der Command ist unterwegs");
        await Task.Delay(100);
        laeuft.IsCompleted.Should().BeFalse("ohne Quittung ist nichts durabel — keine 202");
        frei.SetResult();
        (await laeuft).Status.Should().Be(202);
    }

    [Fact]
    public async Task Ohne_Verbindung_oder_wenn_das_Hineingeben_scheitert_heisst_es_spaeter()
    {
        var (a, l, abgabe) = Annahme(Buchen);
        await using var _ = abgabe;
        l.Schweigt = int.MaxValue;
        var r = await a.NimmAsync("lieferant", 5300, null, "k1", "x", default);
        r.Status.Should().Be(503, "der Absender wiederholt mit derselben Kennung");
        r.Fehler.Should().Contain("später");
        a.Setze("haken", null);
        (await a.NimmAsync("lieferant", 5300, null, "k1", "x", default)).Fehler.Should().Contain("keine Verbindung");
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
