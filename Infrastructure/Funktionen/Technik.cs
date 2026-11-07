using System.Globalization;
using Abstractions;
using Infrastructure.Umgebung;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// BINDUNG DER TECHNIK (docs/06 §6.5): WAS es gibt, ist Code — Technik (ohne Marker: was der Konstruktor einer Funktion nimmt), ihre Umsetzungen und
// die Konfig-Records in den Konstruktoren (generierte Tabelle `GeneratedTechnik`). WELCHE Umsetzung läuft und mit welchen Werten, sagt der Knoten
// einer Instanz (`Technik`, die Felder seiner Konfig-Records). Der Start prüft je Knoten, der im Host rechnet: fehlt etwas, ist es mehrdeutig, unbekannt
// oder zyklisch, startet nichts. Gebaut wird je Aktivierung im Bereich des Knotens (`KnotenBereich`).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Eine Umsetzung einer Technik (aus <c>GeneratedTechnik.Umsetzungen</c>): Name, Typ und generierte Fabrik — bei einer Klasse ist sie selbst ihre
/// einzige Umsetzung; mit <see cref="Zustand"/>, wenn ihr Konstruktor <c>IZustand&lt;T&gt;</c> nimmt.
/// </summary>
public sealed record TechnikUmsetzung(Type Technik, string Name, Type Typ, Func<IServiceProvider, object> Fabrik, FunktionsZustandsform? Zustand = null);

/// <summary>
/// Die Zustände der Technik EINER Aktivierung (im DI-Bereich der Aktivierung, docs/04 §4.4): die generierte Fabrik einer Umsetzung mit
/// <c>IZustand&lt;T&gt;</c> holt ihren Zustand hier, unter dem Namen der Technik (je Bereich einer — dieselbe Technik hat in einer
/// Aktivierung eine Instanz). Die Aktivierung lädt und sichert sie zusammen mit dem Zustand ihrer Funktion (<see cref="FunktionsAktivierung"/>).
/// </summary>
public sealed class AktivierungsZustaende
{
    private readonly Dictionary<string, (FunktionsZustandsform Form, object Zustand)> _je = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, byte[]>? _gesichert;

    /// <summary>Die gesicherten Zustände (Name der Technik → Bytes), mit denen eine Technik beim Bau beginnt (statt frisch).</summary>
    public void Vorgabe(IReadOnlyDictionary<string, byte[]> gesichert) => _gesichert = gesichert;

    /// <summary>Die Zustände des Bereichs — gebaut wird eine Technik mit Zustand nur in der Aktivierung einer Funktion.</summary>
    public static AktivierungsZustaende Von(IServiceProvider sp) => sp.GetService<AktivierungsZustaende>()
        ?? throw new InvalidOperationException("Eine Technik mit Zustand (IZustand<T>) wird nur in der Aktivierung einer Funktion gebaut (BindeTechnik).");

    /// <summary>Der Zustand der Technik <paramref name="funktion"/> in dieser Aktivierung (beim ersten Mal frisch).</summary>
    public object Hole(string funktion, FunktionsZustandsform form)
    {
        if (!_je.TryGetValue(funktion, out var z))
        {
            _je[funktion] = z = (form, form.Neu());
            if (_gesichert?.TryGetValue(funktion, out var daten) == true) form.Lies(z.Zustand, daten);
        }
        return z.Zustand;
    }

    /// <summary>Name der Technik → Form und Zustand.</summary>
    public IReadOnlyDictionary<string, (FunktionsZustandsform Form, object Zustand)> Alle => _je;
}

/// <summary>Der Knoten einer Instanz, für den eine Aktivierung baut: Instanz, Knoten und seine Konfig (Technik-Wahl, Werte der Konfig-Records).</summary>
public sealed record KnotenKontext(string Instanz, string Knoten, KnotenKonfig Konfig)
{
    public string Ort => Instanz + "/" + Knoten;
}

