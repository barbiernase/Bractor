// ── Zielverzeichnis: Domain.Client/ImagePair/Stores/FeedbackStore.cs ──

using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Connection;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Hält transiente Feedback-Daten: Ablehnungen, letzte Datei, Fehler.
///
/// Empfängt:
///   - Alle Ablehnungs-Events (ITransientEvent) → LetzteAblehnung
///   - BildVerfuegbar                           → LetzteVerfuegbareDatei
///   - QueryFailed                              → Fehlermeldung
///   - ImagePairNichtGefundenAntwort            → LetzteAblehnung
///
/// Publisht nichts. Liest keine anderen Stores.
/// Subscribed als 6. Kind im Workspace.
/// </summary>
public partial class FeedbackStore : StoreBase
{
    // ═══════════════════════════════════════════════════
    // STATE
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Letzte Ablehnungsnachricht vom Server.
    /// Die View zeigt das als Snackbar/Toast.
    /// Typ ist object weil verschiedene Ablehnungs-Records ankommen.
    /// </summary>
    [ObservableProperty] private object? _letzteAblehnung;

    /// <summary>
    /// Letzte verfügbare Datei (Debug-Info).
    /// </summary>
    [ObservableProperty] private LetzteDateiInfo? _letzteVerfuegbareDatei;

    /// <summary>
    /// Letzter Query-Fehler (Debug/Logging).
    /// </summary>
    [ObservableProperty] private string? _letzterFehler;

    // ═══════════════════════════════════════════════════
    // HANDLE — Ablehnungs-Events
    // ═══════════════════════════════════════════════════

    void Handle(BildNichtVerfuegbar evt, MessageContext ctx)
        => LetzteAblehnung = evt;

    void Handle(RegionIndexUngueltig evt, MessageContext ctx)
        => LetzteAblehnung = evt;

    void Handle(PaarNichtKomplett evt, MessageContext ctx)
        => LetzteAblehnung = evt;

    void Handle(ImagePairNichtGefunden evt, MessageContext ctx)
        => LetzteAblehnung = evt;

    void Handle(ImagePairEingabeUngueltig evt, MessageContext ctx)
        => LetzteAblehnung = evt;

    // ═══════════════════════════════════════════════════
    // HANDLE — Query-Responses
    // ═══════════════════════════════════════════════════

    void Handle(ImagePairNichtGefundenAntwort antwort, MessageContext ctx)
        => LetzteAblehnung = antwort;

    void Handle(QueryFailed evt, MessageContext ctx)
    {
        LetzterFehler = $"{evt.QueryType} — {evt.ErrorMessage}";
        Console.WriteLine($"[FeedbackStore] ⚠ QueryFailed: {LetzterFehler}");
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Server-Events
    // ═══════════════════════════════════════════════════

    void Handle(BildVerfuegbar evt, MessageContext ctx)
    {
        LetzteVerfuegbareDatei = new LetzteDateiInfo(
            evt.Meta.OriginalDateiname, evt.Pfad, evt.Version,
            ctx.AggregateId, evt.Meta.DateigroesseBytes, ctx.CreatedAtUtc);
    }
}

/// <summary>
/// Debug-Info über die letzte empfangene Datei.
/// </summary>
public record LetzteDateiInfo(
    string FileName, string Pfad, BildVersion Version,
    Guid PairId, long SizeBytes, DateTimeOffset EmpfangenAm);
