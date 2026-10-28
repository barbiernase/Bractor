using Abstractions;
using Client.Infrastructure.Connection;
using Domain.Datensatz;
using Microsoft.Extensions.DependencyInjection;
using Domain.ImagePair;
using Domain.Projections.Datensatz;

namespace Verhalten.Tests;

/// <summary>Ein echter Host der Beispieldomäne für alle Client-Tests einer Klasse.</summary>
public sealed class BeispielHost : IAsyncLifetime
{
    public Testumgebung Umgebung { get; private set; } = null!;
    public Hostlauf Host { get; private set; } = null!;
    public JsonEingang Eingang { get; private set; } = null!;
    public Log Log { get; private set; } = null!;
    public Bildpaare Bilder { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Umgebung = await Testumgebung.NeuAsync(Anwendung.Beispiel, "verhalten-client");
        Host = await Hostlauf.StarteAsync(Umgebung);
        Eingang = new JsonEingang(Host);
        Log = new Log(Umgebung);
        Bilder = new Bildpaare(Umgebung);
    }

    /// <summary>Ein Bildpaar über das Quell-Verzeichnis, fertig, wenn es im Log komplett ist.</summary>
    public async Task<Bildpaare.Paar> KomplettesPaarAsync()
    {
        var paar = await Bilder.LegeAbAsync();
        await Warte.BisAsync(async () => await Log.ZahlAsync<ImagePairKomplett>(paar.Id) == 1, TimeSpan.FromSeconds(90),
            $"Paar {paar.Schluessel} wird nicht komplett", Host);
        return paar;
    }

    public async Task DisposeAsync()
    {
        Eingang.Dispose();
        await Host.DisposeAsync();
    }
}

/// <summary>
/// Das Verhalten an der Leitung, wie es ein Client erlebt (Plan §6, Phase 0) — gefahren mit dem echten Client-Stack des Blazor-Arbeitsplatzes
/// bzw. der rohen Leitung gegen einen echten Host. Muss den Umbau von Broker, Sitzung und Verteiler (Phasen 5–7) unverändert überstehen.
/// </summary>
[Trait("Kategorie", "Garantie")]
public sealed class ClientVerhaltenTests(BeispielHost b) : IClassFixture<BeispielHost>
{
    private static readonly TimeSpan Kurz = TimeSpan.FromSeconds(15);
    private static readonly HashSet<string> Gehoert =
        GeneratedWiring_Domain_Client_Modules_Blazor.ServerEventTypes.Select(t => t.Name.ToLowerInvariant()).ToHashSet();

