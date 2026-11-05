using System.Collections.Concurrent;
using Abstractions;
using Grpc.Core;
using Infrastructure.Akteure;
using Infrastructure.Extensions;
using Infrastructure.Funktionen;
using Infrastructure.Komposition;
using Infrastructure.Lebenszyklus;
using Infrastructure.Mapping;
using Infrastructure.Projections;
using Infrastructure.Serialization;
using Infrastructure.Verteilen;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Leitung = Cqrs.Leitung;

namespace Infrastructure.GrpcClient;

/// <summary>Was die Lese-Schleife der Verbindung weiterreicht: eine Nachricht des Clients.</summary>
public sealed record VonClient(Leitung.ClientMessage Nachricht);

/// <summary>Eine andere Sitzung leitet eine Query an diese weiter — dieser Client beantwortet sie (Zuständiger).</summary>

/// <summary>Eine fertige Server-Nachricht, die ein Hintergrund-Teil der Sitzung schreiben will (Arbeitsaufträge an Funktionen draußen).</summary>
public sealed record ServerNachricht(Leitung.ServerMessage Nachricht);

/// <summary>Was alle Sitzungen eines Knotens teilen.</summary>
public sealed record SitzungsDienste(
    ProtoMessageMapper Mapper,
    IAggregateDispatcher Dispatcher,
    IQueryDienst Queries,
    Verteilung? Verteilung,
    AkteurTor? Tor,
    Konsum Konsum,
    IEventStoreRepository? Log,
    ILogger Logger,
    Werkstatt? Werkstatt = null,
    Infrastructure.Funktionen.Verkettung? Koepfe = null,
    Infrastructure.Eingaenge.IOffeneEingaenge? Offene = null,
    Infrastructure.Funktionen.IFunktionsWege? Wege = null);

/// <summary>
/// Die Sitzung einer gRPC-Verbindung als Actor (docs/konzept-akteure.md §5): der <b>einzige Schreiber</b> auf den
/// Antwort-Stream. Die Lese-Schleife der Verbindung reicht jede Nachricht des Clients nur weiter (<see cref="VonClient"/>); Pushes kommen vom
/// Verteiler des Knotens (<see cref="Zustellung"/>, begrenzt über den <see cref="Rueckstau"/>); was wartet (Query, Antwort einer Funktion
/// draußen), läuft neben der Mailbox, geschrieben wird danach wieder im Actor.
/// Nach dem Handshake meldet sie ihre wirksame Liste (Vertrag ∩ Token) beim Verteiler an; gezielte Antworten (Ablehnung, <c>CommandFailed</c>)
/// erreichen sie über ihre Sitzungs-Id, gleich, was sie hört. Endet die Sitzung (Stream zu, Ablehnung am Handshake), beendet sie
/// <see cref="Ende"/>; den Status bekommt der Client vom <see cref="CqrsClientServiceImpl"/>.
/// </summary>
public sealed class SitzungsActor : RahmenActor, IArbeiter
{
    /// <summary>So lange darf ein Auftrag an einen Worker ohne Regung bleiben (das SDK meldet alle 10 s ein Lebenszeichen).</summary>
    public static readonly TimeSpan GeduldDraussen = TimeSpan.FromSeconds(60);

    /// <summary>So lange sammelt die Sitzung „P hat S weiterverarbeitet“, bevor sie dem Client die betroffenen Queries nennt.</summary>
    public static readonly TimeSpan Zusammenfassen = TimeSpan.FromMilliseconds(200);

    /// <summary>So viele Einträge liefert eine Seite des Logs höchstens.</summary>
    public const int LogSeiteHoechstens = 1_000;

    /// <summary>So viele Antworten beobachtet eine Sitzung höchstens (die ältesten fallen heraus).</summary>
    public const int HoechstensAntworten = 200;

    /// <summary>Zeigt eine Antwort mehr Ids, beobachtet die Sitzung ihre Projektion grob statt jeder Id (docs/13 §13.7).</summary>
    public const int HoechstensIds = 1_000;

    /// <summary>Was eine gehaltene Antwort zeigt: die Projektion, die antwortete, und die Streams ihrer Ids (leer = grob, die ganze Projektion).</summary>
    private sealed record Beobachtung(IReadOnlyList<string> Projektionen, IReadOnlySet<Guid> Streams);

