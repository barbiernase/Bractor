using Abstractions;

namespace Pruefdomaene.Messung;

public record MesswertErfasst(double Wert) : IEvent;
public record SchaetzungVermerkt(double Erwartet, int Anzahl) : IEvent;
public record AusreisserGemeldet(double Wert) : IEvent;
