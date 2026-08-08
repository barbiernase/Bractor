// ── Domain.Client/ImagePair/UserIntents.cs ──

using Client.Infrastructure.Abstractions;
using Domain.ImagePair;

namespace Domain.Client.ImagePair;

/// <summary>
/// User-Intents: Reine Absichtserklärungen der View-Schicht.
///
/// Sections dispatchen Intents. Der UserIntentHandler resolved sie
/// zu Domain-Events/Commands. Die Section kennt keine Domain-Logik.
///
/// Intent → Handler → yield Domain-Event/Command → Store → Changed → Re-Render
/// </summary>

// ── Navigation ──
public record NavigateNextAngefordert : IClientEvent;
public record NavigatePrevAngefordert : IClientEvent;
public record NavigateNextOffenAngefordert : IClientEvent;

// ── Labeling ──
public record LabelProduktAngefordert(Klassifikation Label) : IClientEvent;
public record LabelKameraAngefordert(Klassifikation Label) : IClientEvent;

// ── Modus / Tag-Labeling ──
public record ModusToggleAngefordert : IClientEvent;
public record StarteTagLabelingAngefordert : IClientEvent;
public record BeendeTagLabelingAngefordert : IClientEvent;
