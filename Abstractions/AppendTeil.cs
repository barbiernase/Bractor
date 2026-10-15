namespace Abstractions;

/// <summary>
/// Ein Teil eines Appends an EINEN Stream (docs/03 §3.1, §3.4): die Events eines Commands mit ihren Metadaten (Korrelation, Kausalität =
/// CommandId, Akteur, Spur) und Beilagen. Ein Aggregat-Actor hängt die Commands eines Bündels als Teile in einem Append an — eine Transaktion,
/// Metadaten je Event aus seinem Teil; der Einzel-Append ist ein Append mit einem Teil.
/// </summary>
public sealed record AppendTeil(
    IReadOnlyList<IEvent> Events,
    string? CorrelationId,
    string? CausationId,
    string? Akteur,
    Spur.Kopf? SpurKopf,
    IReadOnlyList<Beilage>? Beilagen = null);
