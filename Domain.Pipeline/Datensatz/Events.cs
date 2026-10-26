using Abstractions;
using Domain.Datensatz;

namespace Domain.Pipeline.Datensatz;

/// <summary>Hinweis: die Range traf kein Paar — nichts aufzunehmen.</summary>
public record RangeOhneTreffer(RangeKriterien Kriterien) : ITransientEvent;

/// <summary>Hinweis: kein Mitglied war im Read-Model auffindbar — das Einfrieren wird nicht abgeschlossen.</summary>
public record KeinMitgliedAuffindbar(int Angefragt) : ITransientEvent;
