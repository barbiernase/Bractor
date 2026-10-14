using System.Globalization;
using Abstractions;
using Infrastructure.Funktionen;
using Infrastructure.Umgebung;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Komposition;

namespace Infrastructure.Dienste;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DIENST-BINDUNG (docs/konzept-werkbank.md §12.3 E11): WAS es gibt, ist Code — Dienste (`interface IX : IDienst`), ihre
// Implementierungen und die Konfig-Records in den Konstruktoren (generierte Tabelle `GeneratedDienste`). WELCHE Implementierung läuft
// und mit welchen Werten, sagt die Umgebung (`Dienste:<IX>:Implementierung`, `Konfig:<Record>`). Der Start prüft gegen den Bedarf der
// gebundenen Funktions-Implementierungen: fehlt etwas, ist es mehrdeutig oder zyklisch, startet nichts.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Eine Implementierung eines Dienstes (aus <c>GeneratedDienste.Implementierungen</c>): Name, Typ und generierte Bindung.</summary>
public sealed record DienstImplementierung(Type Dienst, string Name, Type Impl, Action<IServiceCollection> Binde);

/// <summary>Ein Konfig-Record (aus <c>GeneratedDienste.Konfigs</c>): Typ, Abschnitt unter <c>Konfig</c> und generierte Bindung.</summary>
public sealed record KonfigRecord(Type Typ, string Name, Func<IConfigurationSection, object> Binde);

/// <summary>Liest ein Feld eines Konfig-Records streng (fehlt es oder passt der Wert nicht, ist das ein Boot-Fehler mit Ort).</summary>
public static class KonfigLeser
{
    private static string Wert(IConfigurationSection s, string feld) =>
        s[feld] ?? throw new BootFehler($"{s.Path}:{feld} fehlt.", "host:" + Dienstvertrag.KonfigAbschnitt);

    private static T Parse<T>(IConfigurationSection s, string feld, Func<string, (bool, T)> parse, string art)
    {
        var w = Wert(s, feld);
        var (ok, v) = parse(w);
        return ok ? v : throw new BootFehler($"{s.Path}:{feld} = '{w}' ist kein(e) {art}.", "host:" + Dienstvertrag.KonfigAbschnitt);
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
        throw new BootFehler($"{s.Path}:{feld}: Typ {typeof(T).Name} ist nicht aus der Konfig bindbar (CQRS091).", "host:" + Dienstvertrag.KonfigAbschnitt);

    /// <summary>Unbekannte Felder sind ein Fehler (wie bei allen Abschnitten der Umgebung).</summary>
    public static void NurBekannte(IConfigurationSection s, params string[] felder)
    {
        var fremd = s.GetChildren().Select(k => k.Key).Where(k => !felder.Contains(k, StringComparer.Ordinal)).ToList();
        if (fremd.Count > 0)
            throw new BootFehler($"{s.Path}: unbekannte Felder {string.Join(", ", fremd)} (bekannt: {string.Join(", ", felder)}).", "host:" + Dienstvertrag.KonfigAbschnitt);
    }
}

public static class DienstExtensions
{
    /// <summary>
    /// Bindet die Dienste und Konfig-Records, die die gebundenen Funktions-Implementierungen brauchen (transitiv über die Dienst-Implementierungen),
    /// nach den Abschnitten <c>Dienste</c> und <c>Konfig</c> der Umgebung. Muss NACH <c>BindeFunktionen</c> stehen.
    /// </summary>
    public static IServiceCollection BindeDienste(this IServiceCollection services, UmgebungKonfig u, IConfiguration konfig)
        => services.BindeDienste(u.Dienste, konfig.GetSection(Dienstvertrag.KonfigAbschnitt),
            Anwendung.Dienste.Namen, Anwendung.Dienste.Implementierungen, Anwendung.Dienste.Braucht, Anwendung.Dienste.Konfigs);