    private readonly IServerStreamWriter<Leitung.ServerMessage> _stream;
    private readonly string _id;
    private readonly SitzungsDienste _d;
    private readonly AkteurSitzung _sitzung;
    private readonly TaskCompletionSource _ende;
    private readonly Rueckstau _rueckstau = new();
    private readonly ILogger _logger;
    private PID? _verteiler;
    private ActorSystem? _system;
    private PID? _selbst;
    // Beobachtung (docs/konzept-akteure.md §5, „P hat S weiterverarbeitet“): je gehaltener Antwort, was sie zeigt; die betroffenen Queries sammeln sich bis zur
    //   nächsten Aktualisierung an den Client.
    private readonly Dictionary<IQuery, Beobachtung> _beobachtungen = new();
    private readonly Queue<IQuery> _beobachtungsReihe = new();
    private readonly HashSet<IQuery> _veraendert = new();
    private bool _aktualisierungGeplant;
    private IReadOnlySet<Type> _hoert = new HashSet<Type>();

    public SitzungsActor(IServerStreamWriter<Leitung.ServerMessage> stream, string sitzungsId, AkteurRechte? akteur, SitzungsDienste dienste,
        TaskCompletionSource ende) : base(dienste.Logger)
    {
        _stream = stream;
        _id = sitzungsId;
        _d = dienste;
        _sitzung = new AkteurSitzung(akteur);
        _ende = ende;
        _logger = dienste.Logger;
    }

    /// <summary>Beendet, wenn die Sitzung von sich aus endet (Fehler = der Status für den Client).</summary>
    public Task Ende => _ende.Task;

    protected override Task BeginneAsync(IContext context)
    {
        _verteiler = PID.FromAddress(context.System.Address, VerteilerActor.Name);
        _system = context.System;
        _selbst = context.Self;
        MeldeAn(context, new HashSet<Type>());   // gezielte Antworten schon vor dem Handshake
        return Task.CompletedTask;
    }

    protected override Task BeimStoppenAsync(IContext context)
    {
        // Funktionen draußen: der Arbeiter ist fort, seine laufenden Aufträge scheitern sofort und werden anderswo erneut gerechnet.
        _d.Werkstatt?.Abmelden(this);
        return Task.CompletedTask;
    }

