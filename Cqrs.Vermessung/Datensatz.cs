using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Cqrs.Vermessung;

/// <summary>
/// S4 Datensatz (Beispieldomäne): als KI-Operator einen Datensatz anlegen, die Range über alle Bildpaare aufnehmen (Fluss mit
/// <c>IRangeSuche</c>, Lese-Fähigkeit) und einfrieren (Fluss mit <c>IMitgliederEinfrieren</c>, großes Event). Sichtbar über
/// <c>HoleDatensatz</c>: erst alle Paare als Mitglieder, dann Status <c>Eingefroren</c>. Kennzahl ist die Dauer bzw. Paare je Sekunde.
/// </summary>
internal sealed class Datensatz
{
    public const string TokenKiOperator = "vermessung-kioperator";
    private readonly Eingang _eingang;
    public Guid Id { get; } = Guid.NewGuid();

    public Datensatz(Eingang eingang) => _eingang = eingang;

    public sealed record Zeiten(double AufnahmeS, double EinfrierenS, int Mitglieder);

    public async Task<Zeiten> LaufeAsync(int erwartetePaare, TimeSpan frist)
    {
        Pruefe(await _eingang.CommandAsync("ErstelleDatensatz", new JsonObject { ["AggregateId"] = Id.ToString(), ["Name"] = "Vermessung S4" }, TokenKiOperator));
        var sw = Stopwatch.StartNew();
        Pruefe(await _eingang.CommandAsync("FuegeRangeHinzu", new JsonObject { ["AggregateId"] = Id.ToString(), ["Kriterien"] = new JsonObject() }, TokenKiOperator));
        await WarteAsync(d => (int?)d["AnzahlMitglieder"] >= erwartetePaare, frist, "Range nicht vollständig aufgenommen");
        var aufnahme = sw.Elapsed.TotalSeconds;
        sw.Restart();
        Pruefe(await _eingang.CommandAsync("FriereEin", new JsonObject { ["AggregateId"] = Id.ToString() }, TokenKiOperator));
        var d = await WarteAsync(d => (string?)d["Status"] == "Eingefroren" && (int?)d["EingefroreneVersion"] == 1, frist, "nicht eingefroren");
        return new Zeiten(aufnahme, sw.Elapsed.TotalSeconds, (int?)d["AnzahlMitglieder"] ?? -1);
    }

    private static void Pruefe(Eingang.Antwort a)
    {
        if (!a.Ok) throw new InvalidOperationException($"S4: Command abgewiesen ({a.Status}): {a.Fehler}");
    }

    private async Task<JsonNode> WarteAsync(Func<JsonNode, bool> bedingung, TimeSpan frist, string meldung)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < frist)
        {
            var a = await _eingang.QueryAsync("HoleDatensatz", new JsonObject { ["DatensatzId"] = Id.ToString() }, TokenKiOperator);
            if (a?["typ"]?.ToString() == "DatensatzAntwort" && a["daten"] is { } d && bedingung(d)) return d;
            await Task.Delay(100);
        }
        throw new TimeoutException($"S4: {meldung} nach {frist.TotalSeconds:0} s");
    }

    /// <summary>Exactly-once (§0.2): Mitglieder des eingefrorenen Datensatzes = aufgenommene Paare, genau ein DatensatzEingefroren.</summary>
    /// <param name="paareImLog">Paare im Log beim Start von S4; läuft der Rückstand aus S1 noch, kommen während der Aufnahme weitere hinzu —
    /// dann liegt die Zahl der aufgenommenen Paare zwischen diesem Stand und dem Stand bei der Prüfung.</param>
    public async Task<List<Pruefung>> PruefeAsync(Datenbank db, int paareImLog)
    {
        var paareJetzt = await db.ZahlAsync($"select count(*) from {db.Schema}.mt_events where type = 'image_pair_erstellt'");
        var s = db.Schema;
        var aufgenommen = await db.ZahlAsync(
            $"select coalesce(sum(jsonb_array_length(data->'ImagePairIds')), 0) from {s}.mt_events where stream_id = @d and type = 'paare_aufgenommen'", ("d", Id));
        var eingefroren = await db.ZahlAsync($"select count(*) from {s}.mt_events where stream_id = @d and type = 'datensatz_eingefroren'", ("d", Id));
        var mitglieder = await db.ZahlAsync(
            $"select coalesce(max(jsonb_array_length(data->'Mitglieder')), -1) from {s}.mt_events where stream_id = @d and type = 'datensatz_eingefroren'", ("d", Id));
        var antwort = await _eingang.QueryAsync("HoleDatensatz", new JsonObject { ["DatensatzId"] = Id.ToString() }, TokenKiOperator);
        return new List<Pruefung>
        {
            paareJetzt == paareImLog
                ? Pruefung.Gleich("aufgenommene Paare = Paare im Log", paareImLog, aufgenommen)
                : new Pruefung("aufgenommene Paare = Paare im Log (Rückstand offen: zwischen Start und Prüfung)",
                    aufgenommen >= paareImLog && aufgenommen <= paareJetzt, $"{paareImLog}…{paareJetzt}", aufgenommen.ToString()),
            Pruefung.Gleich("genau ein DatensatzEingefroren", 1, eingefroren),
            Pruefung.Gleich("Mitglieder des eingefrorenen Datensatzes = aufgenommene Paare", aufgenommen, mitglieder),
            Pruefung.Gleich("Read-Model: Mitglieder = aufgenommene Paare", aufgenommen, (long?)antwort?["daten"]?["AnzahlMitglieder"] ?? -1),
        };
    }
}