    public static IServiceCollection BindeDienste(this IServiceCollection services, IReadOnlyDictionary<string, DienstKonfig> wahl,
        IConfigurationSection konfigAbschnitt, IReadOnlyDictionary<string, Type> namen, IReadOnlyList<DienstImplementierung> implementierungen,
        IReadOnlyDictionary<Type, Type[]> braucht, IReadOnlyList<KonfigRecord> konfigs)
    {
        // 1. Die Umgebung nennt nur bekannte Dienste und Konfig-Records.
        foreach (var name in wahl.Keys.Where(n => !namen.ContainsKey(n)))
            throw new BootFehler($"{Dienstvertrag.DiensteAbschnitt}:{name} — kein Dienst dieses Namens (bekannt: "
                + string.Join(", ", namen.Keys.Order().DefaultIfEmpty("keine")) + ").", "host:" + Dienstvertrag.DiensteAbschnitt);
        foreach (var k in konfigAbschnitt.GetChildren().Where(k => konfigs.All(r => r.Name != k.Key)))
            throw new BootFehler($"{Dienstvertrag.KonfigAbschnitt}:{k.Key} — kein Konfig-Record dieses Namens (bekannt: "
                + string.Join(", ", konfigs.Select(r => r.Name).Order().DefaultIfEmpty("keine")) + ").", "host:" + Dienstvertrag.KonfigAbschnitt);

        // 2. Je Dienst die gewählte Implementierung (oder warum es keine gibt).
        string Karte(Type d) => "dienst:" + d.Name;
        DienstImplementierung Waehle(Type dienst)
        {
            var alle = implementierungen.Where(i => i.Dienst == dienst).ToList();
            var gewaehlt = wahl.GetValueOrDefault(dienst.Name)?.Implementierung;
            var passend = gewaehlt == null ? alle : alle.Where(i => i.Name == gewaehlt).ToList();
            if (passend.Count == 1) return passend[0];
            var gefunden = string.Join(", ", alle.Select(i => i.Name).DefaultIfEmpty("keine"));
            throw new BootFehler(gewaehlt != null
                ? $"{Dienstvertrag.DiensteAbschnitt}:{dienst.Name}:Implementierung = '{gewaehlt}' gibt es nicht (gefunden: {gefunden})."
                : alle.Count == 0
                    ? $"Der Dienst '{dienst.Name}' wird gebraucht, hat aber keine Implementierung (eine öffentliche Klasse, die {dienst.Name} implementiert)."
                    : $"Der Dienst '{dienst.Name}' hat mehrere Implementierungen ({gefunden}) — die Umgebung wählt eine: {Dienstvertrag.DiensteAbschnitt}:{dienst.Name}:Implementierung.",
                Karte(dienst));
        }

        // 3. Vom Bedarf der gebundenen Funktions-Implementierungen aus, transitiv; ein Kreis über Dienste ist ein Fehler.
        var start = services.Select(d => d.ImplementationInstance).OfType<FunktionsBindung>().Where(b => b.Impl != null).Select(b => b.Impl!).Distinct().ToList();
        var gebunden = new Dictionary<Type, DienstImplementierung>();
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
                        throw new BootFehler($"{Dienstvertrag.KonfigAbschnitt}:{rec.Name} fehlt — {impl.Name} braucht den Konfig-Record {rec.Name}.", "host:" + Dienstvertrag.KonfigAbschnitt);
                    var wert = rec.Binde(abschnitt);
                    services.AddSingleton(rec.Typ, wert);
                    continue;
                }
                if (stapel.Contains(t))
                    throw new BootFehler("Dienste im Kreis: " + string.Join(" → ", stapel.SkipWhile(x => x != t).Append(t).Select(x => x.Name))
                        + " — eine Implementierung darf sich nicht über andere Dienste selbst brauchen.", Karte(t));
                if (gebunden.ContainsKey(t)) continue;
                var i = Waehle(t);
                stapel.Add(t);
                Besuche(i.Impl);
                stapel.RemoveAt(stapel.Count - 1);
                gebunden[t] = i;
                i.Binde(services);
            }
        }
        foreach (var impl in start) Besuche(impl);
        if (gebunden.Count + konfigGebunden.Count > 0)
            Console.WriteLine($"[CQRS] Dienste: {string.Join(", ", gebunden.Select(x => x.Key.Name + " → " + x.Value.Name).DefaultIfEmpty("keine"))}"
                + $" · Konfig: {string.Join(", ", konfigGebunden.Select(t => t.Name).DefaultIfEmpty("keine"))}");
        return services;
    }
}
