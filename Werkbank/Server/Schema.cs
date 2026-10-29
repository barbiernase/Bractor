using System.Reflection;
using System.Text.Json.Nodes;
using Abstractions;

namespace Werkbank.Server;

/// <summary>
/// Das Formular-Schema der Umgebung (CLAUDE.md W1–W12): aus den Konfig-Typen (<see cref="UmgebungKonfig"/>, Quellen-Konfig,
/// Funktions-Laufort) — Klasse → Felder, Enum → Auswahl, Default aus dem leeren Objekt. Der Editor zeichnet daraus JEDES Panel des
/// Host-Rahmens mit EINEM generischen Renderer, keine handgebauten Formulare je Karte. (Werkbank-Werkzeug, nicht Laufzeit.)
/// </summary>
public static class Schema
{
    /// <param name="board">Das gelesene Board (warmer Workspace) — liefert je Dienst seine Implementierungen und je Konfig-Record seine Felder
    /// (Code-Fakten), damit das Formular sie anbietet; ohne Board bleiben Dienste/Konfig frei.</param>
    public static JsonObject Umgebung(JsonObject? board = null)
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
        // Dienste (docs/06 §6.5): je Dienst die gewählte Implementierung — die Auswahl kommt aus dem Code (Implementierungen der Dienst-Karte).
        var dienste = new JsonObject();
        foreach (var d in Liste(board?["dienste"]))
            dienste[(string?)d["name"] ?? "?"] = new JsonObject
            {
                ["art"] = "klasse",
                ["felder"] = new JsonObject
                {
                    [nameof(DienstKonfig.Implementierung)] = new JsonObject
                    {
                        ["typ"] = "enum", ["werte"] = new JsonArray(Liste(d["implementierungen"]).Select(i => (JsonNode?)(string?)i["name"]).ToArray()),
                        ["hilfe"] = "Die Klasse, die hier läuft — ohne Eintrag die einzige",
                    },
                },
            };
        aus[UmgebungsAbschnitte.Dienste] = new JsonObject
        {
            ["art"] = "je", ["schluessel"] = "Dienst", ["wert"] = Klasse(typeof(DienstKonfig)), ["eintraege"] = dienste,
            ["hilfe"] = "je Dienst (IDienst) die Implementierung — nötig, wenn es mehrere gibt",
        };
        // Clients (docs/konzept-akteure.md §4): je Client-Vertrag (Handshake-Name) Token, Adresse, URLs und Datei-Verzeichnisse.
        var clients = new JsonObject();
        foreach (var c in Liste(board?["clients"]))
            clients[Abstractions.Clientvertragsregeln.ClientName((string?)c["name"] ?? "?")] = Klasse(typeof(ClientKonfig));
        aus[UmgebungsAbschnitte.Clients] = new JsonObject
        {
            ["art"] = "je", ["schluessel"] = "Client", ["wert"] = Klasse(typeof(ClientKonfig)), ["eintraege"] = clients,
            ["hilfe"] = "je Client (IClientVertrag) sein Token (Identitaeten), die gRPC-Adresse (leer = localhost:Transport.GrpcPort), URLs, Datei-Verzeichnisse",
        };
        // Konfig-Records (docs/06 §6.5): je Record ein Abschnitt mit seinen Feldern (Typen aus dem Code).
        var konfigs = new JsonObject();
        var enums = Liste(board?["enums"]).ToDictionary(e => (string?)e["name"] ?? "", e => e, StringComparer.Ordinal);
        foreach (var r in Liste(board?["records"]).Where(r => (string?)r["kind"] == "konfig"))
        {
            var felder = new JsonObject();
            foreach (var f in Liste(r["felder"]))
                felder[(string?)f["name"] ?? "?"] = FeldVonTyp(((string?)f["typ"] ?? "string").TrimEnd('?'), enums, (string?)f["standard"]);
            konfigs[(string?)r["name"] ?? "?"] = new JsonObject { ["art"] = "klasse", ["felder"] = felder };
        }
        aus[UmgebungsAbschnitte.Konfig] = board == null
            ? new JsonObject { ["art"] = "frei", ["hilfe"] = "je Konfig-Record der Domäne ein Abschnitt (Feldname → Wert)" }
            : new JsonObject { ["art"] = "je", ["schluessel"] = "Konfig-Record", ["wert"] = new JsonObject { ["art"] = "klasse", ["felder"] = new JsonObject() },
                ["eintraege"] = konfigs, ["hilfe"] = "je Konfig-Record (im Konstruktor einer Funktions-/Dienst-Implementierung) seine Werte" };
        return aus;
    }

    private static IEnumerable<JsonObject> Liste(JsonNode? n) => (n as JsonArray ?? []).OfType<JsonObject>();

    /// <summary>Ein Feld eines Konfig-Records als Formular-Feld — dieselben Typen, die die generierte Bindung liest (CQRS091).</summary>
    private static JsonObject FeldVonTyp(string typ, IReadOnlyDictionary<string, JsonObject> enums, string? standard)
    {
        var feld = typ switch
        {
            "bool" => new JsonObject { ["typ"] = "bool" },
            "int" or "long" or "double" or "decimal" => new JsonObject { ["typ"] = "zahl" },
            "TimeSpan" => new JsonObject { ["typ"] = "dauer" },
            "string[]" => new JsonObject { ["typ"] = "liste" },
            "Guid" => new JsonObject { ["typ"] = "guid" },
            _ when enums.TryGetValue(typ, out var e) => new JsonObject { ["typ"] = "enum", ["werte"] = (e["werte"] ?? new JsonArray()).DeepClone() },
            _ => new JsonObject { ["typ"] = "string" },
        };
        if (standard != null) feld["standard"] = standard;
        return feld;
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
