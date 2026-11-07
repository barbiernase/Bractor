using Abstractions;

namespace Pruefdomaene.Pipeline.Posten;

// COMMANDS ohne Ziel am Kopf des Nachschubablaufs (docs/04 §4.4): nie im Log. Ein Client, der auf das Fakt reagiert, gibt ihn hinein (Schritt 6: ein Decide gibt nur Fakten aus).

/// <summary>Eine Reservierung hat den Posten unter seinen Mindestbestand gedrückt (<c>PostenReserviert</c> mit Fehlmenge): nachbestellen.</summary>
public record Unterschritten(Id<global::Pruefdomaene.Posten.Posten> Posten, int Fehlmenge) : ICommand;
