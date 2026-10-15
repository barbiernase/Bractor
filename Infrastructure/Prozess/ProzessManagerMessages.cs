using System;
using Abstractions;

namespace Infrastructure.Prozess;

/// <summary>
/// Weckt den Prozess-Manager einer Korrelation (die Cluster-Identität IST die Korrelation). Trägt die
/// Quelle der Weckung (Stream/Version des auslösenden Events) — nötig, weil der Manager bei der ERSTEN
/// Weckung (Start) das Auslöse-Event lesen muss. <see cref="ProzessNameFürStart"/> ist nur beim Start
/// gesetzt (dann kennt der Router den Prozess-Typ); bei Folge-Weckungen liest der Manager ihn aus seinem Log.
/// <see cref="FristFaellig"/>: der Backstop weckt, weil die Frist im Offen-Index abgelaufen ist — der Manager schreibt sie dann in jedem
/// Fall neu (PF-4).
/// </summary>
public sealed record ProzessWake(Guid QuellStream, int QuellVersion, string? ProzessNameFürStart, bool FristFaellig = false) : IWireMessage;
