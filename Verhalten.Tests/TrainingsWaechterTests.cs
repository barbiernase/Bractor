using System.Text.Json.Nodes;
using Domain.Trainingslauf;

namespace Verhalten.Tests;

/// <summary>
/// Der Trainings-Wächter als fachliches Soll: ein Training, das nach dem Beginn sechs Stunden nichts meldet, gilt genau einmal als hängen
/// geblieben; eines, das vorher endet, nie; eine Meldung nach der Markierung ändert nichts mehr. Die Meldungen kommen, wie im Betrieb, als
/// Ausgaben der Funktion ITraining von ihrem Träger draußen (docs/konzept-akteure.md §5). Die Zeit läuft über die Uhr der Sim
/// (Datenbank-Uhr samt Event-Zeitstempeln), nicht über Warten — mit eigenem Host, damit der Zeitsprung keinen anderen Ablauf trifft.
/// </summary>
[Trait("Kategorie", "Garantie")]
public sealed class TrainingsWaechterTests
{
    private static JsonObject Start(Guid lauf) => new()
    {
        ["AggregateId"] = lauf.ToString(), ["DatensatzId"] = Guid.NewGuid().ToString(), ["DatensatzVersion"] = 1,
        ["Hyperparameter"] = new JsonObject { ["Epochen"] = 1, ["LernRate"] = 0.01, ["BatchGroesse"] = 8, ["Architektur"] = "klein", ["Seed"] = 1 },
    };

    [Fact]
    public async Task Ein_Training_ohne_Meldung_bis_zur_Frist_bleibt_genau_einmal_haengen_ein_beendetes_nie()
    {
        var u = await Testumgebung.NeuAsync(Anwendung.Beispiel, "verhalten-waechter");
        await using var host = await Hostlauf.StarteAsync(u);
        using var eingang = new JsonEingang(host);
        var log = new Log(u);
        // Das Training rechnet draußen (ITraining, getragen vom TrainingsWorker): der Test ist der Träger und meldet stückweise.
        await using var worker = await RoheLeitung.VerbindeAsync(host, "TrainingsWorker", Testumgebung.TokenTrainingssystem);
        await worker.BieteAnAsync("ITraining", slots: 4);

        var beendet = Guid.NewGuid();
        var still = Guid.NewGuid();
        var laeufe = new Dictionary<Guid, Cqrs.Leitung.ArbeitsAuftrag>();
        foreach (var lauf in new[] { beendet, still })
        {
            await eingang.MussAsync("StarteTraining", Start(lauf), Testumgebung.TokenKiOperator);
            laeufe[lauf] = await worker.WarteAufEingangAsync(a => a.Stream == lauf.ToString(), TimeSpan.FromSeconds(30), "kein Eingang an das Training");
            await worker.StueckAsync(laeufe[lauf], 0, fertig: false, new MeldeTrainingBegonnen(new(lauf)));
        }
        await worker.StueckAsync(laeufe[beendet], 1, fertig: true, new MeldeTrainingGescheitert(new(beendet), "vorher zu Ende"));
        await Warte.BisAsync(async () => await log.ZahlAsync<TrainingGescheitert>(beendet) == 1 && await log.ZahlAsync<TrainingBegonnen>(still) == 1,
            TimeSpan.FromSeconds(30), "die Meldungen des Trägers wirkten nicht", host);
        await Warte.NachlaufAsync();

        await u.StelleUhrVorAsync(TimeSpan.FromHours(7));
        await Warte.BisAsync(async () => await log.ZahlAsync<TrainingHaengengeblieben>(still) == 1, TimeSpan.FromSeconds(90),
            "das stille Training wird nach der Frist nicht als hängengeblieben markiert", host);

        // Zu spät: die Meldung nach der Markierung ändert nichts mehr.
        await worker.StueckAsync(laeufe[still], 1, fertig: true,
            new MeldeTrainingAbgeschlossen(new(still), "/zu/spaet", new Endmetriken(0.1, 0.9)));
        await Warte.NachlaufAsync(5);

        (await log.ZahlAsync<TrainingHaengengeblieben>(still)).Should().Be(1, "genau einmal hängengeblieben");
        (await log.ZahlAsync<TrainingAbgeschlossen>(still)).Should().Be(0, "nach der Markierung ist der Lauf beendet");
        (await log.ZahlAsync<TrainingHaengengeblieben>(beendet)).Should().Be(0, "ein Training, das vor der Frist endet, bleibt nie hängen");
    }
}
