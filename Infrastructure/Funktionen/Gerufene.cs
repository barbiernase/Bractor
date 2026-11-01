using System.Globalization;
using Abstractions;
using Infrastructure.Umgebung;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// BINDUNG DER GERUFENEN FUNKTIONEN (docs/06 §6.5): WAS es gibt, ist Code — gerufene Funktionen (`IFunktion` ohne `Auf`), ihre
// Umsetzungen und die Konfig-Records in den Konstruktoren (generierte Tabelle `GeneratedGerufene`). WELCHE Umsetzung läuft und mit welchen
// Werten, sagt die Umgebung (`Funktionen:<Name>:Implementierung` — derselbe Abschnitt wie für Funktionen mit Eingängen —, `Konfig:<Record>`).
// Der Start prüft gegen den Bedarf der Funktionen, die im Host rechnen: fehlt etwas, ist es mehrdeutig oder zyklisch, startet nichts.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Eine Umsetzung einer gerufenen Funktion (aus <c>GeneratedGerufene.Umsetzungen</c>): Name, Typ und generierte Bindung — bei einer Klasse ist
/// sie selbst ihre einzige Umsetzung.
/// </summary>
public sealed record GerufeneUmsetzung(Type Funktion, string Name, Type Typ, Action<IServiceCollection> Binde);

/// <summary>Ein Konfig-Record (aus <c>GeneratedGerufene.Konfigs</c>): Typ, Abschnitt unter <c>Konfig</c> und generierte Bindung.</summary>
public sealed record KonfigRecord(Type Typ, string Name, Func<IConfigurationSection, object> Binde);

/// <summary>Liest ein Feld eines Konfig-Records streng (fehlt es oder passt der Wert nicht, ist das ein Boot-Fehler mit Ort).</summary>
public static class KonfigLeser
{
    private static string Wert(IConfigurationSection s, string feld) =>
        s[feld] ?? throw new BootFehler($"{s.Path}:{feld} fehlt.", "host:" + Funktionsvertrag.KonfigAbschnitt);

    private static T Parse<T>(IConfigurationSection s, string feld, Func<string, (bool, T)> parse, string art)
    {
        var w = Wert(s, feld);
        var (ok, v) = parse(w);
        return ok ? v : throw new BootFehler($"{s.Path}:{feld} = '{w}' ist kein(e) {art}.", "host:" + Funktionsvertrag.KonfigAbschnitt);
    }

    public static bool Hat(IConfigurationSection s, string feld) => s[feld] != null || s.GetSection(feld).GetChildren().Any();

    public static string Text(IConfigurationSection s, string feld) => Wert(s, feld);
    public static bool JaNein(IConfigurationSection s, string feld) => Parse(s, feld, w => (bool.TryParse(w, out var v), v), "Wahrheitswert");
    public static int Zahl(IConfigurationSection s, string feld) =>
        Parse(s, feld, w => (int.TryParse(w, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v), v), "ganze Zahl");
    public static long GrosseZahl(IConfigurationSection s, string feld) =>
        Parse(s, feld, w => (long.TryParse(w, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v), v), "ganze Zahl");
    public static double Kommazahl(IConfigurationSection s, string feld) =>
        Parse(s, feld, w => (double.TryParse(w, NumberStyles.Float, CultureInfo.InvariantCulture, out var v), v), "Zahl");
    public static decimal Dezimal(IConfigurationSection s, string feld) =>
        Parse(s, feld, w => (decimal.TryParse(w, NumberStyles.Number, CultureInfo.InvariantCulture, out var v), v), "Zahl");
    public static TimeSpan Dauer(IConfigurationSection s, string feld) =>
        Parse(s, feld, w => (TimeSpan.TryParse(w, CultureInfo.InvariantCulture, out var v), v), "Dauer (hh:mm:ss)");
    public static Guid Kennung(IConfigurationSection s, string feld) => Parse(s, feld, w => (Guid.TryParse(w, out var v), v), "Guid");
    public static T Wahl<T>(IConfigurationSection s, string feld) where T : struct, Enum =>
        Parse(s, feld, w => (Enum.TryParse<T>(w, ignoreCase: false, out var v) && Enum.IsDefined(v), v), "Wert von " + typeof(T).Name);
    public static string[] Texte(IConfigurationSection s, string feld)
    {
        var kinder = s.GetSection(feld).GetChildren().Select(k => k.Value ?? "").ToArray();
        return kinder.Length > 0 || s[feld] == null ? kinder : [s[feld]!];
    }

