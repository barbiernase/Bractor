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
    // Fristen für das Fälligkeits-Verzeichnis der Uhr (Plan Pub/Sub-Neustrukturierung Phase 3).
    private List<Guid>? _fristStreams;
    private List<int>? _fristVersionen;
    private List<TimeSpan>? _fristNach;
    private List<string>? _fristBefehle, _fristDaten;
    private List<string?>? _fristAkteure;
    // Kennungen der Quell-Nachrichten in einem Stream je Schlüssel (Plan Pub/Sub-Neustrukturierung Phase 3).
    private List<Guid>? _kennungen, _kennungStreams;

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
                case QuellKennung k:
                    (_kennungen ??= new()).Add(k.Vorgang);
                    (_kennungStreams ??= new()).Add(streamId);
                    break;
                case FristFaellig f:
                    (_fristStreams ??= new()).Add(streamId);
                    (_fristVersionen ??= new()).Add(f.Version);
                    (_fristNach ??= new()).Add(f.Nach);
                    (_fristBefehle ??= new()).Add(f.Befehl);
                    (_fristDaten ??= new()).Add(f.Daten);
                    (_fristAkteure ??= new()).Add(f.Akteur);
                    break;
            }
        }
    }

    /// <summary>Die gesammelten Quittungen als eine Anweisung derselben Transaktion (Marten führt sie nach den Events aus).</summary>
    public void Abschliessen(IDocumentSession session, IDocumentStore store)
    {
        if (_commands is not null) InboxTabelle.Quittiere(session, InboxTabelle.Name(store), _commands, _streams!, _abgelehnt!);
        if (_kennungen is not null)
            session.QueueSqlCommand($"insert into {Infrastructure.Quellen.QuellKennungen.Name(store)} (vorgang, stream_id) select * from unnest(?::uuid[], ?::uuid[])",
                _kennungen.ToArray(), _kennungStreams!.ToArray());
        if (_fristStreams is not null)
            Infrastructure.Uhr.FristVerzeichnis.Lege(session, store, _fristStreams, _fristVersionen!, _fristNach!, _fristBefehle!, _fristDaten!, _fristAkteure!);
    }

    /// <summary>Die CommandIds der Quittungen (für die Ausnahme einer Wiederholung).</summary>
    public static Guid? QuittungIn(IReadOnlyList<Beilage>? beilagen)
        => beilagen?.OfType<Quittung>().Select(q => (Guid?)q.CommandId).FirstOrDefault();
}
