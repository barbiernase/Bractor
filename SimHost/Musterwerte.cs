using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SimHost;

/// <summary>
/// Werte für eine Nachricht, die niemand gerechnet hat — das Ergebnis einer Funktion ohne Implementierung (Entwurf zuerst).
/// Ein Feld nimmt den gleichnamigen Wert aus dem Auftrag bzw. den Nachrichten am Draht (die Zuordnung „Feld ← Draht.Feld“ liest
/// sich auch rückwärts), sonst einen lesbaren Platzhalter: Text = Feldname, Zahl = 1, Guid deterministisch aus dem Ort (so spielt
/// der Hot-Reload dieselbe Geschichte mit denselben Ids nach), Liste = zwei Elemente (damit ein Je-Rahmen etwas aufzufächern hat).
/// Ein Overlay (JSON aus dem Panel) überschreibt einzelne Felder. Tooling-Reflection im SimHost, nie im Framework.
/// </summary>
internal static class Musterwerte
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static object Baue(Type typ, string ort, IReadOnlyList<object> quellen, JsonElement? overlay = null)
    {
        var wert = Wert(typ, typ.Name, ort, quellen, 0);
        if (overlay is not { ValueKind: JsonValueKind.Object } o || wert is null) return wert ?? throw Fehlt(typ);
        try
        {
            var basis = JsonSerializer.SerializeToNode(wert, typ, Json)!.AsObject();
            foreach (var p in o.EnumerateObject())
            {
                var schlüssel = basis.Select(kv => kv.Key).FirstOrDefault(k => string.Equals(k, p.Name, StringComparison.OrdinalIgnoreCase)) ?? p.Name;
                basis[schlüssel] = JsonNode.Parse(p.Value.GetRawText());
            }
            return basis.Deserialize(typ, Json) ?? wert;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Werte passen nicht zu {typ.Name}: {ex.Message}");
        }
    }

    private static Exception Fehlt(Type t) => new InvalidOperationException($"{t.Name} lässt sich nicht bauen (kein öffentlicher Konstruktor).");

    private static object? Wert(Type t, string name, string ort, IReadOnlyList<object> quellen, int tiefe)
    {
        // Gleichnamiger Wert aus Auftrag / Drähten (jüngste Quelle zuerst).
        foreach (var q in quellen.Reverse())
        {
            var p = q.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p is null || p.GetIndexParameters().Length > 0 || !t.IsAssignableFrom(p.PropertyType)) continue;
            try { return p.GetValue(q); } catch { /* weiter */ }
        }

        var basis = Nullable.GetUnderlyingType(t) ?? t;
        if (basis == typeof(string)) return name;
        if (basis == typeof(Guid)) return Guid(ort + "|" + name);
        if (basis == typeof(bool)) return false;
        if (basis == typeof(DateTimeOffset)) return DateTimeOffset.UtcNow;
        if (basis == typeof(DateTime)) return DateTime.UtcNow;
        if (basis == typeof(TimeSpan)) return TimeSpan.FromSeconds(1);
        if (basis.IsEnum) return Enum.GetValues(basis).GetValue(0);
        if (basis.IsPrimitive || basis == typeof(decimal)) return Convert.ChangeType(1, basis);
        if (tiefe > 3) return null;

        var element = ElementTyp(basis);
        if (element is not null)
        {
            var liste = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
            for (var i = 0; i < 2; i++) liste.Add(Wert(element, $"{Einzahl(name)}{i + 1}", $"{ort}|{name}[{i}]", Array.Empty<object>(), tiefe + 1));
            if (basis.IsArray)
            {
                var arr = Array.CreateInstance(element, liste.Count);
                liste.CopyTo(arr, 0);
                return arr;
            }
            return basis.IsAssignableFrom(liste.GetType()) ? liste : Activator.CreateInstance(basis, liste);
        }

        if (basis.IsInterface || basis.IsAbstract) return null;
        var ctor = basis.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
        if (ctor is null) return basis.IsValueType ? Activator.CreateInstance(basis) : null;
        var args = ctor.GetParameters()
            .Select(p => Wert(p.ParameterType, p.Name ?? "x", ort, tiefe == 0 ? quellen : Array.Empty<object>(), tiefe + 1))
            .ToArray();
        return ctor.Invoke(args);
    }

    private static Type? ElementTyp(Type t)
    {
        if (t == typeof(string)) return null;
        if (t.IsArray) return t.GetElementType();
        var e = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? t : t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return e?.GetGenericArguments()[0];
    }

    private static string Einzahl(string name) => name.Length > 1 && name.EndsWith('n') ? name[..^1] : name;

    private static Guid Guid(string ort) => new(MD5.HashData(Encoding.UTF8.GetBytes(ort)));
}