/// <summary>
/// Der BEREICH eines Knotens (im DI-Bereich der Aktivierung): die generierten Registrierungen der Konfig-Records und der Technik lesen hier, für welchen
/// Knoten gebaut wird — die Werte seiner Konfig-Records und welche Umsetzung er je Technik gewählt hat. Ohne Knoten (ein Prüfstand) gilt die einzige
/// Umsetzung, ein Konfig-Record ist dann ein Fehler.
/// </summary>
public sealed class KnotenBereich
{
    private readonly Dictionary<Type, object> _konfigs = new();

    /// <summary>Der Knoten, für den dieser Bereich baut (die Aktivierung setzt ihn vor dem Bau).</summary>
    public KnotenKontext? Kontext { get; set; }

    public static KnotenBereich Von(IServiceProvider sp) => sp.GetService<KnotenBereich>()
        ?? throw new InvalidOperationException("Technik und Konfig-Records werden nur im Bereich eines Knotens gebaut (BindeTechnik).");

    /// <summary>Der Konfig-Record <paramref name="rec"/> aus den Werten des Knotens (je Bereich einmal gebunden).</summary>
    public object Konfig(KonfigRecord rec)
    {
        if (_konfigs.TryGetValue(rec.Typ, out var k)) return k;
        var kontext = Kontext ?? throw new BootFehler($"Der Konfig-Record {rec.Name} wird außerhalb eines Knotens gebraucht — Werte stehen am Knoten einer Instanz.",
            "host:Graphen");
        return _konfigs[rec.Typ] = rec.Binde(Abschnitt(kontext, rec));
    }

    /// <summary>Die Werte eines Knotens, die zu einem Konfig-Record gehören, als Abschnitt (Pfad <c>instanz/knoten:Feld</c> für die Meldungen).</summary>
    public static IConfigurationSection Abschnitt(KnotenKontext kontext, KonfigRecord rec)
    {
        var werte = kontext.Konfig.Werte.Where(w => rec.Felder.Contains(w.Key.Split(':')[0], StringComparer.Ordinal))
            .Select(w => new KeyValuePair<string, string?>(kontext.Ort + ":" + w.Key, w.Value));
        return new ConfigurationBuilder().AddInMemoryCollection(werte).Build().GetSection(kontext.Ort);
    }

    /// <summary>Baut die Technik <paramref name="technik"/> mit der Umsetzung, die der Knoten gewählt hat (bei einer einzigen: diese).</summary>
    public object Technik(Type technik, IReadOnlyList<TechnikUmsetzung> umsetzungen, IServiceProvider sp)
    {
        var wahl = Kontext?.Konfig.Technik.GetValueOrDefault(technik.Name);
        var u = umsetzungen.Count == 1 && wahl is null ? umsetzungen[0]
            : umsetzungen.FirstOrDefault(x => x.Name == wahl)
              ?? throw new BootFehler($"{Kontext?.Ort ?? "?"}: keine Umsetzung von {technik.Name} gewählt (Technik:{technik.Name}; gefunden: "
                  + string.Join(", ", umsetzungen.Select(x => x.Name)) + ").", "funktion:" + technik.Name);
        return u.Fabrik(sp);
    }
}

/// <summary>Je Knoten (Ort <c>instanz/knoten</c>), ob eine gewählte Technik in seiner Konstruktor-Kette Zustand trägt (beim Start ermittelt).</summary>
public sealed class TechnikWahl(IReadOnlyDictionary<string, bool> jeOrt)
{
    public bool MitZustand(string ort) => jeOrt.GetValueOrDefault(ort);
}

/// <summary>Ein Konfig-Record (aus <c>GeneratedTechnik.Konfigs</c>): Typ, einfacher Name, seine Felder und die generierte Bindung.</summary>
public sealed record KonfigRecord(Type Typ, string Name, IReadOnlyList<string> Felder, Func<IConfigurationSection, object> Binde);

