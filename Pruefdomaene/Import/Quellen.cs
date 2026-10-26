using Abstractions;

namespace Pruefdomaene.Import;

/// <summary>
/// Eine Quell-Nachricht vom Client (Plan Pub/Sub-Neustrukturierung Phase 3): ein Import ist angefordert. Ihre Kennung ist die Command-Id, die der
/// Client vergibt — dieselbe Anforderung zweimal ist ein Fakt im Log.
/// </summary>
public sealed record ImportAngefordert(int Zeilen) : ClientGemeldet;
