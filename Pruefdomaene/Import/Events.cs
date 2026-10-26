using Abstractions;

namespace Pruefdomaene.Import;

public record ImportGestartet(int Zeilen) : IEvent;
public record ImportStandGemeldet(int Zeilen) : IEvent;
public record ImportExistiert(Id<Importauftrag> ImportId) : ITransientEvent;
