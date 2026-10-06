using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Modell;
using Domain.Projections;
using Domain.Trainingslauf;
using FluentAssertions;
using Infrastructure.Akteure;
using Infrastructure.GrpcClient;
using Xunit;

namespace Infrastructure.Pruefstand.Akteure;

/// <summary>
/// Client-Verträge (<c>docs/konzept-akteure.md</c> §4): die generierte Tabelle, die Schnittmenge Vertrag ∩ Token, der Handshake je
/// Client, der Akteur je Antwort bei einem Client mit mehreren Akteuren und die Hash-Gleichheit mit dem Python-Generat — rein, ohne gRPC.
/// </summary>
public class ClientVertragTests
{
    private static ClientVertrag C(string name) => GeneratedClientVertraege.Alle[name];
    private static AkteurRechte A(string name) => GeneratedAkteurRechte.Alle[name];

    [Fact]
    public void Die_Clients_der_Domaene_stehen_in_der_Tabelle()
    {
        GeneratedClientVertraege.Alle.Keys.Should().BeEquivalentTo("KlassifikationsWorker", "TrainingsWorker", "Arbeitsplatz");
        C("KlassifikationsWorker").Verkoerpert.Should().Equal("Klassifizierer");
        C("TrainingsWorker").Verkoerpert.Should().Equal("TrainingsSystem");
    }

    [Fact]
    public void Ein_Client_verkoerpert_mehrere_Akteure()
    {
        var ap = C("Arbeitsplatz");
        ap.Verkoerpert.Should().BeEquivalentTo("Inspekteur", "KIOperator", "Modellfreigeber", "Produktpruefer");
        ap.Traegt.Should().BeEmpty("der Arbeitsplatz handelt spontan, er trägt keine Zusage mit Ausgabe");
        ap.Kenntnis.Should().Contain(typeof(ImagePairKomplett)).And.Contain(typeof(PhysischesProduktGelabelt));
    }

    [Fact]
    public void Verkoerpert_nimmt_nur_Akteure_hinzu_die_der_Client_braucht()
        => C("KlassifikationsWorker").Verkoerpert.Should().NotContain("Modellfreigeber",
            "HoleAktivesModell darf auch der Modellfreigeber — der Klassifizierer aus dem getragenen Teil darf es schon");

    [Fact]
    public void Der_Rand_schneidet_aus_der_Befugnis()
    {
        var r = C("Arbeitsplatz").Rechte(GeneratedAkteurRechte.Alle, token: null)!;
        r.Teile.Select(t => t.Name).Should().BeEquivalentTo("Inspekteur", "KIOperator", "Modellfreigeber", "Produktpruefer");
        r.DarfHinein(typeof(LabelBildPaar)).Should().BeTrue();
        r.DarfHinein(typeof(SetzeModellAktiv)).Should().BeTrue();
        r.DarfHinein(typeof(LabelEinzelBild)).Should().BeFalse("der Inspekteur darf es, aber der Arbeitsplatz sendet es nicht (kein ISendet)");
        r.DarfHinein(typeof(NimmRangeAuf)).Should().BeFalse("niemand darf es direkt — der Vertrag erweitert die Befugnis nie");
        r.AkteurFuer(typeof(SetzeModellAktiv)).Should().Be("Modellfreigeber");
        r.AkteurFuer(typeof(LabelPhysischesProdukt)).Should().Be("Produktpruefer");
    }

    [Fact]
    public void Wirksam_ist_Vertrag_geschnitten_mit_dem_Token()
    {
        var anna = C("Arbeitsplatz").Rechte(GeneratedAkteurRechte.Alle, A("Inspekteur"))!;
        anna.Teile.Select(t => t.Name).Should().Equal("Inspekteur");
        anna.DarfHinein(typeof(LabelBildPaar)).Should().BeTrue();
        anna.DarfHinein(typeof(FriereEin)).Should().BeFalse("das ist der KIOperator-Anteil — Anna ist nur Inspekteurin");

        var zwei = C("Arbeitsplatz").Rechte(GeneratedAkteurRechte.Alle, AkteurRechte.Vereinige([A("Inspekteur"), A("KIOperator")]))!;
        zwei.Teile.Select(t => t.Name).Should().BeEquivalentTo("Inspekteur", "KIOperator");

        C("Arbeitsplatz").Rechte(GeneratedAkteurRechte.Alle, A("Klassifizierer")).Should().BeNull("das Token verkörpert keinen Akteur dieses Clients");
    }

    [Fact]
    public void Handshake_nennt_den_Client_und_abonniert_genau_seine_Eingaenge()
    {
        var e = AkteurVertragsPruefung.Pruefe("KlassifikationsWorker", C("KlassifikationsWorker").Hash, null, GeneratedAkteurRechte.Alle,
            streng: true, GeneratedClientVertraege.Alle);
        e.Ablehnung.Should().BeNull();
        e.Client!.Name.Should().Be("KlassifikationsWorker");

        var wunsch = new CapabilitiesResult { SubscribedEvents = { "TrainingAngefordert" } };
        AkteurVertragsPruefung.Wende(wunsch, e.Vertrag!, Loese, _ => false, _ => false);
        wunsch.SubscribedEvents.Should().BeEquivalentTo("BildVerfuegbar", "ImagePairKomplett", "ModellAktiviert");
        wunsch.AllowedCommands.Should().BeEquivalentTo("KlassifiziereBildPaarDurchKi", "KlassifiziereEinzelBildDurchKi");
    }

