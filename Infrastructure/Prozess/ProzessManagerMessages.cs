using System;
using Abstractions;

namespace Infrastructure.Prozess;

/// <summary>
/// Weckt den Prozess-Manager einer Korrelation (die Cluster-Identität IST die Korrelation). Trägt die
/// Quelle der Weckung (Stream/Version des Events, das sie auslöste) — beim Start liest der Manager daraus das Auslöse-Event, danach liest er
/// nur diesen Stream nach (und gar nichts, wenn er das Event schon kennt). <see cref="Guid.Empty"/> = ohne Quelle (Selbst-Weckung, Funktion). <see cref="ProzessNameFürStart"/> ist nur beim Start
/// gesetzt (dann kennt der Router den Prozess-Typ); bei Folge-Weckungen liest der Manager ihn aus seinem Log.
/// <see cref="VomBackstop"/>: der Backstop weckt (fällige Frist oder Heilung nach Stille) — der Manager pflegt dann die Frist im Offen-Index
/// (PF-4) und räumt den Eintrag eines beendeten Vorgangs ab.
/// </summary>
/// <see cref="Vorgang"/>: die Weckung nach einem Send bzw. einem Funktions-Ergebnis — der Ausgang dieses Vorgangs liegt in
/// <see cref="QuellStream"/> (oder kommt noch); kennt der Fold ihn schon, liest der Manager nichts. <see cref="QuellVersion"/> ist dann ein
/// Hinweis: ab dieser Version steht der Ausgang (der Send hat ihn dort geschrieben); 0 = unbekannt.
public sealed record ProzessWake(Guid QuellStream, int QuellVersion, string? ProzessNameFürStart, bool VomBackstop = false, Guid Vorgang = default)
    : IWireMessage;
