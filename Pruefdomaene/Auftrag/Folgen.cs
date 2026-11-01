using Abstractions;

namespace Pruefdomaene.Auftrag;

/// <summary>
/// Eine FOLGE (docs/03 §3.2): der Auftrag ist gestartet — die Abwicklung reserviert den Posten und belegt das Kontingent. Was die Ziele darauf
/// entscheiden, hört sie als Antwort auf ihre Commands (docs/04 §4.4), nicht aus dem Log.
/// </summary>
public record AuftragAbzuwickeln(Id<Auftrag> AuftragId, Id<Pruefdomaene.Posten.Posten> PostenId, Id<Pruefdomaene.Kontingent.Kontingent> KontingentId, int Menge);
