using Abstractions;
using Domain.Datensatz;

namespace Domain.Pipeline.Datensatz;

/// <summary>Die Range traf Paare: ihre Ids und die Herkunft (Kriterien + Anzahl) für die Nachvollziehbarkeit.</summary>
public record RangeGefunden(IReadOnlyList<Guid> ImagePairIds, RangeHerkunft Herkunft) : IEvent;

/// <summary>Die Range traf kein Paar — nichts aufzunehmen.</summary>
public record RangeOhneTreffer(RangeKriterien Kriterien) : IEvent;

/// <summary>Der immutable Mitglieder-Snapshot (stabile Reihenfolge).</summary>
public record MitgliederEingefroren(IReadOnlyList<DatensatzMitglied> Mitglieder) : IEvent;

/// <summary>Kein Mitglied war im Read-Model auffindbar — das Einfrieren wird nicht abgeschlossen.</summary>
public record KeinMitgliedAuffindbar(int Angefragt) : IEvent;
