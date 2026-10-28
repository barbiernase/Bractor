using System.Buffers;
using System.Text;
using System.Text.Json;
using Abstractions;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// FESTHALTEN DER AUSGABE (Plan Pub/Sub-Neustrukturierung §4.2, R12, A1): eine Funktion hält ihre Ausgabe im Log fest, BEVOR sie wirkt — unter
// dem eindeutigen Schlüssel „Funktion F, Eingang X“: ein eigener Stream je (F, X), Id abgeleitet, angehängt mit Versionsprüfung. Wer zuerst
// festhält, gewinnt; ein zweiter Lauf (zweite Aktivierung, zweiter Worker, Wiederholung nach Absturz) findet den Eintrag und sendet aus ihm.
// Was im Eintrag steht: die Events der Ausgabe selbst (sie sind Fakten im Log), je Command ein BefehlFestgehalten (der Command als generiertes
// Wire-JSON, ohne Reflection) und am Ende AusgabeFestgehalten (der Lauf ist vollständig, auch wenn er nichts ausgab). Hinweise stehen nicht
// darin (höchstens einmal, nur der gewinnende Lauf schickt sie). Die Version im Stream ist der Fortschritt: ein Strom hält stückweise fest und
// setzt nach einem Absturz bei der nächsten Ausgabe fort.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Ein festgehaltener Command einer Funktion (Framework-Event im Stream „Funktion F, Eingang X“): Diskriminator und Wire-JSON.</summary>
public sealed record BefehlFestgehalten(string Typ, string Daten) : IEvent;


/// <summary>Das Ende eines festgehaltenen Laufs: so viele Ausgaben (ohne Hinweise) gehören zu ihm, mehr kommen nicht.</summary>
public sealed record AusgabeFestgehalten(int Anzahl) : IEvent;


/// <summary>Was unter „Funktion F, Eingang X“ festgehalten ist: die Ausgaben in Reihenfolge (Commands und Events) und ob der Lauf vollständig ist.</summary>
public sealed record Festgehalten(IReadOnlyList<IAusgabe> Ausgaben, bool Abgeschlossen)
{
    public static readonly Festgehalten Nichts = new(Array.Empty<IAusgabe>(), false);
}

/// <summary>Der Eingang einer Funktion: Funktion, Stream und Version des Eingangs (der Schlüssel des Festhaltens), dazu Korrelation und Akteur.</summary>
public sealed record FunktionsEingabe(string Funktion, Guid Stream, int Version, string? Korrelation, string? Akteur)
{
    /// <summary>Der Stream „Funktion F, Eingang X“.</summary>
    public Guid Schluessel => FunktionsIds.Festhalten(Funktion, Stream, Version);
}

/// <summary>Die abgeleiteten Ids einer Funktion — dieselbe Ausgabe ergibt beim Wiederholen dieselbe Id.</summary>
public static class FunktionsIds
{
    /// <summary>Der Stream, unter dem die Ausgabe der Funktion zum Eingang (Stream, Version) festgehalten wird.</summary>
    public static Guid Festhalten(string funktion, Guid stream, int version) => Herkunft.Ableite($"festgehalten:{funktion}:{stream:N}:{version}");

    /// <summary>
    /// Die Command-Id der Ausgabe an <paramref name="stelle"/> (Plan §4.2: Funktion, Eingang, Stelle, Typ, Ziel): ein erneutes Senden aus dem
    /// Festgehaltenen trägt dieselbe Id, die Inbox des Ziels verwirft es.
    /// </summary>
    public static Guid Befehl(string funktion, Guid stream, int version, int stelle, ICommand befehl) =>
        Herkunft.Ableite($"funktionsbefehl:{funktion}:{stream:N}:{version}:{stelle}:{befehl.GetType().FullName}:{befehl.Ziel:N}");

    // ── Hinweis-Netz (Plan §2.9, A7): die Kennung eines Hinweises vergibt der Absender, abgeleitet — dasselbe Nachrechnen ergibt dieselbe Kennung ──

