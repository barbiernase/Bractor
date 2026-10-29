using FluentAssertions;
using Werkbank.Server;
using Xunit;

namespace Infrastructure.Pruefstand.Werkbank;

/// <summary>
/// Die Daten-Prüfung von ⟳ (docs/06 §6.5) liest die Events aus dem ECHTEN Anwendungs-Proto — seit der getrennten Leitung aus dem
/// Abschnitt „EVENT PAYLOADS“, bei einem alten gespeicherten Stand aus dem damaligen Event-Umschlag. Ohne das fand sie im neuen Proto keine Events
/// und prüfte nichts (bzw. meldete gegen einen alten Stand jedes Event als verschwunden).
/// </summary>
public class ProtoVergleichTests
{
    private static string DomainProto()
    {
        var d = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (d != null && d.GetFiles("*.sln").Length == 0) d = d.Parent;
        return System.IO.File.ReadAllText(System.IO.Path.Combine(d!.FullName,
            Abstractions.Projektstruktur.Projekt(Abstractions.Projektstruktur.StandardWurzel, Abstractions.Projektstruktur.Rolle.Leitung),
            Abstractions.Projektstruktur.ProtoDatei));
    }

    [Fact]
    public void Findet_die_Events_im_Abschnitt_des_Anwendungs_Protos_und_nur_sie()
    {
        var events = ProtoVergleich.Events(DomainProto());
        events.Should().ContainKey("BildVerfuegbarDto").And.ContainKey("CommandFailedDto");
        events.Should().NotContainKey("ErstelleImagePairDto", "ein Command ist kein Event");
        events["BildVerfuegbarDto"].Should().ContainKey("version");
        ProtoVergleich.Unvertraeglich(DomainProto(), DomainProto()).Should().BeEmpty();
    }

    [Fact]
    public void Ein_verschwundenes_Event_oder_Feld_ist_unvertraeglich_auch_gegen_den_alten_Umschlag()
    {
        const string alt = """
            message EventEnvelopeDto {
                string id = 1;
                oneof payload {
                    GebuchtDto gebucht = 10;
                    StorniertDto storniert = 11;
                }
            }
            message GebuchtDto {
                string konto = 1;
                int64 betrag = 2;
            }
            message StorniertDto {
                string grund = 1;
            }
            """;
        const string neu = """
            // ============================================================================
            // COMMAND PAYLOADS
            // ============================================================================
            message BucheDto {
                string konto = 1;
            }
            // ============================================================================
            // EVENT PAYLOADS
            // ============================================================================
            message GebuchtDto {
                string konto = 1;
                string betrag = 2;
            }
            // ============================================================================
            // QUERY PAYLOADS
            // ============================================================================
            message StorniertDto {
                string grund = 1;
            }
            """;
        ProtoVergleich.Unvertraeglich(alt, neu).Should().BeEquivalentTo(
            "Gebucht.betrag: int64 → string", "Event Storniert gibt es nicht mehr");
    }
}