    [Fact]
    public void Handshake_lehnt_Token_ohne_passenden_Akteur_und_streng_einen_falschen_Hash_ab()
    {
        AkteurVertragsPruefung.Pruefe("Arbeitsplatz", C("Arbeitsplatz").Hash, A("TrainingsSystem"), GeneratedAkteurRechte.Alle, false,
            GeneratedClientVertraege.Alle).Ablehnung.Should().Contain("keinen der Akteure des Clients Arbeitsplatz");
        AkteurVertragsPruefung.Pruefe("Arbeitsplatz", "0000000000000000", null, GeneratedAkteurRechte.Alle, true, GeneratedClientVertraege.Alle)
            .Ablehnung.Should().Contain("anderen Domänen-Stand");
        AkteurVertragsPruefung.Pruefe("Arbeitsplatz", "0000000000000000", null, GeneratedAkteurRechte.Alle, false, GeneratedClientVertraege.Alle)
            .Warnung.Should().Contain("anderen Domänen-Stand");
    }

    [Fact]
    public void Der_Akteur_Vertrag_per_Akteur_Name_bleibt_als_Uebergang()
        => AkteurVertragsPruefung.Pruefe("Klassifizierer", A("Klassifizierer").VertragHash, null, GeneratedAkteurRechte.Alle, true,
            GeneratedClientVertraege.Alle).Vertrag!.Name.Should().Be("Klassifizierer");

    /// <summary>Ein Client, der Klassifizierer UND TrainingsSystem trägt (wie ein ML-Worker auf einer GPU-Maschine): jede Antwort im Namen
    /// des Akteurs, dessen Teil sie vorsieht — nicht des ersten, der den Command darf.</summary>
    [Fact]
    public void Ein_Client_mit_zwei_Akteuren_antwortet_je_Zusage_im_richtigen_Namen()
    {
        var k = A("Klassifizierer");
        var t = A("TrainingsSystem");
        var ml = new ClientVertrag("MlWorker", typeof(object), "h",
            new Dictionary<string, IReadOnlyDictionary<Type, IReadOnlySet<Type>>> { ["Klassifizierer"] = k.Vertrag, ["TrainingsSystem"] = t.Vertrag },
            t.Stroeme, new HashSet<Type>(), new HashSet<Type> { typeof(HoleDatensatzSamples) }, new HashSet<Type>(), ["Klassifizierer", "TrainingsSystem"]);
        var r = ml.Rechte(GeneratedAkteurRechte.Alle, null)!;

        r.AkteurFuerZusage(typeof(ImagePairKomplett), typeof(KlassifiziereBildPaarDurchKi)).Should().Be("Klassifizierer");
        r.AkteurFuerZusage(typeof(TrainingAngefordert), typeof(MeldeFortschritt)).Should().Be("TrainingsSystem");
        r.Zugesagt(typeof(TrainingAngefordert), typeof(KlassifiziereBildPaarDurchKi)).Should().BeFalse();
        r.Hoert.Should().Contain(typeof(ImagePairKomplett)).And.Contain(typeof(TrainingAbgebrochen));
        r.DarfHinein(typeof(HoleDatensatzSamples)).Should().BeTrue();

        var wunsch = new CapabilitiesResult();
        AkteurVertragsPruefung.Wende(wunsch, r, Loese, _ => false, _ => false);
        wunsch.SubscribedEvents.Should().BeEquivalentTo("BildVerfuegbar", "ImagePairKomplett", "ModellAktiviert", "TrainingAbgebrochen", "TrainingAngefordert");
    }

    /// <summary>Beide Generate aus derselben Quelle (Akteurvertrag.ClientKanon) — der Python-Client meldet so den Server-Hash.</summary>
    [Fact]
    public void Python_Generat_traegt_dieselben_Client_Hashes_wie_der_Server()
    {
        var text = File.ReadAllText(Path.Combine(Wurzel(), "Domain.Client.Worker.Python.ML", "domain_client", "generated", "vertraege.py"));
        var clients = Regex.Matches(text, "CLIENT: ClassVar\\[str\\] = \"(\\w+)\"[\\s\\S]*?VERTRAG_HASH: ClassVar\\[str\\] = \"(\\w+)\"")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        clients.Should().BeEquivalentTo(GeneratedClientVertraege.Alle.ToDictionary(x => x.Key, x => x.Value.Hash),
            "vertraege.py ist veraltet → ./codegen.sh --force");
    }

    private static string Wurzel()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (d.GetFiles("*.sln").Length > 0) return d.FullName;
        throw new InvalidOperationException("Solution-Wurzel nicht gefunden");
    }

    private static readonly Dictionary<string, Type> Typen = new[]
    {
        typeof(ImagePairKomplett), typeof(BildVerfuegbar), typeof(ModellAktiviert), typeof(TrainingAngefordert), typeof(TrainingAbgebrochen),
        typeof(KlassifiziereBildPaarDurchKi), typeof(KlassifiziereEinzelBildDurchKi),
    }.ToDictionary(t => t.Name);

    private static Type? Loese(string name) => Typen.GetValueOrDefault(name);
}
