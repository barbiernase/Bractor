using Abstractions;
using Marten;

namespace Infrastructure.Persistence;

/// <summary>
/// Legt die Beilagen eines oder mehrerer Appends (<see cref="Beilage"/>, docs/03 §3.3) in die Marten-Session, deren <c>SaveChanges</c> auch die
/// Events schreibt — eine Transaktion. Die Quittungen sammelt er und schreibt sie zuletzt in EINER Anweisung (<see cref="Abschliessen"/>); die
/// Vorgangs-Buchhaltung (Offen-Index) geht als Dokument-Operation in dieselbe Session. Ein Weg für Einzel-Append und Group-Commit.
/// </summary>
internal sealed class BeilagenSchreiber
{
    private List<Guid>? _commands, _streams;
    private List<bool>? _abgelehnt;

    public void Lege(IDocumentSession session, Guid streamId, IReadOnlyList<Beilage>? beilagen)
    {
        if (beilagen is null) return;
        foreach (var b in beilagen)
        {
            switch (b)
            {
                case Quittung q:
                    (_commands ??= new()).Add(q.CommandId);
                    (_streams ??= new()).Add(streamId);
                    (_abgelehnt ??= new()).Add(q.Ausgang == KommandoAusgang.Abgelehnt);
                    break;
                case VorgangOffen o:
                    session.Store(new ProzessOffen { Id = o.Korrelation, ProzessName = o.ProzessName, SeitUtc = DateTimeOffset.UtcNow });
                    break;
                case VorgangBeendet e:
                    session.Delete<ProzessOffen>(e.Korrelation);
                    break;
            }
        }
    }

    /// <summary>Die gesammelten Quittungen als eine Anweisung derselben Transaktion (Marten führt sie nach den Events aus).</summary>
    public void Abschliessen(IDocumentSession session, IDocumentStore store)
    {
        if (_commands is not null) InboxTabelle.Quittiere(session, InboxTabelle.Name(store), _commands, _streams!, _abgelehnt!);
    }

    /// <summary>Die CommandIds der Quittungen (für die Ausnahme einer Wiederholung).</summary>
    public static Guid? QuittungIn(IReadOnlyList<Beilage>? beilagen)
        => beilagen?.OfType<Quittung>().Select(q => (Guid?)q.CommandId).FirstOrDefault();
}
