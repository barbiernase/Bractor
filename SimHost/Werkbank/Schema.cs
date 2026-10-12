using System.Reflection;
using System.Text.Json.Nodes;
using Abstractions;

namespace SimHost.Werkbank;

/// <summary>
/// Das Formular-Schema der Umgebung (docs/konzept-werkbank.md §8.1): aus den Konfig-Typen (<see cref="UmgebungKonfig"/>, Quellen-Konfig,
/// Funktions-Laufort) — Klasse → Felder, Enum → Auswahl, Default aus dem leeren Objekt. Der Editor zeichnet daraus JEDES Panel des
/// Host-Rahmens mit EINEM generischen Renderer, keine handgebauten Formulare je Karte. (Werkbank-Werkzeug, nicht Laufzeit.)
/// </summary>
public static class Schema
{
    public static JsonObject Umgebung()
    {
        var aus = new JsonObject();
        foreach (var p in typeof(UmgebungKonfig).GetProperties().Where(p => p.PropertyType.IsClass && p.PropertyType != typeof(string)
                     && !p.PropertyType.IsGenericType))
            aus[p.Name] = Klasse(p.PropertyType);
        aus[UmgebungsAbschnitte.Funktionen] = new JsonObject { ["art"] = "je", ["schluessel"] = "Funktion", ["wert"] = Klasse(typeof(FunktionsKonfig)) };
        aus[UmgebungsAbschnitte.Quellen] = new JsonObject { ["art"] = "je", ["schluessel"] = "Quelle", ["wert"] = Klasse(typeof(DateiQuellKonfig)) };
        aus[UmgebungsAbschnitte.Identitaeten] = new JsonObject
        {
            ["art"] = "klasse",
            ["felder"] = new JsonObject
            {
                ["Tokens"] = new JsonObject { ["typ"] = "map", ["standard"] = "{}", ["hilfe"] = "Token → Akteur(e), kommagetrennt" },
                ["Standard"] = new JsonObject { ["typ"] = "string", ["standard"] = "" },
                ["VertragStreng"] = new JsonObject { ["typ"] = "bool", ["standard"] = "false" },
            },
        };
        aus[UmgebungsAbschnitte.Konfig] = new JsonObject { ["art"] = "frei", ["hilfe"] = "je Konfig-Record der Domäne ein Abschnitt (Feldname → Wert)" };
        return aus;
    }

    private static JsonObject Klasse(Type t)
    {
        var leer = Activator.CreateInstance(t);
        var felder = new JsonObject();
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
        {
            var typ = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            var wert = leer == null ? null : p.GetValue(leer);
            JsonObject feld;
            if (typ.IsEnum) feld = new JsonObject { ["typ"] = "enum", ["werte"] = new JsonArray(Enum.GetNames(typ).Select(n => (JsonNode)n).ToArray()) };
            else if (typ == typeof(bool)) feld = new JsonObject { ["typ"] = "bool" };
            else if (typ == typeof(int) || typ == typeof(long)) feld = new JsonObject { ["typ"] = "zahl" };
            else if (typ == typeof(TimeSpan)) feld = new JsonObject { ["typ"] = "dauer" };
            else if (typ == typeof(string)) feld = new JsonObject { ["typ"] = "string" };
            else if (typ == typeof(string[])) feld = new JsonObject { ["typ"] = "liste" };
            else if (typ.IsClass) feld = Klasse(typ);
            else feld = new JsonObject { ["typ"] = "string" };
            if (wert != null && !typ.IsClass || wert is string) feld["standard"] = wert switch { TimeSpan ts => ts.ToString(), bool b => b ? "true" : "false", _ => wert?.ToString() };
            felder[p.Name] = feld;
        }
        return new JsonObject { ["art"] = "klasse", ["felder"] = felder };
    }
}
