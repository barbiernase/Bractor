using Abstractions;
using Domain.Antrag;

namespace Domain.Vorgang;

/// <summary>
/// Prozess A der Verkettungs-Demo: auf einen gestellten Antrag hin wird der Vorgang GENEHMIGT. Sein terminales
/// Ergebnis-Event <see cref="VorgangGenehmigt"/> ist ein PERSISTIERTES Domänen-Event (mit Signal) — genau der
/// Ankerpunkt, an dem der Folge-Prozess hängt (siehe <see cref="AktivierungsProzess"/>).
/// </summary>
public sealed class GenehmigungsProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<AntragGestellt>.Definiere(p =>
    {
        p.Auf<AntragGestellt>()
            .Sende<Genehmige>(e => new Genehmige(e.Vorgang));
    });
}
