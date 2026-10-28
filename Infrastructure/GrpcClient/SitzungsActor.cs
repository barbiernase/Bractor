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
public sealed record QueryWeiterleitung(IQuery Query, string CorrelationId);

/// <summary>Eine fertige Server-Nachricht, die ein Hintergrund-Teil der Sitzung schreiben will (Arbeitsaufträge an Funktionen draußen).</summary>
public sealed record ServerNachricht(Leitung.ServerMessage Nachricht);

/// <summary>Was alle Sitzungen eines Knotens teilen.</summary>
public sealed record SitzungsDienste(
    ProtoMessageMapper Mapper,
    IAggregateDispatcher Dispatcher,
    IQueryDienst Queries,
    QueryHandlerRegistry Zustaendige,
    Verteilung? Verteilung,
    AkteurTor? Tor,
    IAusgabeAblage? Ausgaben,
    Konsum Konsum,
    HinweisNetz? HinweisNetz,
    IEventStoreRepository? Log,
    ConcurrentDictionary<string, TaskCompletionSource<Leitung.QueryResponseFromClient>> OffeneWeiterleitungen,
    ILogger Logger);

/// <summary>
/// Die Sitzung einer gRPC-Verbindung als Actor (Plan Pub/Sub-Neustrukturierung §2.7, Phase 7): der <b>einzige Schreiber</b> auf den
/// Antwort-Stream. Die Lese-Schleife der Verbindung reicht jede Nachricht des Clients nur weiter (<see cref="VonClient"/>); Pushes kommen vom
/// Verteiler des Knotens (<see cref="Zustellung"/>, begrenzt über den <see cref="Rueckstau"/>); was wartet (Query, Antwort einer Funktion
/// draußen, Weiterleitung an einen zuständigen Client), läuft neben der Mailbox, geschrieben wird danach wieder im Actor.
/// Nach dem Handshake meldet sie ihre wirksame Liste (Vertrag ∩ Token) beim Verteiler an; gezielte Antworten (Ablehnung, <c>CommandFailed</c>)
/// erreichen sie über ihre Sitzungs-Id, gleich, was sie hört. Endet die Sitzung (Stream zu, Ablehnung am Handshake), beendet sie
/// <see cref="Ende"/>; den Status bekommt der Client vom <see cref="CqrsClientServiceImpl"/>.
/// </summary>
public sealed class SitzungsActor : RahmenActor
{
    /// <summary>Wie lange eine an einen zuständigen Client weitergeleitete Query auf seine Antwort wartet.</summary>
    public static readonly TimeSpan WeiterleitungsFrist = TimeSpan.FromSeconds(60);

    /// <summary>So lange sammelt die Sitzung „P hat S weiterverarbeitet“, bevor sie dem Client die betroffenen Queries nennt.</summary>
    public static readonly TimeSpan Zusammenfassen = TimeSpan.FromMilliseconds(200);

    /// <summary>So viele Einträge liefert eine Seite des Logs höchstens.</summary>
    public const int LogSeiteHoechstens = 1_000;

    /// <summary>So viele Antworten beobachtet eine Sitzung höchstens (die ältesten fallen heraus).</summary>
    public const int HoechstensAntworten = 200;

    /// <summary>Zeigt eine Antwort mehr Ids, beobachtet die Sitzung ihre Projektion grob statt jeder Id (Plan §7, Risiko 6).</summary>
    public const int HoechstensIds = 1_000;

    /// <summary>Was eine gehaltene Antwort zeigt: die Projektion, die antwortete, und die Streams ihrer Ids (leer = grob, die ganze Projektion).</summary>
    private sealed record Beobachtung(string Projektion, IReadOnlySet<Guid> Streams);