/// <summary>Liest ein Feld eines Konfig-Records streng (fehlt es oder passt der Wert nicht, ist das ein Boot-Fehler mit Ort).</summary>
public static class KonfigLeser
{
    private static string Wert(IConfigurationSection s, string feld) =>
        s[feld] ?? throw new BootFehler($"{s.Path}:{feld} fehlt.", "host:" + UmgebungsAbschnitte.Graphen);

    private static T Parse<T>(IConfigurationSection s, string feld, Func<string, (bool, T)> parse, string art)
    {
        var w = Wert(s, feld);
        var (ok, v) = parse(w);
        return ok ? v : throw new BootFehler($"{s.Path}:{feld} = '{w}' ist kein(e) {art}.", "host:" + UmgebungsAbschnitte.Graphen);
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
        throw new BootFehler($"{s.Path}:{feld}: Typ {typeof(T).Name} ist nicht aus der Konfig bindbar (CQRS091).", "host:" + UmgebungsAbschnitte.Graphen);

    /// <summary>Unbekannte Felder sind ein Fehler (wie bei allen Abschnitten der Umgebung).</summary>
    public static void NurBekannte(IConfigurationSection s, params string[] felder)
    {
        var fremd = s.GetChildren().Select(k => k.Key).Where(k => !felder.Contains(k, StringComparer.Ordinal)).ToList();
        if (fremd.Count > 0)
            throw new BootFehler($"{s.Path}: unbekannte Felder {string.Join(", ", fremd)} (bekannt: {string.Join(", ", felder)}).", "host:" + UmgebungsAbschnitte.Graphen);
    }
}

public static class TechnikExtensions
{
    /// <summary>
    /// Bindet die Technik und die Konfig-Records, die die Knoten der Instanzen im Host brauchen (transitiv über die Umsetzungen), aus den generierten
    /// Tabellen der angemeldeten Anwendung. Muss NACH den Instanzen stehen.
    /// </summary>
    public static IServiceCollection BindeTechnik(this IServiceCollection services, GraphInstanzen instanzen)
        => services.BindeTechnik(HostKnoten(instanzen), Anwendung.Technik.Umsetzungen, Anwendung.Technik.Braucht, Anwendung.Technik.Konfigs);

    /// <summary>Die Knoten der Instanzen, die im Host rechnen: ihr Kontext (Instanz, Knoten, Konfig) und die Klasse ihrer Funktion.</summary>
    public static IEnumerable<(KnotenKontext Knoten, Type Umsetzung)> HostKnoten(GraphInstanzen instanzen) =>
        from x in FunktionsBausteinWiring.Knoten(instanzen)
        select (new KnotenKontext(x.Instanz.Name, x.Knoten.Name, x.Konfig), x.Baustein.Typ);

