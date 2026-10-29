using Infrastructure.Komposition;
using Marten;
using Marten.Services;

namespace Infrastructure.Persistence;

/// <summary>
/// Typisierte Ids (<c>Id&lt;X&gt;</c>, docs/10 §10.4) stehen im Log und in Snapshots als UUID-String. Martens Serializer bekommt dafür dieselben
/// geschlossenen Konverter wie der generierte Event-Kontext (der Codegen trägt je verwendetem <c>Id&lt;X&gt;</c> einen ein) — ohne Reflection,
/// an einer Stelle für den Host und jeden Store, der dasselbe Log liest (Tests).
/// </summary>
public static class IdSerialisierung
{
    public static void Registriere(StoreOptions options)
    {
        if (options.Serializer() is not SystemTextJsonSerializer stj)
            throw new InvalidOperationException($"Marten serialisiert mit {options.Serializer().GetType().Name} — das Framework erwartet System.Text.Json");
        var konverter = Anwendung.EventJson.Options.Converters;
        stj.Configure(o =>
        {
            foreach (var k in konverter)
                o.Converters.Add(k);
        });
    }
}