    private readonly IServerStreamWriter<Leitung.ServerMessage> _stream;
    private readonly string _id;
    private readonly SitzungsDienste _d;
    private readonly AkteurSitzung _sitzung;
    private readonly TaskCompletionSource _ende;
    private readonly Rueckstau _rueckstau = new();
    private readonly ILogger _logger;
    private BausteinSitzung? _anbieter;
    private PID? _verteiler;
    // Antworten und Lebenszeichen der Funktionen draußen in der Reihenfolge des Clients, eine nach der anderen (eine zweite Antwort auf denselben
    //   Eingang sieht so, dass die erste ihn schon erledigt hat); gewartet wird neben der Mailbox.
    private readonly Queue<Func<IContext, CancellationToken, Task<Func<Task>?>>> _anbieterArbeit = new();
    private bool _anbieterLaeuft;
    // Beobachtung (Plan §2.7, „P hat S weiterverarbeitet“): je gehaltener Antwort, was sie zeigt; die betroffenen Queries sammeln sich bis zur
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
        MeldeAn(context, new HashSet<Type>());   // gezielte Antworten schon vor dem Handshake
        if (_d.Ausgaben is { } ausgaben) _anbieter = BaueAnbieter(context, ausgaben);
        return Task.CompletedTask;
    }

    protected override async Task BeimStoppenAsync(IContext context)
    {
        _d.Zustaendige.UnregisterAll(context.Self);
        // Funktionen draußen: Hol-Schleifen beenden; Laufendes nicht erledigt melden — die Leases laufen ab, der Vermittler gibt die Eingänge
        //   neu aus (anderer Träger, oder dieser nach dem Neuverbinden), und der Funktions-Actor sendet nur, was festgehalten ist.
        if (_anbieter is { } a)
        {
            try { await a.DisposeAsync(); }
            catch (Exception ex) { _logger.LogDebug(ex, "{Session} Funktions-Anbieter: Ende mit Fehler", _id); }
        }
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

            case QueryWeiterleitung w:
                await SchreibeAsync(new Leitung.ServerMessage
                {
                    QueryForward = new Leitung.QueryForward { CorrelationId = w.CorrelationId, Payload = _d.Mapper.MapToDto(w.Query) }
                });
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
        if (antwort.Projektion is not { } projektion) return;
        var ids = antwort.Deps?.Select(d => d.Id).Where(g => g != Guid.Empty).ToHashSet() ?? new HashSet<Guid>();
        if (ids.Count > HoechstensIds) ids.Clear();
        if (!_beobachtungen.ContainsKey(query))
        {
            _beobachtungsReihe.Enqueue(query);
            while (_beobachtungsReihe.Count > HoechstensAntworten) _beobachtungen.Remove(_beobachtungsReihe.Dequeue());
        }
        _beobachtungen[query] = new Beobachtung(projektion, ids);
        context.Send(_verteiler!, new SitzungBeobachtet(_id,
            _beobachtungen.Values.SelectMany(b => b.Streams).ToHashSet(),
            _beobachtungen.Values.Where(b => b.Streams.Count == 0).Select(b => b.Projektion).ToHashSet(StringComparer.Ordinal)));
    }

    /// <summary>Sammelt die betroffenen Queries und nennt sie dem Client nach <see cref="Zusammenfassen"/> in einer Nachricht.</summary>
    private void Weiterverarbeitet(IContext context, Verarbeitet v)
    {
        foreach (var (query, b) in _beobachtungen)
            if (b.Streams.Count == 0 ? b.Projektion == v.Projektion : v.Streams.Any(b.Streams.Contains))
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
                await CommandAsync(m.Command);
                break;
            case Leitung.ClientMessage.MessageOneofCase.Query:
                await QueryAsync(context, m.Query);
                break;
            case Leitung.ClientMessage.MessageOneofCase.TransientEvent:
                await HinweisAsync(m.TransientEvent);
                break;
            case Leitung.ClientMessage.MessageOneofCase.QueryAnswer:
                if (_d.OffeneWeiterleitungen.TryGetValue(m.QueryAnswer.CorrelationId, out var offen)) offen.TrySetResult(m.QueryAnswer);
                else _logger.LogWarning("{Session} QueryAnswer for unknown CorrelationId {CorrelationId}", _id, m.QueryAnswer.CorrelationId);
                break;
            case Leitung.ClientMessage.MessageOneofCase.LiesLog:
                await LiesLogAsync(context, m.LiesLog);
                break;
            case Leitung.ClientMessage.MessageOneofCase.FunktionenAnbieten:
                await FunktionenAnbietenAsync(m.FunktionenAnbieten);
                break;
            case Leitung.ClientMessage.MessageOneofCase.ArbeitsErgebnis:
                await ArbeitsErgebnisAsync(context, m.ArbeitsErgebnis);
                break;
            case Leitung.ClientMessage.MessageOneofCase.ArbeitLebt:
                if (_anbieter is { } a && Guid.TryParse(m.ArbeitLebt.Vorgang, out var lebt))
                    AnbieterArbeit(context, async (_, _) => { await a.LebtAsync(lebt); return null; });
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

        // Befugnis statt Selbstauskunft: erlaubte Mengen aus IDarf (∪ Vertrags-Ausgaben), Hören abgeleitet, Zuständigkeit nur für Lücken (das
        //   System bedient den Typ nicht selbst) und nur einmal. Mit Vertrag hört die Sitzung genau, was er hört.
        var result = Handshake.Ermittle(request, _id);
        Func<string, Type?> loese = n => MessageTypeMapping.Resolve(n).Type;
        Func<Type, bool> internBedient = t => Anwendung.QueryTypen.Contains(t);
        Func<string, bool> schonVergeben = n => _d.Zustaendige.GetHandler(n) is { } q && !q.Equals(context.Self);
        if (_sitzung.Vertrag is { } v)
        {
            var abweichung = ClientVertragsPruefung.Wende(result, v, loese, internBedient, schonVergeben);
            if (abweichung.Count > 0)
                _logger.LogInformation("{Session} Vertrag {Vertrag} statt Selbstauskunft: [{Abweichung}]", _id, v.Name, string.Join(", ", abweichung));
        }
        else if (akteur != null)
        {
            var verweigert = AkteurTor.Wende(result, akteur, loese, internBedient, schonVergeben);
            if (verweigert.Count > 0)
                _logger.LogWarning("{Session} Akteur {Akteur} verweigert: [{Verweigert}]", _id, akteur.Name, string.Join(", ", verweigert));
        }

        // Die wirksame Liste beim Verteiler anmelden (ersetzt die vorige).
        MeldeAn(context, result.SubscribedEvents.Select(n => MessageTypeMapping.Resolve(n))
            .Where(r => r.Type is not null && r.Category == MessageCategory.Event).Select(r => r.Type!).ToHashSet());

        foreach (var queryName in result.HandlingQueries)
            _d.Zustaendige.Register(queryName, context.Self);

        if (result.UnknownTypes.Count > 0)
            _logger.LogWarning("{Session} unknown types: [{Types}]", _id, string.Join(", ", result.UnknownTypes));

        var antwort = Handshake.Antwort(result);
        if (_sitzung.Vertrag is { } angenommen)
        {
            antwort.Vertrag = angenommen.Name;
            antwort.VertragHash = angenommen.VertragHash;
        }
        await SchreibeAsync(new Leitung.ServerMessage { CapabilitiesResponse = antwort });
        _logger.LogInformation("{Session} → CapabilitiesResponse: {Commands} commands, {Events} events, {Queries} queries, handling {HandlingQueries} queries",
            _id, result.AllowedCommands.Count, result.SubscribedEvents.Count, result.AllowedQueries.Count, result.HandlingQueries.Count);
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

        // Beantwortet ein Client sie (Zuständiger): weiterleiten und neben der Mailbox auf seine Antwort warten.
        if (_d.Zustaendige.GetHandler(name) is { } zustaendig)
        {
            var korrelation = string.IsNullOrEmpty(request.CorrelationId) ? Guid.NewGuid().ToString() : request.CorrelationId;
            var offen = new TaskCompletionSource<Leitung.QueryResponseFromClient>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_d.OffeneWeiterleitungen.TryAdd(korrelation, offen))
            {
                await FehlerAsync("QUERY_DUPLICATE_CORRELATION", "Duplicate correlation ID for query forward", request.CorrelationId);
                return;
            }
            context.Send(zustaendig, new QueryWeiterleitung(query, korrelation));
            Neben(context, ct => offen.Task.WaitAsync(WeiterleitungsFrist, ct), async t =>
            {
                _d.OffeneWeiterleitungen.TryRemove(korrelation, out _);
                if (!t.IsCompletedSuccessfully)
                {
                    await FehlerAsync("QUERY_FORWARD_TIMEOUT", "Client handler did not respond in time", request.CorrelationId);
                    return;
                }
                var antwort = await (Task<Leitung.QueryResponseFromClient>)t;
                if (!string.IsNullOrEmpty(antwort.ErrorCode))
                    await FehlerAsync(antwort.ErrorCode, antwort.ErrorMessage, request.CorrelationId);
                else
                    await SchreibeAsync(new Leitung.ServerMessage
                    {
                        QueryResponse = new Leitung.QueryResponse { CorrelationId = request.CorrelationId, Payload = antwort.Payload }
                    });
            });
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

        // An die Clients, die ihn hören (Verteiler), und an die Funktionen, die ihn hören (Hinweis-Netz): die Kennung vergibt der Client
        //   (CausationId, sonst gewürfelt — Clients würfeln, R13), die Adresse folgt aus dem Stream, den er nennt (A6).
        _d.Verteilung?.Melde(envelope with { ZielSitzung = null });
        _d.HinweisNetz?.Verteile(new HinweisZustellen(Guid.TryParse(envelope.CausationId, out var kennung) ? kennung : Guid.NewGuid(), envelope.Payload,
            envelope.AggregateId, envelope.AggregateVersion, DateTimeOffset.UtcNow, envelope.CorrelationId,
            ImAuftrag.IstAkteur(envelope.UserId) ? envelope.UserId : null));
        _logger.LogDebug("{Session} TransientEvent published: {Payload}", _id, envelope.Payload.GetType().Name);
    }

    // ═════════════════════════════════════════
    // FUNKTIONEN DRAUSSEN (ITraegt, Laufort Extern)
    // ═════════════════════════════════════════

    /// <summary>
    /// Die Anbieter-Sitzung dieser Verbindung: Hol-/Melde-Wege zum Vermittler im Cluster, Weiterreichen über diesen Actor (ein Schreiber am
    /// Stream), Festhalten der Antwort unter „Funktion, Eingang" und die Meldung an den Funktions-Actor der Partition, der sendet.
    /// </summary>
    private BausteinSitzung BaueAnbieter(IContext context, IAusgabeAblage ausgaben)
    {
        var bote = new Bote(context.System.Cluster(), _logger);
        var selbst = context.Self;
        var root = context.System.Root;
        var draussen = Anwendung.Funktionen.Bausteine.Where(b => b.Draussen).ToList();
        return new BausteinSitzung(
            _id,   // Arbeiter-Name am Vermittler: die Sitzungs-Id nennt den Knoten, eindeutig über Knoten hinweg
            name => draussen.FirstOrDefault(b => b.Name == name),
            (b, anfrage, ct) => bote.FrageAsync<EingaengeZugeteilt>(FunktionsVermittlerActor.Identitaet(b.Typ), anfrage,
                TimeSpan.FromMilliseconds(Math.Max(0, anfrage.WarteMs)) + TimeSpan.FromSeconds(5), ct),
            (b, nachricht) => { bote.Melde(FunktionsVermittlerActor.Identitaet(b.Typ), nachricht); return Task.CompletedTask; },
            (e, b, ab) =>
            {
                root.Send(selbst, new ServerNachricht(new Leitung.ServerMessage
                {
                    ArbeitsAuftrag = new Leitung.ArbeitsAuftrag
                    {
                        Vorgang = e.Vorgang.ToString(),
                        Korrelation = e.KorrelationsText ?? "",
                        Funktion = b.Name,
                        Eingang = _d.Mapper.MapToDto(e.Eingang),
                        Akteur = e.Akteur ?? "",
                        Stream = e.Stream.ToString(),
                        Version = e.Version,
                        ZeitUtc = e.Zeit.ToUnixTimeMilliseconds(),
                        Ab = ab,
                        Hinweis = e.IstHinweis,
                    }
                }));
                return Task.CompletedTask;
            },
            ausgaben,
            (b, e, fehler, teil, aus) =>
            {
                bote.Melde(FunktionsActor.IdentitaetFuer(b, e.Stream, _d.Konsum), EingangBeantwortet.Aus(e.Vorgang, fehler, teil, aus));
                return Task.CompletedTask;
            },
            _logger);
    }

    private async Task FunktionenAnbietenAsync(Leitung.FunktionenAnbieten request)
    {
        if (_anbieter == null)
        {
            await FehlerAsync("FUNKTIONEN_NICHT_VERFUEGBAR", "Dieser Host hat keine Funktion, die draußen rechnet", "");
            return;
        }
        // Akteur-Tor: mit angemeldetem Akteur nur, was er rechnen darf (IDarf<F>, bei einem Client ∩ ITraegt); ohne Tor/Akteur offen.
        var akteur = _sitzung.Akteur;
        var unbekannt = _anbieter.Biete(request.Angebote.Select(a => (a.Funktion, a.Slots)),
            akteur is null ? null : akteur.DarfRechnen, out var nichtBefugt);
        if (nichtBefugt.Count > 0)
            await FehlerAsync("FUNKTION_NICHT_BEFUGT",
                $"{akteur!.Name} darf nicht rechnen: {string.Join(", ", nichtBefugt)} (IDarf<…> am Akteur bzw. ITraegt<…> im Client-Vertrag fehlt)", "");
        if (unbekannt.Count > 0)
        {
            _logger.LogWarning("{Session} bietet unbekannte Funktionen an: [{Unbekannt}]", _id, string.Join(", ", unbekannt));
            await FehlerAsync("FUNKTION_UNBEKANNT",
                $"Unbekannte Funktion(en) (keine Funktion draußen dieses Namens): {string.Join(", ", unbekannt)}", "");
        }
    }

    private async Task ArbeitsErgebnisAsync(IContext context, Leitung.ArbeitsErgebnis request)
    {
        if (_anbieter is not { } anbieter || !Guid.TryParse(request.Vorgang, out var vorgang))
        {
            await FehlerAsync("ARBEITS_ERGEBNIS_UNGUELTIG",
                $"Antwort ohne gültigen Eingang oder ohne Funktion draußen: '{request.Vorgang}'", request.Vorgang);
            return;
        }
        var fehler = string.IsNullOrEmpty(request.Fehler) ? null : request.Fehler;
        List<IAusgabe>? ausgaben = null;
        if (fehler == null)
        {
            try { ausgaben = request.Ausgaben.Select(_d.Mapper.Ausgabe).ToList(); }
            catch (Exception ex) { fehler = $"Antwort nicht lesbar: {ex.Message}"; }
        }
        AnbieterArbeit(context, async (_, ct) =>
        {
            var ausgang = await anbieter.AntwortAsync(vorgang, ausgaben, fehler, ct, request.Ab, request.Fertig);
            _logger.LogDebug("{Session} Antwort {Vorgang} (ab {Ab}, fertig {Fertig}): {Ausgang}", _id, vorgang, request.Ab, request.Fertig, ausgang);
            return ausgang == ErgebnisAusgang.Unbekannt
                ? () => FehlerAsync("ARBEIT_UNBEKANNT", $"Eingang {vorgang} läuft in dieser Sitzung nicht (Lease abgelaufen oder schon erledigt)", request.Vorgang)
                : null;
        });
    }

    /// <summary>Reiht Arbeit der Funktionen draußen ein: eine nach der anderen neben der Mailbox; was sie danach schreiben will, schreibt der Actor.</summary>
    private void AnbieterArbeit(IContext context, Func<IContext, CancellationToken, Task<Func<Task>?>> arbeit)
    {
        _anbieterArbeit.Enqueue(arbeit);
        if (!_anbieterLaeuft) NaechsteAnbieterArbeit(context);
    }

    private void NaechsteAnbieterArbeit(IContext context)
    {
        if (!_anbieterArbeit.TryDequeue(out var arbeit)) { _anbieterLaeuft = false; return; }
        _anbieterLaeuft = true;
        Neben(context, ct => arbeit(context, ct), async t =>
        {
            if (t.IsCompletedSuccessfully && await (Task<Func<Task>?>)t is { } danach) await danach();
            else if (t.IsFaulted) _logger.LogWarning(t.Exception, "{Session} Funktion draußen: Antwort nicht verarbeitet", _id);
            NaechsteAnbieterArbeit(context);
        });
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
