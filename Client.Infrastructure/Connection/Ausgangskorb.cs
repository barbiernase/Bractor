using System.Collections.Concurrent;
using Abstractions;
using Cqrs.Leitung;
using Google.Protobuf;
using Infrastructure.Serialization;

namespace Client.Infrastructure.Connection;

/// <summary>
/// Der Ausgangskorb des Clients (Plan Pub/Sub-Neustrukturierung §4.2, „Client → Aggregat“): ein Command liegt darin, BEVOR er gesendet wird, und
/// bleibt, bis seine Antwort kam (Event, Ablehnung oder <c>CommandFailed</c> mit seiner Korrelation). Nach dem Wiederverbinden — mit einem
/// dauerhaften Korb auch nach einem Neustart des Clients — geht jeder offene noch einmal hinaus, mit DERSELBEN Command-Id: die Inbox des Ziels
/// sorgt dafür, dass er genau einmal wirkt.
/// </summary>
public interface IAusgangskorb
{
    /// <summary>Legt den Command ab, bevor er gesendet wird.</summary>
    void Lege(CommandEnvelope envelope);

    /// <summary>Seine Antwort kam — er ist erledigt.</summary>
    void Erledigt(Guid commandId);

    /// <summary>Die offenen Commands, in der Reihenfolge, in der sie hineinkamen.</summary>
    IReadOnlyList<CommandEnvelope> Offen();
}

/// <summary>Der Ausgangskorb im Speicher: übersteht ein Wiederverbinden, keinen Neustart des Clients.</summary>
public sealed class Ausgangskorb : IAusgangskorb
{
    private readonly ConcurrentDictionary<Guid, (long Nr, CommandEnvelope Envelope)> _offen = new();
    private long _nr;

    public void Lege(CommandEnvelope envelope) => _offen[envelope.CommandId] = (Interlocked.Increment(ref _nr), envelope);

    public void Erledigt(Guid commandId) => _offen.TryRemove(commandId, out _);

    public IReadOnlyList<CommandEnvelope> Offen() => _offen.Values.OrderBy(e => e.Nr).Select(e => e.Envelope).ToList();
}

/// <summary>
/// Der Ausgangskorb als Verzeichnis: je offenem Command eine Datei mit seiner Hülle, so wie sie auf die Leitung geht (Proto, mit Command-Id und
/// Korrelation) — er übersteht auch einen Neustart des Clients. Geschrieben wird erst in eine temporäre Datei, dann umbenannt (nie halb).
/// </summary>
public sealed class DateiAusgangskorb : IAusgangskorb
{
    private const string Endung = ".befehl";
    private readonly string _verzeichnis;
    private readonly INutzlastTabelle _tabelle;
    private readonly ProtoMessageMapper _mapper;

    public DateiAusgangskorb(string verzeichnis, INutzlastTabelle tabelle)
    {
        _verzeichnis = verzeichnis;
        _tabelle = tabelle;
        _mapper = new ProtoMessageMapper(tabelle);
        Directory.CreateDirectory(verzeichnis);
    }

    public void Lege(CommandEnvelope envelope)
    {
        // Der Name ordnet nach der Zeit des Ablegens (Ticks), dann nach der Id.
        var datei = Path.Combine(_verzeichnis, $"{DateTime.UtcNow.Ticks:D20}-{envelope.CommandId:N}{Endung}");
        var temp = datei + ".tmp";
        File.WriteAllBytes(temp, _mapper.MapToDto(envelope).ToByteArray());
        File.Move(temp, datei, overwrite: true);
    }

    public void Erledigt(Guid commandId)
    {
        foreach (var datei in Directory.EnumerateFiles(_verzeichnis, $"*-{commandId:N}{Endung}"))
            File.Delete(datei);
    }

    public IReadOnlyList<CommandEnvelope> Offen() =>
        Directory.EnumerateFiles(_verzeichnis, "*" + Endung).Order(StringComparer.Ordinal)
            .Select(d => Huelle(CommandEnvelopeDto.Parser.ParseFrom(File.ReadAllBytes(d)))).ToList();

    // Die Hülle zurück, wie der Client sie gebaut hatte (ohne die Routing-Tabelle des Servers): Id, Korrelation, Version, Aggregat-Typ.
    private CommandEnvelope Huelle(CommandEnvelopeDto dto) => new()
    {
        CommandId = Guid.Parse(dto.CommandId),
        Payload = _tabelle.Command(dto.Payload),
        Modus = new CommandModus.Client(dto.ExpectedVersion),
        CreatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(dto.CreatedAtUtc),
        CorrelationId = dto.CorrelationId,
        UserId = dto.UserId,
        AggregateType = dto.AggregateType,
    };
}
