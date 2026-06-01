using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Hält den gesamten Chart-State: Strip, Tagesliste, Verlauf, gewählter Tag.
///
/// Empfängt:
///   - ProduktionsVerlaufAntwort  → Verlauf setzen
///   - ProduktionsTageAntwort     → ProduktionsTage setzen
///   - ProduktionsStripAntwort    → Strip setzen, WarteAufStrip = false
///   - TagGewaehlt                → GewaehlterTag setzen, WarteAufStrip = true
///   - ImagePairErstellt          → Strip live um neuen Punkt erweitern
///   - PhysischesProduktGelabelt  → Strip-Punkt ProduktLabel updaten
///
/// Publisht nichts — Queries kommen aus ChartViewModel.
/// Liest keine anderen Stores.
/// </summary>
public partial class ChartStore : StoreBase
{
    // ═══════════════════════════════════════════════════
    // STATE
    // ═══════════════════════════════════════════════════

    [ObservableProperty] private ProduktionsVerlaufAntwort? _verlauf;
    [ObservableProperty] private ProduktionsTageAntwort? _produktionsTage;
    [ObservableProperty] private ProduktionsStripAntwort? _strip;
    [ObservableProperty] private DateTimeOffset? _gewaehlterTag;
    [ObservableProperty] private bool _warteAufStrip;

    // ═══════════════════════════════════════════════════
    // HANDLE — Query-Responses
    // ═══════════════════════════════════════════════════

    void Handle(ProduktionsVerlaufAntwort antwort, MessageContext ctx)
    {
        Verlauf = antwort;
    }

    void Handle(ProduktionsTageAntwort antwort, MessageContext ctx)
    {
        ProduktionsTage = antwort;
    }

    void Handle(ProduktionsStripAntwort antwort, MessageContext ctx)
    {
        Strip = antwort;
        WarteAufStrip = false;
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Client-Events
    // ═══════════════════════════════════════════════════

    void Handle(TagGewaehlt evt, MessageContext ctx)
    {
        GewaehlterTag = evt.Datum;

        // WarteAufStrip nur wenn ein echter Tag gewählt wurde (nicht null = Auswahl aufheben)
        WarteAufStrip = evt.Datum != null;
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Server-Events (live)
    // ═══════════════════════════════════════════════════

    void Handle(ImagePairErstellt evt, MessageContext ctx)
    {
        // Strip live erweitern wenn bereits geladen.
        // Der neue Punkt hat noch kein ProduktLabel (kommt später per PhysischesProduktGelabelt).
        // Zeitlich korrekt einsortiert — Strip bleibt chronologisch.
        if (Strip == null) return;

        var neuePunkte = Strip.Punkte
            .Append(new ProduktionsStripPunkt(evt.ProduziertAm, ProduktLabel: null))
            .OrderBy(p => p.Zeitpunkt)
            .ToList();

        Strip = new ProduktionsStripAntwort(neuePunkte);
    }

    void Handle(PhysischesProduktGelabelt evt, MessageContext ctx)
    {
        // Strip-Punkt für dieses Teil mit neuem ProduktLabel aktualisieren.
        // Matching über AggregateId → ProduziertAm ist nicht direkt verfügbar,
        // daher wird der Zeitpunkt aus ctx.AggregateId nicht direkt genutzt —
        // wir matchen stattdessen über den Zeitpunkt des zuletzt bekannten Eintrags.
        //
        // TODO Phase 3: ListStore.FindById(ctx.AggregateId)?.ProduziertAm für exaktes Matching.
        //               Derzeit: Strip bleibt unverändert wenn ProduziertAm nicht bekannt.
        //               Auswirkung: Strip-Farbe verzögert sich bis zum nächsten Strip-Load.
        //
        // Wenn wir den AggregateId im Strip speichern würden, wäre es direkt.
        // ProduktionsStripPunkt enthält aber nur Zeitpunkt + Label (leichtgewichtig by design).
        if (Strip == null) return;
    }
}
