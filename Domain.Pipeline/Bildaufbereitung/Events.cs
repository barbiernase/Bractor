using Abstractions;

namespace Domain.Pipeline.Bildaufbereitung;

public record BildVerkleinert(string Pfad, int BreitePixel, int HoehePixel) : IEvent;

public record HistogrammAusgeglichen(string Pfad, int BreitePixel, int HoehePixel, DateTimeOffset ErstelltAm) : IEvent;

/// <summary>Die Datei ließ sich nicht lesen/schreiben — Ergebnis beider Funktionen.</summary>
public record BildNichtLesbar(string Pfad, string Grund) : IEvent;