    /// <param name="knoten">Die Knoten im Host — von der Klasse ihrer Umsetzung aus wird gebunden, je Knoten mit seiner Wahl und seinen Werten.</param>
    public static IServiceCollection BindeTechnik(this IServiceCollection services, IEnumerable<(KnotenKontext Knoten, Type Umsetzung)> knoten,
        IReadOnlyList<TechnikUmsetzung> alleUmsetzungen, IReadOnlyDictionary<Type, Type[]> braucht, IReadOnlyList<KonfigRecord> konfigs)
    {
        var technikGebraucht = new HashSet<Type>();
        var konfigGebraucht = new HashSet<Type>();
        var zustandJeOrt = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (kontext, umsetzung) in knoten)
        {
            var kk = kontext.Konfig;
            var records = new List<KonfigRecord>();
            var zustand = false;
            var stapel = new List<Type>();
            string Karte(Type t) => "funktion:" + t.Name;
            void Besuche(Type impl)
            {
                foreach (var t in braucht.GetValueOrDefault(impl) ?? [])
                {
                    if (konfigs.FirstOrDefault(r => r.Typ == t) is { } rec)
                    {
                        if (records.Contains(rec)) continue;
                        records.Add(rec);
                        konfigGebraucht.Add(t);
                        continue;
                    }
                    if (stapel.Contains(t))
                        throw new BootFehler("Technik im Kreis: " + string.Join(" → ", stapel.SkipWhile(x => x != t).Append(t).Select(x => x.Name))
                            + " — eine Umsetzung darf sich nicht über andere Technik selbst brauchen.", Karte(t));
                    var alle = alleUmsetzungen.Where(u => u.Technik == t).ToList();
                    var wahl = kk.Technik.GetValueOrDefault(t.Name);
                    var u = wahl is null && alle.Count == 1 ? alle[0] : alle.FirstOrDefault(x => x.Name == wahl);
                    if (u is null)
                        throw new BootFehler(wahl != null
                            ? $"{kontext.Ort}: Technik:{t.Name} = '{wahl}' gibt es nicht (gefunden: {string.Join(", ", alle.Select(x => x.Name).DefaultIfEmpty("keine"))})."
                            : alle.Count == 0
                                ? $"{kontext.Ort}: die Technik '{t.Name}' wird gebraucht, hat aber keine Umsetzung (eine öffentliche Klasse, die {t.Name} implementiert)."
                                : $"{kontext.Ort}: die Technik '{t.Name}' hat mehrere Umsetzungen ({string.Join(", ", alle.Select(x => x.Name))}) — der Knoten wählt "
                                  + $"eine: Technik:{t.Name}.", Karte(t));
                    technikGebraucht.Add(t);
                    zustand |= u.Zustand is not null;
                    stapel.Add(t);
                    Besuche(u.Typ);
                    stapel.RemoveAt(stapel.Count - 1);
                }
            }
            Besuche(umsetzung);
            zustandJeOrt[kontext.Ort] = zustand;

            // Die Technik-Wahl nennt nur Technik dieses Knotens; die Werte nur Felder seiner Konfig-Records; jeder Record bindet (fehlt ein Feld, scheitert es hier).
            foreach (var fremd in kk.Technik.Keys.Where(n => !alleUmsetzungen.Any(u => u.Technik.Name == n)))
                throw new BootFehler($"{kontext.Ort}: Technik:{fremd} — keine Technik dieses Namens.", "instanz:" + kontext.Instanz);
            var felder = records.SelectMany(r => r.Felder).ToHashSet(StringComparer.Ordinal);
            var unbekannt = kk.Werte.Keys.Select(w => w.Split(':')[0]).Distinct().Where(f => !felder.Contains(f)).ToList();
            if (unbekannt.Count > 0)
                throw new BootFehler($"{kontext.Ort}: unbekannte Felder {string.Join(", ", unbekannt)} (bekannt: "
                    + string.Join(", ", KnotenKonfig.Rahmenfelder.Concat(felder).Order(StringComparer.Ordinal)) + ").", "instanz:" + kontext.Instanz);
            foreach (var rec in records) rec.Binde(KnotenBereich.Abschnitt(kontext, rec));
        }

        foreach (var t in technikGebraucht)
        {
            var umsetzungen = alleUmsetzungen.Where(u => u.Technik == t).ToList();
            services.AddScoped(t, sp => KnotenBereich.Von(sp).Technik(t, umsetzungen, sp));
        }
        foreach (var rec in konfigs.Where(r => konfigGebraucht.Contains(r.Typ)))
            services.AddScoped(rec.Typ, sp => KnotenBereich.Von(sp).Konfig(rec));
        services.AddSingleton(new TechnikWahl(zustandJeOrt));
        services.TryAddScoped<AktivierungsZustaende>();
        services.TryAddScoped<KnotenBereich>();
        if (technikGebraucht.Count + konfigGebraucht.Count > 0)
            Console.WriteLine($"[CQRS] Technik: {string.Join(", ", technikGebraucht.Select(t => t.Name).Order().DefaultIfEmpty("keine"))}"
                + $" · Konfig: {string.Join(", ", konfigs.Where(r => konfigGebraucht.Contains(r.Typ)).Select(r => r.Name).DefaultIfEmpty("keine"))}");
        return services;
    }
}
