using Abstractions;

namespace Pruefdomaene.Posten;

/// <summary>
/// Eine FOLGE (docs/03 §3.1): eine Reservierung hat den Posten unter seinen Mindestbestand gedrückt. Ein schlichter Record in der Decide-Signatur —
/// nicht im Log; je hörende Funktion legt die Entscheidung einen offenen Eingang in derselben Transaktion.
/// </summary>
public record Unterschritten(Id<Posten> Posten, int Fehlmenge);
