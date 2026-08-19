using Client.Infrastructure.Abstractions;
using Domain.Modell;

namespace Domain.Client.Modules.Modelle;

/// <summary>Übersetzt die Modelle-Bühnen-Intents in Modell-Commands.</summary>
public partial class ModellIntentHandler
{
    IEnumerable<object> Handle(ModellRegistrierenIntent evt, MessageContext ctx)
    {
        var id = Guid.NewGuid();
        var name = string.IsNullOrWhiteSpace(evt.Name) ? "Modell" : evt.Name.Trim();
        var pfad = string.IsNullOrWhiteSpace(evt.Pfad) ? $"modelle/{id:N}.pt" : evt.Pfad;
        yield return new RegistriereModell(
            id, evt.TrainingslaufId, evt.DatensatzId, evt.DatensatzVersion,
            name, pfad, new ModellMetriken(evt.Loss, evt.Genauigkeit));
    }

    IEnumerable<object> Handle(ModellAktivSetzenIntent evt, MessageContext ctx)
    {
        yield return new SetzeModellAktiv(evt.ModellId);
    }

    IEnumerable<object> Handle(ModellArchivierenIntent evt, MessageContext ctx)
    {
        yield return new ArchiviereModell(evt.ModellId);
    }
}