    /// <summary>Die Kennung des Hinweises an <paramref name="stelle"/> der Ausgabe einer Funktion zum Log-Eingang (Stream, Version).</summary>
    public static Guid Hinweis(string funktion, Guid stream, int version, int stelle) =>
        Herkunft.Ableite($"hinweis:{funktion}:{stream:N}:{version}:{stelle}");

    /// <summary>Die Kennung des Hinweises an <paramref name="stelle"/> der Ausgabe einer Funktion zum Hinweis-Eingang mit der Kennung <paramref name="eingang"/>.</summary>
    public static Guid Hinweis(string funktion, Guid eingang, int stelle) => Herkunft.Ableite($"hinweis:{funktion}:hinweis:{eingang:N}:{stelle}");

    /// <summary>Die Kennung des Hinweises, den ein Aggregat auf den Command <paramref name="commandId"/> hin ausgab (eine Ablehnung).</summary>
    public static Guid HinweisAusCommand(Guid commandId) => Herkunft.Ableite($"hinweis:command:{commandId:N}");

    /// <summary>
    /// Die Command-Id der Ausgabe an <paramref name="stelle"/> zum Hinweis-Eingang mit der Kennung <paramref name="hinweis"/> (Plan §4.2: Funktion,
    /// Hinweis-Kennung, Stelle, Typ, Ziel) — ein Wiederholen des Emitters trägt dieselbe Id, die Inbox des Ziels verwirft es.
    /// </summary>
    public static Guid BefehlAusHinweis(string funktion, Guid hinweis, int stelle, ICommand befehl) =>
        Herkunft.Ableite($"funktionsbefehl:{funktion}:hinweis:{hinweis:N}:{stelle}:{befehl.GetType().FullName}:{befehl.Ziel:N}");

    /// <summary>Der Schlüssel eines Hinweis-Eingangs draußen (Vermittler, Sitzung): je Funktion und Kennung.</summary>
    public static Guid HinweisEingang(string funktion, Guid hinweis) => Herkunft.Ableite($"hinweiseingang:{funktion}:{hinweis:N}");
}

/// <summary>
/// Die Ablage des Festgehaltenen (Naht: live das Log über <see cref="LogAusgabeAblage"/>; die Garantie „wer zuerst festhält, gewinnt“ ist
/// Store-Semantik und wird gegen echtes Marten geprüft).
/// </summary>
public interface IAusgabeAblage
{
    Task<Festgehalten> LiesAsync(FunktionsEingabe eingabe, CancellationToken ct);

    /// <summary>
    /// Hält <paramref name="ausgaben"/> (Commands und Events, keine Hinweise) ab der Stelle <paramref name="ab"/> fest; mit
    /// <paramref name="abgeschlossen"/> ist der Lauf danach vollständig. Liefert false, wenn an dieser Stelle schon etwas steht (ein anderer Lauf
    /// hat dort festgehalten) — dann gilt dessen Eintrag.
    /// </summary>
    Task<bool> HalteFestAsync(FunktionsEingabe eingabe, int ab, IReadOnlyList<IAusgabe> ausgaben, bool abgeschlossen, CancellationToken ct);
}

