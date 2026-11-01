using Abstractions;

namespace Pruefdomaene.Import;

public partial class Importauftrag
{
    public partial class Decider : IDecider<Importauftrag>
    {
        /// <summary>Der Start ist ein Fakt; den Lauf über die Zeilen sagt die Folge dem Importlauf.</summary>
        public IEnumerable<OneOf<ImportGestartet, ImportExistiert, ImportZuLaufen>> Decide(StarteImport cmd)
        {
            if (this.State.Existiert) { yield return new ImportExistiert(cmd.AggregateId); yield break; }
            yield return new ImportGestartet(cmd.Zeilen);
            yield return new ImportZuLaufen(cmd.AggregateId, cmd.Zeilen);
        }

        /// <summary>Der Stand zählt nur vorwärts: eine verspätete oder wiederholte Meldung ändert nichts.</summary>
        public IEnumerable<OneOf<ImportStandGemeldet>> Decide(MeldeImportStand cmd)
        {
            if (cmd.Zeilen <= this.State.Stand) yield break;
            yield return new ImportStandGemeldet(cmd.Zeilen);
        }
    }
}