    /// <summary>Ein Feld, dessen Typ nicht bindbar ist (CQRS091 bricht schon den Build) — hier nur der Vollständigkeit halber.</summary>
    public static T NichtBindbar<T>(IConfigurationSection s, string feld) =>
        throw new BootFehler($"{s.Path}:{feld}: Typ {typeof(T).Name} ist nicht aus der Konfig bindbar (CQRS091).", "host:" + Funktionsvertrag.KonfigAbschnitt);

    /// <summary>Unbekannte Felder sind ein Fehler (wie bei allen Abschnitten der Umgebung).</summary>
    public static void NurBekannte(IConfigurationSection s, params string[] felder)
    {
        var fremd = s.GetChildren().Select(k => k.Key).Where(k => !felder.Contains(k, StringComparer.Ordinal)).ToList();
        if (fremd.Count > 0)
            throw new BootFehler($"{s.Path}: unbekannte Felder {string.Join(", ", fremd)} (bekannt: {string.Join(", ", felder)}).", "host:" + Funktionsvertrag.KonfigAbschnitt);
    }
}

public static class GerufeneExtensions
{
    /// <summary>
    /// Bindet die gerufenen Funktionen und Konfig-Records, die die Funktionen im Host brauchen (transitiv über die Umsetzungen), nach den
    /// Abschnitten <c>Funktionen</c> und <c>Konfig</c> der Umgebung. Muss NACH <c>BindeFunktionen</c> stehen.
    /// </summary>
    public static IServiceCollection BindeGerufene(this IServiceCollection services, UmgebungKonfig u, IConfiguration konfig)
        => services.BindeGerufene(u.Funktionen, konfig.GetSection(Funktionsvertrag.KonfigAbschnitt));

    /// <summary>Wie oben, mit der Wahl und dem Konfig-Abschnitt direkt — gebunden wird für die Funktionen, die nach den schon registrierten
    /// Bindungen (<c>BindeFunktionen</c>) im Host rechnen, aus den generierten Tabellen der angemeldeten Anwendung.</summary>
    public static IServiceCollection BindeGerufene(this IServiceCollection services, IReadOnlyDictionary<string, FunktionsKonfig> wahl,
        IConfigurationSection konfigAbschnitt)
    {
        // Die Funktionen, die im Host laufen: ihre Umsetzung (die Klasse selbst bzw. die gewählte Implementierung der Schnittstelle).
        var bindungen = services.Select(d => d.ImplementationInstance).OfType<BausteinBindung>().ToList();
        var umsetzungen = new List<Type>();
        foreach (var b in Anwendung.Funktionen.Bausteine)
            if (FunktionsBausteinWiring.Gebunden(b, bindungen) is { Laufort: FunktionsLaufort.Host } bindung
                && b.Umsetzungen.Where(x => bindung.Umsetzung is null || x.Name == bindung.Umsetzung).ToList() is [var umsetzung])
                umsetzungen.Add(umsetzung.Typ);
        var gerufen = Anwendung.Gerufene.Namen;
        return services.BindeGerufene(wahl.Where(w => gerufen.ContainsKey(w.Key)).ToDictionary(w => w.Key, w => w.Value.Implementierung),
            konfigAbschnitt, umsetzungen, Anwendung.Gerufene.Umsetzungen, Anwendung.Gerufene.Braucht, Anwendung.Gerufene.Konfigs);
    }

