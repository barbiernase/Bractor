using Abstractions;
using Pruefdomaene.Auftrag;

namespace Pruefdomaene.Pipeline.Auftrag;

// COMMANDS ohne Ziel am Kopf des Auftragsablaufs (docs/04 §4.4): nie im Log. Ein Client, der auf das Fakt reagiert, gibt ihn hinein (Schritt 6: ein Decide gibt nur Fakten aus); was die Ziele auf die Commands der
// Abwicklung entscheiden, hört sie als Antwort (docs/04 §4.4), nicht aus dem Log.

/// <summary>Der Auftrag ist gestartet (<see cref="AuftragGestartet"/>): die Abwicklung reserviert den Posten und belegt das Kontingent.</summary>
public record AuftragAbzuwickeln(Id<global::Pruefdomaene.Auftrag.Auftrag> AuftragId, Id<global::Pruefdomaene.Posten.Posten> PostenId,
    Id<global::Pruefdomaene.Kontingent.Kontingent> KontingentId, int Menge) : ICommand;

/// <summary>Gegenzug der gelungenen Reservierung eines gescheiterten Auftrags (<see cref="PostenfreigabeAngefordert"/>): die Abwicklung gibt den Posten frei.</summary>
public record PostenFreizugeben(Id<global::Pruefdomaene.Auftrag.Auftrag> AuftragId, Id<global::Pruefdomaene.Posten.Posten> PostenId, int Menge) : ICommand;

/// <summary>Gegenzug der gelungenen Belegung eines gescheiterten Auftrags (<see cref="KontingententlastungAngefordert"/>).</summary>
public record KontingentZuEntlasten(Id<global::Pruefdomaene.Auftrag.Auftrag> AuftragId, Id<global::Pruefdomaene.Kontingent.Kontingent> KontingentId, int Menge) : ICommand;
