// ── Zielverzeichnis: Domain.Client/ImagePair/Stores/StatistikStore.cs ──

using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Hält die Statistik-Zähler.
///
/// Empfängt:
///   - ImagePairStatistikAntwort    → Server-Statistik (akkurat über alle Daten)
///   - ImagePairStatistikBerechnet  → Handler-berechnete Zähler (IClientEvent)
///
/// Publisht nichts. Liest keine anderen Stores.
/// Subscribed als 5. Kind im Workspace.
/// </summary>
public partial class StatistikStore : StoreBase
{
    // ═══════════════════════════════════════════════════
    // STATE
    // ═══════════════════════════════════════════════════

    [ObservableProperty] private ImagePairStatistikAntwort? _serverStatistik;

    [ObservableProperty] private int _anzahlGesamt;
    [ObservableProperty] private int _anzahlKomplett;
    [ObservableProperty] private int _anzahlMitKiKlassifikation;
    [ObservableProperty] private int _anzahlOhneKlassifikation;
    [ObservableProperty] private int _anzahlAnomalie;

    // ═══════════════════════════════════════════════════
    // HANDLE
    // ═══════════════════════════════════════════════════

    void Handle(ImagePairStatistikAntwort antwort, MessageContext ctx)
    {
        ServerStatistik = antwort;
    }

    void Handle(ImagePairStatistikBerechnet evt, MessageContext ctx)
    {
        AnzahlGesamt = evt.AnzahlGesamt;
        AnzahlKomplett = evt.AnzahlKomplett;
        AnzahlMitKiKlassifikation = evt.AnzahlMitKiKlassifikation;
        AnzahlOhneKlassifikation = evt.AnzahlOhneKlassifikation;
        AnzahlAnomalie = evt.AnzahlAnomalie;
    }
}