/// <summary>
/// Das Festhalten im Log: je (F, X) ein Stream mit abgeleiteter Id, angehängt mit erwarteter Version = Zahl der schon festgehaltenen Einträge.
/// Zwei Läufe an derselben Stelle: Marten lässt nur einen gelten (Versionsprüfung, bei Version 0 der Schlüssel des Streams). Danach meldet sie
/// den Commit wie ein Aggregat (<see cref="PubSub.Commitmeldung"/>): eine Funktion, die ein ausgegebenes Event hört, sieht es sofort.
/// </summary>
public sealed class LogAusgabeAblage(IEventStoreRepository store, PubSub.BrokerPublisher? publisher = null) : IAusgabeAblage
{
    public async Task<Festgehalten> LiesAsync(FunktionsEingabe eingabe, CancellationToken ct)
    {
        var events = await store.ReadStreamAsync(eingabe.Schluessel, 0, ct);
        if (events.Count == 0) return Festgehalten.Nichts;
        var ausgaben = new List<IAusgabe>(events.Count);
        var fertig = false;
        foreach (var e in events.OrderBy(e => e.AggregateVersion).ThenBy(e => e.SubIndex))
        {
            switch (e.Payload)
            {
                case AusgabeFestgehalten: fertig = true; break;
                case BefehlFestgehalten b: ausgaben.Add(Befehlsform.Lies(b)); break;
                default: ausgaben.Add(e.Payload); break;
            }
        }
        return new Festgehalten(ausgaben, fertig);
    }

    public async Task<bool> HalteFestAsync(FunktionsEingabe eingabe, int ab, IReadOnlyList<IAusgabe> ausgaben, bool abgeschlossen,
        CancellationToken ct)
    {
        var events = new List<IEvent>(ausgaben.Count + 1);
        foreach (var a in ausgaben)
            events.Add(a switch
            {
                ICommand c => Befehlsform.Schreibe(c),
                ITransientEvent h => throw new InvalidOperationException($"Ein Hinweis ({h.GetType().Name}) wird nicht festgehalten."),
                IEvent e => e,
                _ => throw new InvalidOperationException($"{a.GetType().Name} ist weder Command noch Event."),
            });
        if (abgeschlossen) events.Add(new AusgabeFestgehalten(ab + ausgaben.Count));
        if (events.Count == 0) return true;
        try
        {
            await store.AppendEventsAsync(eingabe.Schluessel, ab, events, correlationId: eingabe.Korrelation,
                causationId: Spur.Ursache(eingabe.Stream, eingabe.Version), aggregateType: "Funktion:" + eingabe.Funktion,
                akteur: ImAuftrag.IstAkteur(eingabe.Akteur) ? eingabe.Akteur : null);
            if (publisher is not null && events.Any(e => e is not BefehlFestgehalten and not AusgabeFestgehalten))
                PubSub.Commitmeldung.Melde(publisher, Aggregate.EventEnvelopeFactory.BuildPerEvent(events, eingabe.Schluessel, "Funktion:" + eingabe.Funktion,
                        ab, eingabe.Korrelation ?? eingabe.Schluessel.ToString(), Spur.Ursache(eingabe.Stream, eingabe.Version),
                        ImAuftrag.IstAkteur(eingabe.Akteur) ? eingabe.Akteur! : ImAuftrag.Ohne, ab + events.Count, Spur.Aktuell)
                    .Where(e => e.Payload is not BefehlFestgehalten and not AusgabeFestgehalten).ToList());
            return true;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Ein anderer Lauf hat an dieser Stelle festgehalten (Versionskonflikt) — oder das Schreiben scheiterte. Was gilt, sagt das Log.
            var jetzt = await store.ReadStreamAsync(eingabe.Schluessel, 0, ct);
            if (jetzt.Count > ab) return false;
            throw;
        }
    }
}

/// <summary>Ein Command als festgehaltenes Event: Diskriminator und das generierte Wire-JSON (dieselbe Form wie im Command-Envelope).</summary>
public static class Befehlsform
{
    public static BefehlFestgehalten Schreibe(ICommand befehl)
    {
        var puffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(puffer)) Anwendung.WirePoly.WriteCommand(w, befehl);
        return new BefehlFestgehalten(Anwendung.WirePoly.CommandDiskriminator(befehl), Encoding.UTF8.GetString(puffer.WrittenSpan));
    }

    public static ICommand Lies(BefehlFestgehalten b)
    {
        var leser = new Utf8JsonReader(Encoding.UTF8.GetBytes(b.Daten));
        leser.Read();
        return Anwendung.WirePoly.ReadCommand(b.Typ, ref leser);
    }
}
