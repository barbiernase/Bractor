using Abstractions;
using Infrastructure.Komposition;
using Marten;

namespace Infrastructure.Persistence;

/// <summary>
/// Legt die Beilagen eines oder mehrerer Appends (<see cref="Beilage"/>, docs/03 §3.3) in die Marten-Session, deren <c>SaveChanges</c> auch die
/// Events schreibt — eine Transaktion. Die Quittungen sammelt er und schreibt sie zuletzt in EINER Anweisung (<see cref="Abschliessen"/>); die
/// Vorgangs-Buchhaltung (Offen-Index) geht als Dokument-Operation in dieselbe Session. Ein Weg für Einzel-Append und Group-Commit.
/// Ein Append ohne Events (nur Quittungen: Noop, Ablehnung) bekommt die Kopf-Prüfung seines Streams (<see cref="PruefeKopf"/>) — die Versionsprüfung,
/// die sonst der Append der Events leistet.
/// </summary>
internal sealed class BeilagenSchreiber
{
    private List<Guid>? _commands, _streams;
    private List<bool>? _abgelehnt;
    private List<Guid>? _kopfStreams;
    private List<long>? _kopfVersionen;
    // Fristen für das Fälligkeits-Verzeichnis der Uhr.
    private List<Guid>? _fristStreams;
    private List<int>? _fristVersionen;
    private List<TimeSpan>? _fristNach;
    private List<string>? _fristBefehle, _fristDaten;
    private List<string?>? _fristAkteure;
    // Kennungen der Quell-Nachrichten in einem Stream je Schlüssel.
    // Das Verzeichnis der Schlüssel der Bündel (Typ, Feld, Schlüssel je Event; die Sequenz liest die Anweisung aus mt_events).
    private List<string>? _rueckTypen, _rueckFelder;
    private List<Guid>? _rueckSchluessel, _rueckStreams;
    private List<int>? _rueckVersionen;

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

    /// <summary>
    /// Ein Event an <paramref name="version"/> des Streams: hört ein Bündel seinen Typ über ein Feld oder global (<c>Anwendung.Rueckblick</c>), kommt
    /// es ins Verzeichnis der Schlüssel (docs/04 §4.3.1) — je Eintrag eine Zeile.
    /// </summary>
    public void Rueckblick(Guid streamId, int version, object payload)
    {
        if (payload is not IEvent e || !Anwendung.Rueckblick.TryGetValue(e.GetType(), out var eintraege)) return;
        var typ = Anwendung.Upcasting.Diskriminator(e);
        foreach (var x in eintraege)
        {
            (_rueckTypen ??= new()).Add(typ);
            (_rueckFelder ??= new()).Add(x.Feld);
            (_rueckSchluessel ??= new()).Add(x.Schluessel(e));
            (_rueckStreams ??= new()).Add(streamId);
            (_rueckVersionen ??= new()).Add(version);
        }
    }

    /// <summary>Ein Append ohne Events an <paramref name="streamId"/>: er gilt nur, wenn der Stream noch auf <paramref name="version"/> steht.</summary>
    public void PruefeKopf(Guid streamId, int version)
    {
        (_kopfStreams ??= new()).Add(streamId);
        (_kopfVersionen ??= new()).Add(version);
    }

    /// <summary>Die gesammelten Quittungen als eine Anweisung derselben Transaktion (Marten führt sie nach den Events aus).</summary>
    public void Abschliessen(IDocumentSession session, IDocumentStore store)
    {
        if (_kopfStreams is not null) InboxTabelle.PruefeKoepfe(session, store, _kopfStreams, _kopfVersionen!);
        if (_commands is not null) InboxTabelle.Quittiere(session, InboxTabelle.Name(store), _commands, _streams!, _abgelehnt!);
        if (_fristStreams is not null)
            Infrastructure.Uhr.FristVerzeichnis.Lege(session, store, _fristStreams, _fristVersionen!, _fristNach!, _fristBefehle!, _fristDaten!, _fristAkteure!);
        if (_rueckTypen is not null)
            RueckblickTabelle.Lege(session, store, _rueckTypen, _rueckFelder!, _rueckSchluessel!, _rueckStreams!, _rueckVersionen!);
    }

    /// <summary>Die CommandIds der Quittungen (für die Ausnahme einer Wiederholung).</summary>
    public static Guid? QuittungIn(IReadOnlyList<Beilage>? beilagen)
        => beilagen?.OfType<Quittung>().Select(q => (Guid?)q.CommandId).FirstOrDefault();
}