    protected override async Task BehandleAsync(IContext context)
    {
        switch (context.Message)
        {
            case VonClient v:
                try { await AusClientAsync(context, v.Nachricht); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Session} error processing message", _id);
                    await FehlerAsync("PROCESSING_ERROR", ex.Message, "");
                }
                break;

            case Zustellung z:
                foreach (var e in z.Envelopes)
                    await SchreibeAsync(new Leitung.ServerMessage { Event = new Leitung.EventNotification { Envelope = _d.Mapper.MapToDto(e) } });
                _rueckstau.Erledigt();
                break;

            case ServerNachricht n:
                await SchreibeAsync(n.Nachricht);
                break;

            case Verarbeitet v:
                _rueckstau.Erledigt();
                Weiterverarbeitet(context, v);
                break;
        }
    }

    // ═════════════════════════════════════════
    // BEOBACHTUNG: „P hat S weiterverarbeitet“ → der Client fragt neu
    // ═════════════════════════════════════════

    /// <summary>Merkt sich, was eine Antwort zeigt, und meldet dem Verteiler die neue Menge der beobachteten Streams und Projektionen.</summary>
    private void Beobachte(IContext context, IQuery query, QueryResponse<IQueryResponse> antwort)
    {
        if (antwort.Projektionen.Count == 0) return;
        var ids = antwort.Deps?.Select(d => d.Id).Where(g => g != Guid.Empty).ToHashSet() ?? new HashSet<Guid>();
        if (ids.Count > HoechstensIds) ids.Clear();
        if (!_beobachtungen.ContainsKey(query))
        {
            _beobachtungsReihe.Enqueue(query);
            while (_beobachtungsReihe.Count > HoechstensAntworten) _beobachtungen.Remove(_beobachtungsReihe.Dequeue());
        }
        _beobachtungen[query] = new Beobachtung(antwort.Projektionen, ids);
        context.Send(_verteiler!, new SitzungBeobachtet(_id,
            _beobachtungen.Values.SelectMany(b => b.Streams).ToHashSet(),
            _beobachtungen.Values.Where(b => b.Streams.Count == 0).SelectMany(b => b.Projektionen).ToHashSet(StringComparer.Ordinal)));
    }

    /// <summary>Sammelt die betroffenen Queries und nennt sie dem Client nach <see cref="Zusammenfassen"/> in einer Nachricht.</summary>
    private void Weiterverarbeitet(IContext context, Verarbeitet v)
    {
        foreach (var (query, b) in _beobachtungen)
            if (b.Streams.Count == 0 ? b.Projektionen.Contains(v.Projektion) : v.Streams.Any(b.Streams.Contains))
                _veraendert.Add(query);
        if (_veraendert.Count == 0 || _aktualisierungGeplant) return;
        _aktualisierungGeplant = true;
        Neben(context, ct => Task.Delay(Zusammenfassen, ct), async _ =>
        {
            _aktualisierungGeplant = false;
            var nachricht = new Leitung.Aktualisiert();
            nachricht.Queries.AddRange(_veraendert.Select(q => _d.Mapper.MapToDto(q)));
            _veraendert.Clear();
            await SchreibeAsync(new Leitung.ServerMessage { Aktualisiert = nachricht });
        });
    }

    private async Task AusClientAsync(IContext context, Leitung.ClientMessage m)
    {
        switch (m.MessageCase)
        {
            case Leitung.ClientMessage.MessageOneofCase.Capabilities:
                await HandshakeAsync(context, m.Capabilities);
                break;
            case Leitung.ClientMessage.MessageOneofCase.Command:
                // Ein Command ohne Ziel geht an den Kopf eines Graphen bzw. an einen Akteur (Arbeit); einer an ein Aggregat an sein Aggregat.
                if (m.Command.Envelope?.Payload is { } nutzlast && _d.Mapper.CommandOhneZiel(nutzlast) is { } ohneZiel)
                    await AnKopfAsync(context, m.Command.Envelope, ohneZiel);
                else await CommandAsync(m.Command);
                break;
            case Leitung.ClientMessage.MessageOneofCase.Query:
                await QueryAsync(context, m.Query);
                break;
            case Leitung.ClientMessage.MessageOneofCase.TransientEvent:
                await HinweisAsync(m.TransientEvent);
                break;
            case Leitung.ClientMessage.MessageOneofCase.LiesLog:
                await LiesLogAsync(context, m.LiesLog);
                break;
            case Leitung.ClientMessage.MessageOneofCase.ArbeitAnnehmen:
                await ArbeitAnnehmenAsync(m.ArbeitAnnehmen);
                break;
            case Leitung.ClientMessage.MessageOneofCase.ArbeitsErgebnis:
                await ArbeitsErgebnisAsync(m.ArbeitsErgebnis);
                break;
            case Leitung.ClientMessage.MessageOneofCase.ArbeitLebt:
                if (Guid.TryParse(m.ArbeitLebt.Vorgang, out var lebt)) _d.Werkstatt?.Regt(this, lebt);
                break;
            default:
                _logger.LogWarning("{Session} unknown message type: {MessageCase}", _id, m.MessageCase);
                break;
        }
    }

    // ═════════════════════════════════════════
    // HANDSHAKE
    // ═════════════════════════════════════════

    private async Task HandshakeAsync(IContext context, Leitung.CapabilitiesRequest request)
    {
        // Client-Vertrag (docs/konzept-akteure.md §5.2): „ich bin Client X" — geprüft gegen die generierte Tabelle (wirksam ist Vertrag ∩ Token;
        //   abweichender Hash = Warnung bzw. im strengen Modus Ablehnung). Ohne Tor sagt der Vertrag, wer da ist.
        var vp = ClientVertragsPruefung.Pruefe(request.Vertrag, request.VertragHash, _sitzung.Akteur, Anwendung.Akteure.Rechte,
            _d.Tor?.VertragStreng ?? false, Anwendung.Akteure.ClientVertraege);
        if (vp.Ablehnung is { } ablehnung)
        {
            _logger.LogWarning("{Session} Vertrag {Vertrag} abgelehnt: {Grund}", _id, request.Vertrag, ablehnung);
            await FehlerAsync("VERTRAG_ABGELEHNT", ablehnung, "");
            _ende.TrySetException(new RpcException(new Status(StatusCode.PermissionDenied, ablehnung)));
            return;
        }
        if (vp.Warnung is { } warnung)
            _logger.LogWarning("{Session} Vertrag {Vertrag}: {Warnung}", _id, request.Vertrag, warnung);
        if (vp.Vertrag is { } vertrag)
        {
            _sitzung.Vertrag = vertrag;
            _sitzung.Akteur = vertrag;   // die Schnittmenge Vertrag ∩ Token IST die Befugnis der Sitzung (docs/konzept-akteure.md §4.3)
            _logger.LogInformation("{Session} Client {Vertrag} ({Typ}) angenommen — verkörpert {Akteure}", _id, vertrag.Name, vertrag.VertragTyp?.Name,
                string.Join(", ", vertrag.Teile.Select(t => t.Name)));
        }
        var akteur = _sitzung.Akteur;

        // Befugnis statt Selbstauskunft: erlaubte Mengen aus IDarf (∪ Vertrags-Ausgaben), Hören abgeleitet. Mit Vertrag hört die
        //   Sitzung genau, was er hört.
        var result = Handshake.Ermittle(request, _id);
        Func<string, Type?> loese = n => MessageTypeMapping.Resolve(n).Type;
        if (_sitzung.Vertrag is { } v)
        {
            var abweichung = ClientVertragsPruefung.Wende(result, v, loese);
            if (abweichung.Count > 0)
                _logger.LogInformation("{Session} Vertrag {Vertrag} statt Selbstauskunft: [{Abweichung}]", _id, v.Name, string.Join(", ", abweichung));
        }
        else if (akteur != null)
        {
            var verweigert = AkteurTor.Wende(result, akteur, loese);
            if (verweigert.Count > 0)
                _logger.LogWarning("{Session} Akteur {Akteur} verweigert: [{Verweigert}]", _id, akteur.Name, string.Join(", ", verweigert));
        }

        // Die wirksame Liste beim Verteiler anmelden (ersetzt die vorige).
        MeldeAn(context, result.SubscribedEvents.Select(n => MessageTypeMapping.Resolve(n))
            .Where(r => r.Type is not null && r.Category == MessageCategory.Event).Select(r => r.Type!).ToHashSet());

        if (result.UnknownTypes.Count > 0)
            _logger.LogWarning("{Session} unknown types: [{Types}]", _id, string.Join(", ", result.UnknownTypes));

        var antwort = Handshake.Antwort(result);
        if (_sitzung.Vertrag is { } angenommen)
        {
            antwort.Vertrag = angenommen.Name;
            antwort.VertragHash = angenommen.VertragHash;
        }
        await SchreibeAsync(new Leitung.ServerMessage { CapabilitiesResponse = antwort });
        _logger.LogInformation("{Session} → CapabilitiesResponse: {Commands} commands, {Events} events, {Queries} queries",
            _id, result.AllowedCommands.Count, result.SubscribedEvents.Count, result.AllowedQueries.Count);
    }

    private void MeldeAn(IContext context, IReadOnlySet<Type> hoert)
    {
        _hoert = hoert;
        context.Send(_verteiler!, new SitzungMeldetAn(_id, context.Self, hoert, _rueckstau));
    }

    // ═════════════════════════════════════════
    // COMMAND
    // ═════════════════════════════════════════

    private async Task CommandAsync(Leitung.CommandRequest request)
    {
        var akteur = _sitzung.Akteur;
        CommandEnvelope envelope;
        try { envelope = _d.Mapper.MapToDomain(request.Envelope) with { OriginSessionId = _id }; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Session} command mapping failed", _id);
            await FehlerAsync("COMMAND_MAPPING_FAILED", ex.Message, request.Envelope?.CorrelationId ?? "");
            return;
        }

        // Tor aus (opt-in): niemand ist angemeldet — die Herkunft folgt dann dem Modell: darf genau EIN Akteur den Typ, kommt er von ihm (sonst
        //   bliebe die ganze Kette dahinter ohne Akteur). Mit Tor stempelt unten das Token.
        if (akteur == null && !ImAuftrag.IstAkteur(envelope.UserId) && AkteurHerkunft.EindeutigerHalter(envelope.Payload.GetType()) is { } halter)
            envelope = envelope with { UserId = halter };

        // Routing über Typen (Invariante 3): der AggregateType ist serverseitig aus dem Command-Typ ableitbar (generierte Tabelle); fehlt er
        //   (ein Worker setzt ihn nicht), hier auflösen — sonst entstünde eine ClusterIdentity ohne Kind, und der Command wirkte nie.
        if (string.IsNullOrWhiteSpace(envelope.AggregateType))
            envelope = envelope with { AggregateType = AggregateDispatcherExtensions.ResolveAggregateType(envelope.Payload) };

        if (akteur != null)
        {
            // Wer hineingibt, steht im Envelope — vom Tor gestempelt, nicht vom Client behauptet.
            envelope = envelope with { UserId = akteur.AkteurFuer(envelope.Payload.GetType()) };
            if (!akteur.DarfHinein(envelope.Payload.GetType()))
            {
                _logger.LogWarning("{Session} Akteur {Akteur} darf {Command} nicht", _id, akteur.Name, envelope.Payload.GetType().Name);
                if (AkteurVerweigerung.Baue(envelope, akteur.Name) is { } verweigert) _d.Verteilung?.Melde(verweigert);
                return;
            }
        }

        // Ohne Versionsprüfung (negativ): ein Client, der den Stand des Ziels nicht kennt und keine OCC will (ein Worker, der von sich aus sendet),
        //   schickt den Command im Modus Emittiert — die Inbox des Ziels dedupliziert über die Command-Id. Positive Versionen bleiben OCC.
        if (request.Envelope.ExpectedVersion < 0)
            envelope = envelope with { Modus = new CommandModus.Emittiert() };

        // Spur: von außen schreibt der Akteur (Wirker akteur:<A>); ohne Akteur bleibt der Wirker leer.
        if (ImAuftrag.IstAkteur(envelope.UserId))
            envelope = envelope with { Wirker = WirkerId.FuerAkteur(envelope.UserId) };

        _logger.LogDebug("{Session} Command {Command} → {AggregateType} ({Modus}), CorrelationId {CorrelationId}",
            _id, envelope.Payload.GetType().Name, envelope.AggregateType, envelope.Modus.GetType().Name, envelope.CorrelationId);
        _d.Dispatcher.Dispatch(envelope);
    }

    /// <summary>
    /// Ein Command ohne Ziel von einem Akteur (docs/04 §4.4): ein offener Eingang je Empfänger (der Kopf eines Graphen bzw. ein Akteur, der ihn als
    /// Arbeit hört; Kennung aus der Command-Id — wer wiederholt, schickt dieselbe), danach der schnelle Weg und die Quittung an den Client. Genau
    /// einmal am Empfänger.
    /// </summary>
    private async Task AnKopfAsync(IContext context, Leitung.CommandEnvelopeDto dto, ICommand command)
    {
        var typ = command.GetType();
        var akteur = _sitzung.Akteur;
        if (akteur != null && !akteur.DarfHinein(typ))
        {
            await FehlerAsync("AKTEUR_DARF_NICHT", $"Akteur '{akteur.Name}' darf {typ.Name} nicht (kein IDarf<{typ.Name}>)", dto.CorrelationId);
            return;
        }
        if (_d.Koepfe is not { } koepfe || _d.Offene is not { } offene)
        {
            await FehlerAsync("KEIN_EMPFAENGER", $"{typ.Name}: dieser Host nimmt keine Commands ohne Ziel an", dto.CorrelationId);
            return;
        }
        var kennung = Guid.TryParse(dto.CommandId, out var id) ? id : Guid.NewGuid();
        var wer = akteur?.AkteurFuer(typ) ?? AkteurHerkunft.EindeutigerHalter(typ);
        var korrelation = string.IsNullOrEmpty(dto.CorrelationId) ? kennung.ToString() : dto.CorrelationId;
        var eingaenge = Infrastructure.Eingaenge.Folgenbeilagen.VonAkteur(command, koepfe, kennung, wer, korrelation);
        if (eingaenge.Count == 0)
        {
            await FehlerAsync("KEIN_EMPFAENGER", $"{typ.Name}: kein Graph nimmt ihn am Kopf, kein Akteur hört ihn (fehlt eine Instanz?)", korrelation);
            return;
        }
        Neben(context, ct => offene.LegeAsync(eingaenge.Select(e => e.Eingang).ToList(), kennung, ct), async t =>
        {
            if (t.IsFaulted)
            {
                await FehlerAsync("EINGANG_NICHT_GELEGT", t.Exception?.GetBaseException().Message ?? "", korrelation);
                return;
            }
            if (_d.Wege is { } wege) Infrastructure.Eingaenge.Folgenbeilagen.StelleZu(eingaenge, wege, kennung);
            // Gelegt heißt angenommen: von jetzt an bringt der Zusteller ihn hin. Die Quittung mit seiner Korrelation sagt es dem Client — wer
            //   ohne sie wiederholt (dieselbe Command-Id), legt keinen zweiten.
            await SchreibeAsync(new Leitung.ServerMessage { Event = new Leitung.EventNotification { Envelope = _d.Mapper.MapToDto(new EventEnvelope
            {
                AggregateId = kennung, AggregateType = "Eingang", CorrelationId = korrelation, CausationId = kennung.ToString(), UserId = wer,
                Payload = new CommandQuittung(typ.Name, kennung, 0, Geschrieben: true),
            }) } });
        });
    }

    // ═════════════════════════════════════════
    // QUERY
    // ═════════════════════════════════════════

    private async Task QueryAsync(IContext context, Leitung.QueryRequest request)
    {
        IQuery query;
        try { query = _d.Mapper.QueryAus(request.Payload); }
        catch (Exception ex)
        {
            await FehlerAsync("QUERY_FAILED", ex.Message, request.CorrelationId);
            return;
        }
        var akteur = _sitzung.Akteur;
        var name = query.GetType().Name;
        if (!AkteurTor.Darf(akteur, query.GetType()))
        {
            await FehlerAsync("AKTEUR_DARF_NICHT", $"Akteur '{akteur!.Name}' darf {name} nicht", request.CorrelationId);
            return;
        }

        // Die Leseseite, mit Read-your-writes: „mindestens S auf v“ je Stream, den der Client zuletzt beschrieben hat.
        var mindestens = request.Mindestens.Where(m => Guid.TryParse(m.Stream, out _))
            .Select(m => new StreamStand(Guid.Parse(m.Stream), m.Version)).ToList();
        Neben(context, _ => _d.Queries.ExecuteAsync(query, mindestens), async t =>
        {
            if (t.IsFaulted && t.Exception!.InnerException is NotSupportedException ns)
            {
                _logger.LogWarning("{Session} query not supported: {Message}", _id, ns.Message);
                await FehlerAsync("QUERY_NOT_SUPPORTED", ns.Message, request.CorrelationId);
                return;
            }
            if (!t.IsCompletedSuccessfully)
            {
                var grund = t.Exception?.InnerException?.Message ?? "abgebrochen";
                _logger.LogWarning(t.Exception, "{Session} query failed", _id);
                await FehlerAsync("QUERY_FAILED", grund, request.CorrelationId);
                return;
            }
            var antwort = await (Task<QueryResponse<IQueryResponse>>)t;
            await SchreibeAsync(new Leitung.ServerMessage { QueryResponse = _d.Mapper.ToQueryResponse(antwort, request.CorrelationId) });
            Beobachte(context, query, antwort);
        });
    }

    // ═════════════════════════════════════════
    // LESEN AB POSITION
    // ═════════════════════════════════════════

    private async Task LiesLogAsync(IContext context, Leitung.LiesLog request)
    {
        if (_d.Log is not { } log)
        {
            await FehlerAsync("LOG_NICHT_VERFUEGBAR", "Dieser Host liest kein Log", request.Korrelation);
            return;
        }
        // Nur, was im Log steht und die Sitzung hören darf (Hinweise stehen nie im Log); ohne solche Typen ist die Seite leer.
        var typen = _hoert.Where(Anwendung.Typen.PersistableEventTypes.Contains).ToList();
        var diskriminatoren = typen.Count == 0 ? null : BatchLeser.TypenAus(typen);
        var ab = Math.Max(0, request.Ab);
        if (diskriminatoren is null)
        {
            await SchreibeAsync(new Leitung.ServerMessage { LogSeite = new Leitung.LogSeite { Korrelation = request.Korrelation, Bis = ab } });
            return;
        }
        var hoechstens = request.Hoechstens is > 0 and <= LogSeiteHoechstens ? request.Hoechstens : LogSeiteHoechstens;
        Neben(context, ct => log.ReadPartitionAsync(ab, 0, 1, diskriminatoren, Array.Empty<long>(), hoechstens, ct), async t =>
        {
            if (!t.IsCompletedSuccessfully)
            {
                await FehlerAsync("LOG_NICHT_LESBAR", t.Exception?.InnerException?.Message ?? "abgebrochen", request.Korrelation);
                return;
            }
            var seite = await (Task<PartitionsSeite>)t;
            await SchreibeAsync(new Leitung.ServerMessage { LogSeite = LogSeite(request.Korrelation, ab, seite) });
        });
    }

    /// <summary>
    /// Die Seite für den Client: nur Einträge bis zum Horizont (dahinter könnte noch ein früheres Event sichtbar werden); ist sie voll, endet sie
    /// vor der letzten Position (ein Split-Upcast liefert mehrere Events mit derselben), sonst reicht „bis“ bis zum Horizont.
    /// </summary>
    private Leitung.LogSeite LogSeite(string korrelation, long ab, PartitionsSeite seite)
    {
        var bisHorizont = seite.Events.Where(e => e.Sequenz <= seite.Gesetzt).ToList();
        var voll = seite.Voll && bisHorizont.Count == seite.Events.Count;
        long bis;
        if (voll && bisHorizont.Select(e => e.Sequenz).Distinct().Count() > 1)
        {
            var letzte = bisHorizont[^1].Sequenz;
            bisHorizont = bisHorizont.Where(e => e.Sequenz < letzte).ToList();
            bis = bisHorizont[^1].Sequenz;
        }
        else bis = voll ? bisHorizont[^1].Sequenz : Math.Max(ab, seite.Gesetzt);
        var antwort = new Leitung.LogSeite { Korrelation = korrelation, Bis = bis, Voll = voll };
        antwort.Eintraege.AddRange(bisHorizont.Select(e => new Leitung.LogEintrag { Position = e.Sequenz, Envelope = _d.Mapper.MapToDto(e.Event) }));
        return antwort;
    }

    // ═════════════════════════════════════════
    // HINWEIS VOM CLIENT
    // ═════════════════════════════════════════

    private async Task HinweisAsync(Leitung.TransientEventRequest request)
    {
        EventEnvelope envelope;
        try { envelope = _d.Mapper.MapToDomain(request.Envelope); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Session} TransientEvent failed", _id);
            await FehlerAsync("TRANSIENT_EVENT_FAILED", ex.Message, "");
            return;
        }
        if (envelope.Payload is not ITransientEvent)
        {
            _logger.LogWarning("{Session} rejected: {Payload} is not ITransientEvent", _id, envelope.Payload.GetType().Name);
            await FehlerAsync("INVALID_TRANSIENT_EVENT", $"{envelope.Payload.GetType().Name} does not implement ITransientEvent", "");
            return;
        }
        var akteur = _sitzung.Akteur;
        if (!AkteurTor.Darf(akteur, envelope.Payload.GetType()))
        {
            await FehlerAsync("AKTEUR_DARF_NICHT", $"Akteur '{akteur!.Name}' darf {envelope.Payload.GetType().Name} nicht", "");
            return;
        }
        if (akteur != null)
            envelope = envelope with { UserId = akteur.AkteurFuer(envelope.Payload.GetType()) };
        else if (!ImAuftrag.IstAkteur(envelope.UserId) && AkteurHerkunft.EindeutigerHalter(envelope.Payload.GetType()) is { } halter)
            envelope = envelope with { UserId = halter };   // Tor aus: Herkunft laut Modell (der eine Akteur mit IDarf)

        // Nur an die Akteure, die ihn hören (über die Verteiler an ihre Sitzungen); Funktionen hören keine Hinweise.
        _d.Verteilung?.Melde(envelope with { ZielSitzung = null });
        _logger.LogDebug("{Session} TransientEvent published: {Payload}", _id, envelope.Payload.GetType().Name);
    }

    // ═════════════════════════════════════════
    // ARBEIT FÜR AKTEURE (IHoert eines Commands ohne Ziel an einem verkörperten Akteur)
    // ═════════════════════════════════════════

    string IArbeiter.Name => _id;

    TimeSpan? IArbeiter.Geduld => GeduldDraussen;

    /// <summary>Ein Auftrag der Werkstatt an diesen Worker: als Server-Nachricht an den Actor (der einzige Schreiber am Stream).</summary>
    void IArbeiter.Sende(AuftragDraussen a)
    {
        if (_system is null || _selbst is null) return;
        var nutzlast = _d.Mapper.Nachricht(a.Eingang);
        _system.Root.Send(_selbst, new ServerNachricht(new Leitung.ServerMessage
        {
            ArbeitsAuftrag = new Leitung.ArbeitsAuftrag
            {
                Vorgang = a.Vorgang.ToString(),
                Korrelation = a.Korrelation ?? "",
                Fuer = a.Funktion.Name,
                Eingang = nutzlast,
                Akteur = a.Herkunft.Akteur ?? "",
                Stream = a.Herkunft.Stream.ToString(),
                Version = a.Herkunft.Version,
                ZeitUtc = a.Herkunft.Zeit.ToUnixTimeMilliseconds(),
            }
        }));
    }

    /// <summary>
    /// Der Client nimmt die Arbeit von Akteuren an (je Akteur mit seinen Plätzen): die Sitzung wird ein Arbeiter in der Werkstatt dieses Knotens — nur
    /// für Akteure, die sie verkörpert (verkörpert ∩ Token) und die Arbeit hören; ohne Tor/Akteur offen.
    /// </summary>
    private async Task ArbeitAnnehmenAsync(Leitung.ArbeitAnnehmen request)
    {
        if (_d.Werkstatt is not { } werkstatt)
        {
            await FehlerAsync("ARBEIT_NICHT_VERFUEGBAR", "Dieser Host hat keine Arbeit für Akteure", "");
            return;
        }
        var akteur = _sitzung.Akteur;
        var unbekannt = new List<string>();
        var nichtBefugt = new List<string>();
        foreach (var a in request.Angebote)
        {
            if (!Anwendung.Akteure.Rechte.TryGetValue(a.Akteur, out var rechte) || rechte.Arbeit.Count == 0) { unbekannt.Add(a.Akteur); continue; }
            if (akteur is not null && akteur.Einzelne.All(x => x.Name != a.Akteur)) { nichtBefugt.Add(a.Akteur); continue; }
            werkstatt.Anmelden(this, a.Akteur, a.Plaetze);
        }
        if (nichtBefugt.Count > 0)
            await FehlerAsync("ARBEIT_NICHT_BEFUGT",
                $"{akteur!.Name} verkörpert nicht: {string.Join(", ", nichtBefugt)} (die Sitzung verkörpert nur die Akteure ihres Clients ∩ Token)", "");
        if (unbekannt.Count > 0)
        {
            _logger.LogWarning("{Session} nimmt Arbeit unbekannter Akteure an: [{Unbekannt}]", _id, string.Join(", ", unbekannt));
            await FehlerAsync("ARBEIT_UNBEKANNT_AKTEUR",
                $"Kein Akteur mit Arbeit (IHoert eines Commands ohne Ziel) dieses Namens: {string.Join(", ", unbekannt)}", "");
        }
    }

    /// <summary>Die Antwort des Workers auf einen Auftrag (ganz oder ein Stück eines Stroms, oder ein Fehler) an die Werkstatt.</summary>
    private async Task ArbeitsErgebnisAsync(Leitung.ArbeitsErgebnis request)
    {
        if (_d.Werkstatt is not { } werkstatt || !Guid.TryParse(request.Vorgang, out var auftrag))
        {
            await FehlerAsync("ARBEITS_ERGEBNIS_UNGUELTIG", $"Antwort ohne gültigen Auftrag oder ohne Arbeit auf diesem Host: '{request.Vorgang}'", request.Vorgang);
            return;
        }
        var fehler = string.IsNullOrEmpty(request.Fehler) ? null : request.Fehler;
        List<object>? ausgaben = null;
        if (fehler is null)
        {
            try { ausgaben = request.Commands.Select(_d.Mapper.Antwortcommand).ToList(); }
            catch (Exception ex) { fehler = $"Antwort nicht lesbar: {ex.Message}"; }
        }
        var ausgang = werkstatt.Ergebnis(this, auftrag, ausgaben, request.Ab, request.Fertig, fehler);
        if (ausgang == Werkstattausgang.Abgelehnt && string.IsNullOrEmpty(request.Fehler))
            await FehlerAsync("ARBEITS_ERGEBNIS_ABGELEHNT", $"Antwort auf {auftrag} außerhalb des IDarf, außer der Reihe oder nicht lesbar — der Auftrag wird wiederholt",
                request.Vorgang);
        else if (ausgang == Werkstattausgang.Unbekannt)
            await FehlerAsync("ARBEIT_UNBEKANNT", $"Eingang {auftrag} läuft auf diesem Knoten nicht (schon beantwortet, abgebrochen oder nie vergeben)", request.Vorgang);
    }

    // ═════════════════════════════════════════
    // SCHREIBEN (nur hier, nur im Actor)
    // ═════════════════════════════════════════

    private Task FehlerAsync(string code, string text, string korrelation) =>
        SchreibeAsync(new Leitung.ServerMessage { Error = new Leitung.ErrorResponse { Code = code, Message = text, CorrelationId = korrelation } });

    private async Task SchreibeAsync(Leitung.ServerMessage nachricht)
    {
        try
        {
            await _stream.WriteAsync(nachricht);
        }
        catch (Exception ex) when (ex is RpcException or InvalidOperationException or OperationCanceledException)
        {
            _logger.LogDebug("{Session} Stream zu — {Case} nicht geschrieben", _id, nachricht.MessageCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Session} Fehler beim Schreiben von {Case}", _id, nachricht.MessageCase);
        }
    }
}