    /// <param name="wahl">Je gerufene Funktion die gewählte Umsetzung (<c>Funktionen:&lt;Name&gt;:Implementierung</c>), null = die einzige.</param>
    /// <param name="umsetzungen">Die Klassen der Funktionen, die auf diesem Knoten im Host rechnen — von ihrem Bedarf aus wird gebunden.</param>
    public static IServiceCollection BindeGerufene(this IServiceCollection services, IReadOnlyDictionary<string, string?> wahl,
        IConfigurationSection konfigAbschnitt, IReadOnlyCollection<Type> umsetzungen,
        IReadOnlyList<GerufeneUmsetzung> alleUmsetzungen, IReadOnlyDictionary<Type, Type[]> braucht, IReadOnlyList<KonfigRecord> konfigs)
    {
        // 1. Die Umgebung nennt nur bekannte Konfig-Records (die Namen unter Funktionen prüft BindeFunktionen).
        foreach (var k in konfigAbschnitt.GetChildren().Where(k => konfigs.All(r => r.Name != k.Key)))
            throw new BootFehler($"{Funktionsvertrag.KonfigAbschnitt}:{k.Key} — kein Konfig-Record dieses Namens (bekannt: "
                + string.Join(", ", konfigs.Select(r => r.Name).Order().DefaultIfEmpty("keine")) + ").", "host:" + Funktionsvertrag.KonfigAbschnitt);

        // 2. Je gerufene Funktion die gewählte Umsetzung (oder warum es keine gibt).
        const string F = UmgebungsAbschnitte.Funktionen;
        string Karte(Type f) => "funktion:" + f.Name;
        GerufeneUmsetzung Waehle(Type funktion)
        {
            var alle = alleUmsetzungen.Where(i => i.Funktion == funktion).ToList();
            var gewaehlt = wahl.GetValueOrDefault(funktion.Name);
            var passend = gewaehlt == null ? alle : alle.Where(i => i.Name == gewaehlt).ToList();
            if (passend.Count == 1) return passend[0];
            var gefunden = string.Join(", ", alle.Select(i => i.Name).DefaultIfEmpty("keine"));
            throw new BootFehler(gewaehlt != null
                ? $"{F}:{funktion.Name}:Implementierung = '{gewaehlt}' gibt es nicht (gefunden: {gefunden})."
                : alle.Count == 0
                    ? $"Die gerufene Funktion '{funktion.Name}' wird gebraucht, hat aber keine Umsetzung (eine öffentliche Klasse, die {funktion.Name} implementiert)."
                    : $"Die gerufene Funktion '{funktion.Name}' hat mehrere Umsetzungen ({gefunden}) — die Umgebung wählt eine: {F}:{funktion.Name}:Implementierung.",
                Karte(funktion));
        }

        // 3. Vom Bedarf der Funktionen im Host aus, transitiv; ein Kreis über gerufene Funktionen ist ein Fehler.
        var start = umsetzungen.Distinct().ToList();
        var gebunden = new Dictionary<Type, GerufeneUmsetzung>();
        var konfigGebunden = new HashSet<Type>();
        var stapel = new List<Type>();
        void Besuche(Type impl)
        {
            foreach (var t in braucht.GetValueOrDefault(impl) ?? [])
            {
                if (konfigs.FirstOrDefault(r => r.Typ == t) is { } rec)
                {
                    if (!konfigGebunden.Add(t)) continue;
                    var abschnitt = konfigAbschnitt.GetSection(rec.Name);
                    if (!abschnitt.Exists())
                        throw new BootFehler($"{Funktionsvertrag.KonfigAbschnitt}:{rec.Name} fehlt — {impl.Name} braucht den Konfig-Record {rec.Name}.", "host:" + Funktionsvertrag.KonfigAbschnitt);
                    var wert = rec.Binde(abschnitt);
                    services.AddSingleton(rec.Typ, wert);
                    continue;
                }
                if (stapel.Contains(t))
                    throw new BootFehler("Gerufene Funktionen im Kreis: " + string.Join(" → ", stapel.SkipWhile(x => x != t).Append(t).Select(x => x.Name))
                        + " — eine Umsetzung darf sich nicht über andere gerufene Funktionen selbst brauchen.", Karte(t));
                if (gebunden.ContainsKey(t)) continue;
                var i = Waehle(t);
                stapel.Add(t);
                Besuche(i.Typ);
                stapel.RemoveAt(stapel.Count - 1);
                gebunden[t] = i;
                i.Binde(services);
            }
        }
        foreach (var impl in start) Besuche(impl);
        if (gebunden.Count + konfigGebunden.Count > 0)
            Console.WriteLine($"[CQRS] Gerufen: {string.Join(", ", gebunden.Select(x => x.Key.Name + " → " + x.Value.Name).DefaultIfEmpty("keine"))}"
                + $" · Konfig: {string.Join(", ", konfigGebunden.Select(t => t.Name).DefaultIfEmpty("keine"))}");
        return services;
    }
}
