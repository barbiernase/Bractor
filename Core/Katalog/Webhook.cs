using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Abstractions;

namespace Core.Katalog;

/// <summary>Die Konfig eines Webhooks: der Pfad, unter dem er Aufrufe annimmt (<c>POST /eingang/webhook/&lt;Pfad&gt;</c> auf dem Einblick-Port).</summary>
public sealed record Webhookkonfig(string Pfad);

/// <summary>
/// Die Postfächer der Webhooks eines Prozesses (je Pfad eines): der Eingang legt einen Aufruf hinein, der Webhook am Kopf seines Graphen nimmt ihn heraus.
/// Ein Postfach ist begrenzt — ist es voll, heißt die Antwort „später“ (der Absender wiederholt mit derselben Kennung).
/// </summary>
public static class Webhookpostfaecher
{
    /// <summary>So viele Aufrufe wartet ein Postfach höchstens.</summary>
    public const int Groesse = 1_000;

    private static readonly ConcurrentDictionary<string, Channel<Aufruf>> Je = new(StringComparer.Ordinal);

    public static Channel<Aufruf> Von(string pfad) =>
        Je.GetOrAdd(pfad.Trim('/'), _ => Channel.CreateBounded<Aufruf>(new BoundedChannelOptions(Groesse) { FullMode = BoundedChannelFullMode.Wait }));

    /// <summary>Legt einen Aufruf in das Postfach des Pfads; false = voll (später).</summary>
    public static bool Lege(string pfad, Aufruf aufruf) => Von(pfad).Writer.TryWrite(aufruf);
}

/// <summary>
/// Der WEBHOOK (Katalog des Frameworks, docs/04 §4.7): eine Quellfunktion am Kopf eines Graphen — je Aufruf ein <see cref="Aufruf"/>, ein neuer Lauf. Er
/// hält nichts fest: ein Aufruf, dessen Lauf nicht fertig wurde, kommt nach einem Neustart nicht wieder — der Absender wiederholt (seine Kennung reist mit).
/// </summary>
public sealed class Webhook(Webhookkonfig konfig) : IFunktion
{
    public async IAsyncEnumerable<Aufruf> Lausche([EnumeratorCancellation] CancellationToken ct)
    {
        var postfach = Webhookpostfaecher.Von(konfig.Pfad).Reader;
        while (await postfach.WaitToReadAsync(ct))
            while (postfach.TryRead(out var aufruf))
                yield return aufruf;
    }
}
