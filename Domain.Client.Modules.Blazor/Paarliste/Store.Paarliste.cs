using Client.Infrastructure.Abstractions;
using Domain.ImagePair;
using Domain.Client.Modules.Suche;
namespace Domain.Client.Modules;

/// <summary>
/// Filter-, Auswahl- und Modus-State. Jede Änderung invalidiert das Fenster und
/// stellt den Sammel-Zähler neu scharf (NeuLaden); das Feuern der Query(s)
/// übernimmt der PaarlisteRefreshHandler.
/// </summary>
public partial class Store
{
    // Baum-Auswahl: Tage → minimale Bereiche. Bestimmt Anzahl und Grenzen der Queries.
    void Handle(AuswahlGeaendert evt, MessageContext ctx)
    { AktiveBereiche = Bereiche.AusTagen(evt.Tage); NeuLaden(); }

    // Feingranulare Kriterien (Labels/Inspektion): Bereiche bleiben, neu laden.
    void Handle(SucheGeaendert evt, MessageContext ctx)
    { Suche = evt.Kriterien; NeuLaden(); }

    // Grobe Presets (StatusBar-Keys 1/2/3): auf Suche abbilden, Bereiche bleiben.
    void Handle(FilterWertGeaendert evt, MessageContext ctx)
    {
        Suche = evt.Wert switch
        {
            "offen"    => Suche with { HatMenschLabel = false, ProduktLabel = null },
            "anomalie" => Suche with { ProduktLabel = Klassifikation.Anomalie, HatMenschLabel = null },
            _          => Suche with { ProduktLabel = null, HatMenschLabel = null },
        };
        NeuLaden();
    }

    void Handle(InspektionsfilterGetoggelt evt, MessageContext ctx)
    { Suche = Suche with { NurNichtInspizierte = !Suche.NurNichtInspizierte }; NeuLaden(); }

    void Handle(ModusGeaendert evt, MessageContext ctx)
    { Modus = evt.Modus; NeuLaden(); }

    // Tag-Labeling = unbegrenzter Pfad mit gesetztem Von/Bis (keine Baum-Bereiche).
    void Handle(TagLabelingGestartet evt, MessageContext ctx)
    {
        var von = new DateTimeOffset(evt.Datum.Date, TimeSpan.Zero);
        Modus = ArbeitsModus.Tag;
        AktiveBereiche = System.Array.Empty<Bereich>();
        Suche = Suche with { Von = von, Bis = von.AddDays(1) };
        NeuLaden();
    }

    void Handle(TagLabelingBeendet evt, MessageContext ctx)
    {
        Modus = ArbeitsModus.Live;
        Suche = Suche with { Von = null, Bis = null };
        NeuLaden();
    }
}
