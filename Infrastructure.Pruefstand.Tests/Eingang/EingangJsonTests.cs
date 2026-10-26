using System;
using System.Linq;
using System.Text.Json;
using Abstractions;
using Domain.Pipeline.Bildaufbereitung;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Projections;
using FluentAssertions;
using Infrastructure.Eingang;
using Infrastructure.Funktionen;
using Infrastructure.Mapping;
using Xunit;
using Domain.Projections.Datensatz;

namespace Infrastructure.Pruefstand.Eingang;

/// <summary>
/// Der JSON-Eingang der Werkbank (Werkbank P5, konzept-werkbank §12.3 E5), store-frei: Name → Typ über die generierte Registry, Felder über den generierten
/// Wire-Kontext (keine Reflection), Enums als Namen — für Commands, Queries, Antworten und die Ausgaben von Funktionen.
/// </summary>
public class EingangJsonTests
{
    private static JsonElement J(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Command_mit_Enum_als_Name_und_Werte_Objekt()
    {
        MessageTypeMapping.Resolve("NimmBildAuf").Should().Be((typeof(NimmBildAuf), MessageCategory.Command));
        var id = Guid.NewGuid();
        var cmd = (NimmBildAuf)EingangJson.Lies(J($$"""
            { "AggregateId": "{{id}}", "PairKey": "P", "Version": "Dc2", "Pfad": "/x.png",
              "Meta": { "OriginalDateiname": "a.tiff", "DateigroesseBytes": 7, "BreitePixel": 3, "HoehePixel": 2, "ErstelltAm": "2026-10-12T10:00:00+00:00" } }
            """), typeof(NimmBildAuf));
        cmd.AggregateId.Wert.Should().Be(id);
        cmd.Version.Should().Be(BildVersion.Dc2);
        cmd.Meta.BreitePixel.Should().Be(3);
        EingangJson.Schreibe(cmd).GetProperty("Version").GetString().Should().Be("Dc2", "die Werkbank zeigt Enums als Namen");
    }

    [Fact]
    public void Query_und_Antwort_stehen_im_Wire_Kontext()
    {
        MessageTypeMapping.Resolve("HoleDatensatzPaare").Category.Should().Be(MessageCategory.Query);
        var q = (HoleDatensatzPaare)EingangJson.Lies(J($$"""{ "DatensatzId": "{{Guid.Empty}}", "Modus": "Ausgeschlossen" }"""), typeof(HoleDatensatzPaare));
        q.Modus.Should().Be(DatensatzPaarModus.Ausgeschlossen);
        q.Seite.Should().Be(1, "fehlende Felder behalten ihren Standard");

        // Jede Antwort, die ein Reader liefern kann, lässt sich schreiben (sonst bricht /eingang/query erst zur Laufzeit).
        foreach (var antwort in GeneratedTypeRegistry.QueryResponses.Values)
            FluentActions.Invoking(() => Infrastructure.Serialization.CqrsWireJsonContext.Default.GetTypeInfo(antwort))
                .Should().NotThrow().Which.Should().NotBeNull($"{antwort.Name} fehlt im Wire-Kontext");
    }

    [Fact]
    public void Ausgaben_von_Funktionen_sind_lesbar()
    {
        foreach (var ausgabe in Infrastructure.Komposition.Anwendung.Funktionen.Bausteine.SelectMany(b => b.Eingaenge).SelectMany(e => e.Ausgaben).Distinct())
            Infrastructure.Serialization.CqrsWireJsonContext.Default.GetTypeInfo(ausgabe).Should().NotBeNull($"{ausgabe.Name} fehlt im Wire-Kontext");
        var e = (BildNichtLesbar)EingangJson.Lies(J("""{ "Pfad": "/k.tiff", "Grund": "kaputt" }"""), typeof(BildNichtLesbar));
        e.Should().Be(new BildNichtLesbar("/k.tiff", "kaputt"));
    }

    [Fact]
    public void Fehlende_Daten_gelten_als_leeres_Objekt_und_Unsinn_ist_ein_Fehler()
    {
        EingangJson.Lies(default, typeof(HoleDatensaetze)).Should().BeOfType<HoleDatensaetze>();
        FluentActions.Invoking(() => EingangJson.Lies(J("""{ "BreitePixel": "breit" }"""), typeof(BildAufbereitet))).Should().Throw<JsonException>();
        FluentActions.Invoking(() => EingangJson.Lies(J("{}"), typeof(EingangJsonTests))).Should().Throw<NotSupportedException>().WithMessage("*Wire-Kontext*");
    }
}
