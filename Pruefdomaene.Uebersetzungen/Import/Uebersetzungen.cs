using Abstractions;
using Pruefwerk.Katalog;
using Pruefdomaene.Import;

namespace Pruefdomaene.Uebersetzungen.Import;

#pragma warning disable CS9113

// ÜBERSETZUNGEN (docs/04 §4.4): Annahme und Zeilenlauf des Katalogs Pruefwerk werden Importaufträge — der Vorgang ist der Auftrag.

public sealed class ImportAngefordert_Anfordern_1(ImportAngefordert source) : ITranslate<ImportAngefordert, Anfordern>
{
    public Anfordern Translate() => new(Menge: source.Zeilen);
}

public sealed class Angenommen_StarteImport_1(Angenommen source) : ITranslate<Angenommen, StarteImport>
{
    public StarteImport Translate() => new(AggregateId: new(source.Vorgang), Zeilen: source.Menge);
}

public sealed class ImportZuLaufen_Laufen_1(ImportZuLaufen source) : ITranslate<ImportZuLaufen, Laufen>
{
    public Laufen Translate() => new(Vorgang: source.Import.Wert, Menge: source.Zeilen);
}

public sealed class Stand_MeldeImportStand_1(Stand source) : ITranslate<Stand, MeldeImportStand>
{
    public MeldeImportStand Translate() => new(AggregateId: new(source.Vorgang), Zeilen: source.Gelesen);
}
