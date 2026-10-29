using Abstractions;
using Cqrs.Leitung;
using Domain.Datensatz;
using FluentAssertions;
using Google.Protobuf;
using Infrastructure.Komposition;
using Infrastructure.Serialization;
using ImagePairAggregat = Domain.ImagePair.ImagePair;

namespace Infrastructure.Pruefstand.Ids;

/// <summary>
/// Typisierte Ids (docs/10 §10.4): das Ziel eines Commands steht in seinem Typ (<see cref="ICommand{TAggregat}"/>),
/// und Log (Event-JSON), Wire (Cross-Node) und Leitung (Proto) tragen eine <see cref="Id{T}"/> als UUID — verlustfrei zurück in denselben Typ.
/// Store-frei: über die generierten Kontexte, die generierte Routing-Tabelle und den Nutzlast-Mapper der Leitung.
/// </summary>
public class TypisierteIdTests
{
    private static readonly Id<ImagePairAggregat>[] Paare = [Id<ImagePairAggregat>.Neu(), Id<ImagePairAggregat>.Neu()];

    private static NimmRangeAuf Command() => new(Id<Datensatz>.Neu(), Paare, new RangeHerkunft(new RangeKriterien(), Paare.Length));

    private static CommandEnvelope Huelle(ICommand cmd) => new() { AggregateType = "Datensatz", Modus = new CommandModus.Client(3), Payload = cmd };

    [Fact]
    public void Das_Ziel_steht_im_Typ_und_die_Huelle_leitet_es_ab()
    {
        var cmd = Command();
        Anwendung.Routing.CommandToAggregate[typeof(NimmRangeAuf)].Should().Be("Datensatz", "das Routing liest ICommand<Datensatz>");
        ((ICommand)cmd).Ziel.Should().Be(cmd.AggregateId.Wert);
        Huelle(cmd).AggregateId.Should().Be(cmd.AggregateId.Wert, "der Envelope trägt keine zweite Angabe des Ziels");
    }

    [Fact]
    public void Im_Log_steht_eine_Id_als_UUID()
    {
        var e = new PaareAufgenommen(Paare, new RangeHerkunft(new RangeKriterien(), Paare.Length));

        var json = GeneratedEventJson.Serialize(e);

        json.Should().Contain($"\"{Paare[0].Wert}\"").And.NotContain(nameof(Id<ImagePairAggregat>.Wert), "eine Id ist ein Skalar, kein Objekt");
        ((PaareAufgenommen)GeneratedEventJson.Deserialize(GeneratedEventJson.Diskriminator(e), json)).ImagePairIds.Should().Equal(Paare);
    }

    [Fact]
    public void Ueber_den_Wire_kommt_der_Command_mit_seinen_Ids_zurueck()
    {
        var cmd = Command();
        var wire = new CqrsWireSerializer();
        var huelle = Huelle(cmd);

        var zurueck = (CommandEnvelope)wire.Deserialize(wire.Serialize(huelle), wire.GetTypeName(huelle));

        zurueck.AggregateId.Should().Be(cmd.AggregateId.Wert);
        var payload = (NimmRangeAuf)zurueck.Payload;
        payload.AggregateId.Should().Be(cmd.AggregateId);
        payload.ImagePairIds.Should().Equal(Paare);
    }

    [Fact]
    public void Auf_der_Leitung_reist_eine_Id_als_UUID()
    {
        var cmd = Command();
        var mapper = new ProtoMessageMapper(Domain.Leitung.GeneratedNutzlast.Instanz);

        var dto = mapper.MapToDto(Huelle(cmd));
        dto.AggregateId.Should().Be(cmd.AggregateId.Wert.ToString());

        var zurueck = (NimmRangeAuf)mapper.MapToDomain(CommandEnvelopeDto.Parser.ParseFrom(dto.ToByteArray())).Payload;
        zurueck.AggregateId.Should().Be(cmd.AggregateId);
        zurueck.ImagePairIds.Should().Equal(Paare);
    }

    [Fact]
    [Trait("Kategorie", "Garantie")]
    public void Eine_Huelle_mit_fremdem_Ziel_wird_an_der_Leitung_abgelehnt()
    {
        var mapper = new ProtoMessageMapper(Domain.Leitung.GeneratedNutzlast.Instanz);
        var dto = mapper.MapToDto(Huelle(Command()));

        var fremdeId = dto.Clone();
        fremdeId.AggregateId = Guid.NewGuid().ToString();
        FluentActions.Invoking(() => mapper.MapToDomain(fremdeId)).Should().Throw<Exception>("die Hülle nennt eine andere Id als der Command");

        var fremdesAggregat = dto.Clone();
        fremdesAggregat.AggregateType = "ImagePair";
        FluentActions.Invoking(() => mapper.MapToDomain(fremdesAggregat)).Should().Throw<Exception>("der Command gehört zu Datensatz");

        var ohneAngabe = dto.Clone();
        ohneAngabe.AggregateId = "";
        ohneAngabe.AggregateType = "";
        mapper.MapToDomain(ohneAngabe).AggregateType.Should().Be("Datensatz", "ohne Angabe folgt das Ziel dem Typ");
    }
}
