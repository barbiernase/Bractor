// ── Domain.Client/ImagePair/Handlers/UserIntentHandler.cs ──

using Client.Infrastructure.Abstractions;
using Domain.ImagePair;

namespace Domain.Client.ImagePair;

/// <summary>
/// Resolved User-Intents zu Domain-Events und Commands.
///
/// Ersetzt die ViewModels (NavigationViewModel, LabelingViewModel, etc.).
/// Die Logik die vorher in den VMs war (Store lesen → Event berechnen)
/// lebt jetzt hier — als Handler, nicht als injizierter Service.
///
/// Sync-Handler: IEnumerable Handle → läuft NACH allen Stores.
/// Liest: NavigationStore, ListStore, ChartStore
/// Yieldet: Domain-Commands (LabelPhysischesProdukt etc.) und Client-Events
/// </summary>
public partial class UserIntentHandler
{
    private readonly NavigationStore _nav;
    private readonly ListStore _list;
    private readonly ChartStore _chart;

    public UserIntentHandler(NavigationStore nav, ListStore list, ChartStore chart)
    {
        _nav = nav;
        _list = list;
        _chart = chart;
    }

    // ═══════════════════════════════════════════════════
    // NAVIGATION
    // ═══════════════════════════════════════════════════

    IEnumerable<object> Handle(NavigateNextAngefordert evt, MessageContext ctx)
    {
        if (_nav.SelectedIndex < _list.Count - 1)
            yield return new ImagePairAusgewaehlt(_list[_nav.SelectedIndex + 1].Id);
    }

    IEnumerable<object> Handle(NavigatePrevAngefordert evt, MessageContext ctx)
    {
        if (_nav.SelectedIndex > 0)
            yield return new ImagePairAusgewaehlt(_list[_nav.SelectedIndex - 1].Id);
    }

    IEnumerable<object> Handle(NavigateNextOffenAngefordert evt, MessageContext ctx)
    {
        for (var i = _nav.SelectedIndex + 1; i < _list.Count; i++)
        {
            if (_list[i].MenschBildpaarLabel == null)
            {
                yield return new ImagePairAusgewaehlt(_list[i].Id);
                yield break;
            }
        }

        // Kein offenes Paar auf dieser Seite → nächste Seite laden
        if (_list.Seite < _list.GesamtSeiten)
            yield return new SeiteAngefordert(_list.Seite + 1);
    }

    // ═══════════════════════════════════════════════════
    // LABELING
    // ═══════════════════════════════════════════════════

    IEnumerable<object> Handle(LabelProduktAngefordert evt, MessageContext ctx)
    {
        if (_nav.AktuelleId is { } id)
            yield return new LabelPhysischesProdukt(id, evt.Label);
    }

    IEnumerable<object> Handle(LabelKameraAngefordert evt, MessageContext ctx)
    {
        if (_nav.AktuelleId is { } id)
            yield return new LabelBildPaar(id, evt.Label);
    }

    // ═══════════════════════════════════════════════════
    // MODUS / TAG-LABELING
    // ═══════════════════════════════════════════════════

    IEnumerable<object> Handle(ModusToggleAngefordert evt, MessageContext ctx)
    {
        var neuerModus = _nav.Modus == ArbeitsModus.Live
            ? ArbeitsModus.Inspekt
            : ArbeitsModus.Live;
        yield return new ModusGeaendert(neuerModus);
    }

    IEnumerable<object> Handle(StarteTagLabelingAngefordert evt, MessageContext ctx)
    {
        if (_chart.GewaehlterTag is { } tag)
            yield return new TagLabelingGestartet(tag);
    }

    IEnumerable<object> Handle(BeendeTagLabelingAngefordert evt, MessageContext ctx)
    {
        if (_nav.Modus == ArbeitsModus.Tag)
            yield return new TagLabelingBeendet();
    }
}