    [Fact]
    public async Task Ein_Client_sieht_jedes_gehoerte_Event_seines_Aggregats_und_kennt_den_Kopf_des_Streams()
    {
        await using var ap = await Arbeitsplatz.VerbindeAsync(b.Host, Testumgebung.TokenArbeitsplatz);
        var paar = await b.KomplettesPaarAsync();

        // Jedes Event des Streams, das der Vertrag hört, kommt genau einmal an — mit der Version, die es im Log hat.
        var stream = await b.Log.StreamAsync(paar.Id);
        var soll = stream.Where(e => Gehoert.Contains(e.Typ)).Select(e => (e.Version, e.Typ)).ToList();
        soll.Should().HaveCountGreaterThanOrEqualTo(4, "ImagePairErstellt, zweimal BildVerfuegbar, ImagePairKomplett");
        await ap.WarteAufAsync(e => e.Kontext.AggregateId == paar.Id && e.Nachricht is ImagePairKomplett, Kurz, "ImagePairKomplett kam nicht an");
        var ist = ap.Alles.Where(e => e.Kontext.AggregateId == paar.Id && e.Nachricht is IEvent and not CommandFailed)
            .Select(e => (e.Kontext.AggregateVersion, e.Nachricht.GetType().Name.ToLowerInvariant())).OrderBy(x => x.AggregateVersion).ToList();
        ist.Should().Equal(soll, "der Client sieht jedes gehörte Event seines Aggregats genau einmal, mit seiner Version");

        // Der Client kennt den Kopf des Streams — und der Server nimmt einen Command mit dieser Version an.
        ap.Versionen.GetVersion(paar.Id).Should().Be(stream.Max(e => e.Version), "die Version aus den Pushes ist der Kopf des Streams");
        var quittung = await ap.QuittungAsync(ap.Sende(new LabelBildPaar(new(paar.Id), Klassifikation.Anomalie)), Kurz);
        quittung.Nachricht.Should().BeOfType<BildPaarGelabelt>("mit der Version aus den Pushes entscheidet das Aggregat, statt einen Konflikt zu melden");
        (await b.Log.ZahlAsync<BildPaarGelabelt>(paar.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Die_Quittung_eines_Commands_erreicht_den_Client_ueber_die_Korrelation_bevor_er_wiederholt()
    {
        await using var ap = await Arbeitsplatz.VerbindeAsync(b.Host, Testumgebung.TokenArbeitsplatz);
        var datensatz = Guid.NewGuid();
        var gesendet = DateTimeOffset.UtcNow;
        var quittung = await ap.QuittungAsync(ap.Sende(new ErstelleDatensatz(new(datensatz), "Quittung")), Kurz);

        quittung.Nachricht.Should().BeOfType<DatensatzErstellt>();
        (quittung.Zeit - gesendet).Should().BeLessThan(TimeSpan.FromSeconds(4), "binnen der Frist des Clients (5 s), sonst schickt er noch einmal");
        await Task.Delay(TimeSpan.FromSeconds(6));
        ap.Alles.Where(e => e.Nachricht is CommandUnbestaetigt or CommandSendFailed).Should().BeEmpty("der Ausgang ist bekannt");
        (await b.Log.ZahlAsync<DatensatzErstellt>(datensatz)).Should().Be(1);
    }

    /// <summary>
    /// Auch eine fachliche Ablehnung ist ein bekannter Ausgang: der Client muss sie über die Korrelation bekommen (docs/08 §8.4). Heute kommt
    /// sie nie an — der Broker stellt die gezielte Ablehnung nur zu, wenn die Sitzung ihren Typ abonniert hat, und der Vertrag des Arbeitsplatzes
    /// nennt keine Ablehnungen; der Client wiederholt dreimal und meldet „unbestätigt“.
    /// </summary>
    [Fact(Skip = "G-12 offen (docs/13)")]
    public async Task Eine_fachliche_Ablehnung_quittiert_den_Command_beim_Client()
    {
        await using var ap = await Arbeitsplatz.VerbindeAsync(b.Host, Testumgebung.TokenArbeitsplatz);
        var datensatz = Guid.NewGuid();
        await ap.QuittungAsync(ap.Sende(new ErstelleDatensatz(new(datensatz), "leer")), Kurz);

        var quittung = await ap.QuittungAsync(ap.Sende(new FriereEin(new(datensatz))), TimeSpan.FromSeconds(4));   // leer → abgelehnt

        quittung.Nachricht.Should().BeOfType<DatensatzLeer>();
        await Task.Delay(TimeSpan.FromSeconds(12));
        ap.Alles.Where(e => e.Nachricht is CommandUnbestaetigt).Should().BeEmpty();
    }

    [Fact]
    public async Task Ein_Client_darf_und_hoert_nur_was_Vertrag_und_Token_gemeinsam_erlauben()
    {
        var paar = await b.KomplettesPaarAsync();
        // Das Token verkörpert vom Arbeitsplatz nur den Inspekteur.
        await using var ap = await Arbeitsplatz.VerbindeAsync(b.Host, Testumgebung.TokenInspekteur);
        await using var roh = await RoheLeitung.VerbindeAsync(b.Host, "Arbeitsplatz", Testumgebung.TokenInspekteur);

        (await ap.QuittungAsync(ap.Sende(new LabelBildPaar(new(paar.Id), Klassifikation.KeineAnomalie)), Kurz))
            .Nachricht.Should().BeOfType<BildPaarGelabelt>("Labeln darf der Inspekteur");

        var datensatz = Guid.NewGuid();
        (await ap.QuittungAsync(ap.Sende(new ErstelleDatensatz(new(datensatz), "verboten")), Kurz))
            .Nachricht.Should().BeOfType<CommandFailed>("Datensätze legt der KI-Operator an — der steckt im Vertrag, aber nicht im Token");
        (await ap.FrageAsync(new HoleDatensaetze(), Kurz, typeof(DatensatzListe)))
            .Should().BeOfType<QueryFailed>("auch fragen darf er nur, was der Inspekteur darf");

        // Hören: Events außerhalb des Vertrags kommen nie an, Events im Vertrag schon (Gegenprobe am Paar).
        var sammelvorgang = Guid.NewGuid();
        await b.Eingang.MussAsync("StarteSammelvorgang", new() { ["AggregateId"] = sammelvorgang.ToString(), ["Anzahl"] = 1 }, Testumgebung.TokenDisponent);
        var zweites = await b.KomplettesPaarAsync();
        await roh.WarteAufEventAsync(e => e.AggregateId == zweites.Id.ToString() && e.Payload.Typ == nameof(ImagePairKomplett), Kurz,
            "das gehörte ImagePairKomplett kam nicht an");
        roh.Events.Should().NotContain(e => e.AggregateId == sammelvorgang.ToString(), "SammelvorgangGestartet steht nicht im Vertrag des Arbeitsplatzes");
        (await b.Log.ZahlAsync<DatensatzErstellt>(datensatz)).Should().Be(0);

        // Ein Token, das keinen Akteur des Clients verkörpert, kommt gar nicht erst an die Leitung.
        var verbinden = () => RoheLeitung.VerbindeAsync(b.Host, "Arbeitsplatz", Testumgebung.TokenDisponent);
        await verbinden.Should().ThrowAsync<Exception>();
    }

    /// <summary>
    /// Ein Client, der eine Funktion trägt (IKlassifikationsWorker: ITraegt&lt;IBildpaarBeurteilung&gt;), beantwortet ihren Eingang — die Antwort
    /// wirkt genau einmal: noch einmal geschickt, nach dem Neuverbinden oder von einem zweiten Träger, der den Eingang nicht hält, ändert sie nichts.
    /// (Phase 4: früher hörte der Client das Event und schickte eine Zusage mit Ursache; jetzt reicht der Funktions-Actor den Eingang über die
    /// Vermittlung mit Lease weiter, hält die erste Antwort fest und sendet sie selbst. Das fachliche Soll — genau eine Klassifikation je Paar —
    /// ist dasselbe.)
    /// </summary>
    [Fact]
    public async Task Die_Antwort_eines_Traegers_wirkt_genau_einmal_auch_doppelt_von_zwei_Traegern_und_nach_dem_Neuverbinden()
    {
        var eins = await RoheLeitung.VerbindeAsync(b.Host, "KlassifikationsWorker", Testumgebung.TokenKlassifizierer);
        await using var zwei = await RoheLeitung.VerbindeAsync(b.Host, "KlassifikationsWorker", Testumgebung.TokenKlassifizierer);
        // Viele Slots: Paare früherer Tests dieser Klasse warten (ohne Träger) beim Vermittler und werden zuerst vergeben.
        await eins.BieteAnAsync("IBildpaarBeurteilung", slots: 100);
        await zwei.BieteAnAsync("IBildpaarBeurteilung", slots: 100);
        var paar = await b.KomplettesPaarAsync();
        bool Fuer(Cqrs.Leitung.ArbeitsAuftrag a) => a.Stream == paar.Id.ToString() && a.Eingang.Typ == nameof(ImagePairKomplett);
        await Warte.BisAsync(() => Task.FromResult(eins.Eingaenge.Any(Fuer) || zwei.Eingaenge.Any(Fuer)), Kurz, "kein Träger bekam das komplette Paar", b.Host);
        var (traeger, anderer) = eins.Eingaenge.Any(Fuer) ? (eins, zwei) : (zwei, eins);
        var eingang = traeger.Eingaenge.First(Fuer);
        RoheLeitung.Eingang(eingang).Should().BeOfType<ImagePairKomplett>();

        var antwort = new KlassifiziereBildPaarDurchKi(new(paar.Id), Klassifikation.Anomalie);
        await traeger.AntworteAsync(eingang, antwort);
        await traeger.AntworteAsync(eingang, antwort);                                                      // noch einmal geschickt
        await anderer.AntworteAsync(eingang, new KlassifiziereBildPaarDurchKi(new(paar.Id), Klassifikation.KeineAnomalie));   // ohne Lease
        // Erst trennen, wenn der Server die Antworten gelesen hat (die doppelte quittiert er mit ARBEIT_UNBEKANNT): die Leitung schließt hart
        //   (Reset), ein noch nicht gesendetes Stück ginge sonst verloren — und dann hielte der getrennte Träger den Eingang bis zum Ablauf der Lease.
        await Warte.BisAsync(() => Task.FromResult(traeger.Fehler.Contains("ARBEIT_UNBEKANNT")), Kurz, "der Server hat die Antworten des Trägers nicht gelesen", b.Host);
        if (ReferenceEquals(traeger, eins))
        {
            await eins.DisposeAsync();
            await using var wieder = await RoheLeitung.VerbindeAsync(b.Host, "KlassifikationsWorker", Testumgebung.TokenKlassifizierer);
            await wieder.AntworteAsync(eingang, antwort);                                                   // nach dem Neuverbinden
            await Warte.NachlaufAsync();
        }
        else await Warte.NachlaufAsync();

        await Warte.BisAsync(async () => await b.Log.ZahlAsync<BildPaarDurchKiKlassifiziert>(paar.Id) >= 1, Kurz, "die Antwort wirkte nicht", b.Host);
        await Warte.NachlaufAsync();
        (await b.Log.ZahlAsync<BildPaarDurchKiKlassifiziert>(paar.Id)).Should().Be(1, "eine Antwort auf einen Eingang wirkt genau einmal");
        var daten = await b.Log.DatenAsync<BildPaarDurchKiKlassifiziert>(paar.Id);
        daten.Single().Should().Contain(((int)Klassifikation.Anomalie).ToString(), "es wirkt die Antwort des Trägers, der den Eingang hielt");
        anderer.Fehler.Should().Contain("ARBEIT_UNBEKANNT", "wer den Eingang nicht hält, kann ihn nicht beantworten");
        if (ReferenceEquals(traeger, zwei)) await eins.DisposeAsync();
    }

    [Fact]
    public async Task Nach_dem_eigenen_Command_sieht_die_naechste_Query_des_Clients_die_Aenderung()
    {
        await using var ap = await Arbeitsplatz.VerbindeAsync(b.Host, Testumgebung.TokenArbeitsplatz);
        var datensatz = Guid.NewGuid();
        await ap.QuittungAsync(ap.Sende(new ErstelleDatensatz(new(datensatz), "Lesen nach Schreiben")), Kurz);
        // Das Anlegen prüft der nächste Test (G-14); hier erst warten, bis die Leseseite den Datensatz kennt.
        await Warte.BisAsync(async () => await ap.FrageAsync(new HoleDatensatz(new(datensatz)), Kurz, typeof(DatensatzAntwort), typeof(DatensatzNichtGefunden))
            is DatensatzAntwort, Kurz, "die Leseseite kennt den Datensatz nicht", b.Host);

        var paare = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var paar = Guid.NewGuid();
            paare.Add(paar);
            (await ap.QuittungAsync(ap.Sende(new NimmPaarAuf(new(datensatz), new(paar))), Kurz)).Nachricht.Should().BeOfType<PaarAufgenommen>();
            var antwort = (DatensatzAntwort)await ap.FrageAsync(new HoleDatensatz(new(datensatz)), Kurz, typeof(DatensatzAntwort), typeof(DatensatzNichtGefunden));
            antwort.Mitglieder.Select(m => m.Wert).Should().BeEquivalentTo(paare, $"Query {i + 1} direkt nach der Quittung zeigt den eigenen Stand");
        }

        await ap.QuittungAsync(ap.Sende(new SetzeSplit(new(datensatz), 80, 10, 10, 7)), Kurz);
        var nachSplit = (DatensatzAntwort)await ap.FrageAsync(new HoleDatensatz(new(datensatz)), Kurz, typeof(DatensatzAntwort), typeof(DatensatzNichtGefunden));
        (nachSplit.Split.TrainProzent, nachSplit.Split.Seed).Should().Be((80, 7));
    }

    /// <summary>
    /// Kennt der Client ein Aggregat nur aus einer Antwort — er war nicht verbunden, als es entstand, und hat keinen Push dazu gesehen —, trägt
    /// sein nächster Command die Version aus der Antwort (eine Version je Aggregat, Plan Phase 5): der Server prüft gegen sie (OCC), statt den
    /// Command ungeprüft anzunehmen, und weil keine Marke die Version verschiebt, passt sie.
    /// </summary>
    [Fact]
    public async Task Kennt_der_Client_ein_Aggregat_nur_aus_einer_Antwort_traegt_sein_Command_die_Version_der_Antwort()
    {
        var datensatz = Guid.NewGuid();
        await using (var schreiber = await Arbeitsplatz.VerbindeAsync(b.Host, Testumgebung.TokenArbeitsplatz))
        {
            await schreiber.QuittungAsync(schreiber.Sende(new ErstelleDatensatz(new(datensatz), "nur aus der Antwort")), Kurz);
            await schreiber.QuittungAsync(schreiber.Sende(new NimmPaarAuf(new(datensatz), new(Guid.NewGuid()))), Kurz);
        }
        var kopf = (await b.Log.StreamAsync(datensatz)).Max(e => e.Version);

        await using var ap = await Arbeitsplatz.VerbindeAsync(b.Host, Testumgebung.TokenArbeitsplatz);
        ap.Versionen.GetVersion(datensatz).Should().BeNull("der Client hat keinen Push zu diesem Datensatz gesehen");
        await Warte.BisAsync(async () => await ap.FrageAsync(new HoleDatensatz(new(datensatz)), Kurz, typeof(DatensatzAntwort), typeof(DatensatzNichtGefunden))
            is DatensatzAntwort { Mitglieder.Count: 1 }, Kurz, "die Leseseite kennt den Datensatz nicht", b.Host);
        ap.Versionen.GetVersion(datensatz).Should().Be(kopf, "die Antwort nennt den Stand des Datensatzes — den Kopf seines Streams");

        var quittung = await ap.QuittungAsync(ap.Sende(new SetzeSplit(new(datensatz), 70, 20, 10, 3)), Kurz);
        quittung.Nachricht.Should().BeOfType<SplitGesetzt>("mit der Version aus der Antwort entscheidet das Aggregat, statt einen Konflikt zu melden");
        quittung.Kontext.AggregateVersion.Should().Be(kopf + 1);
        ap.Alles.Where(e => e.Nachricht is CommandFailed).Should().BeEmpty();
    }

    /// <summary>
    /// Auch ein eben angelegtes Objekt muss die nächste Query des Clients kennen. Heute nicht: der Client fasst nur nach, wenn die Antwort für das
    /// Objekt einen Stand mitbringt, der hinter seinem Schreiben liegt — für ein neues Objekt hat die Projektion noch keinen Eintrag im
    /// Versions-Index, und „kein Eintrag“ gilt als frisch.
    /// </summary>
    [Fact(Skip = "G-14 offen (docs/13)")]
    public async Task Nach_dem_Anlegen_kennt_die_naechste_Query_des_Clients_das_neue_Objekt()
    {
        await using var ap = await Arbeitsplatz.VerbindeAsync(b.Host, Testumgebung.TokenArbeitsplatz);
        for (var i = 0; i < 5; i++)
        {
            var datensatz = Guid.NewGuid();
            await ap.QuittungAsync(ap.Sende(new ErstelleDatensatz(new(datensatz), $"neu {i}")), Kurz);
            (await ap.FrageAsync(new HoleDatensatz(new(datensatz)), Kurz, typeof(DatensatzAntwort), typeof(DatensatzNichtGefunden)))
                .Should().BeOfType<DatensatzAntwort>($"Query {i + 1} gleich nach der Quittung kennt den neuen Datensatz");
        }
    }
}
