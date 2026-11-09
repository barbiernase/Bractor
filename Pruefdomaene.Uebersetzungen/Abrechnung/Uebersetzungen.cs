using Abstractions;
using Pruefwerk.Katalog.Abrechnung;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Uebersetzungen.Abrechnung;

#pragma warning disable CS9113

// ÜBERSETZUNGEN (docs/04 §4.4): Freigabe und Mahnung des Katalogs Pruefwerk werden Rechnungen und Kundenkonten — der Vorgang ist die
// Rechnung, der Empfänger das Konto.

public sealed class Freizugeben_Pruefe_1(Freizugeben source) : ITranslate<Freizugeben, Pruefe>
{
    public Pruefe Translate() => new(Vorgang: source.Rechnung.Wert, Betrag: source.Betrag);
}

public sealed class Freigegeben_GibRechnungFrei_1(Freigegeben source) : ITranslate<Freigegeben, GibRechnungFrei>
{
    public GibRechnungFrei Translate() => new(AggregateId: new(source.Vorgang));
}

public sealed class ZurPruefungVorzulegen_LegeRechnungZurPruefungVor_1(ZurPruefungVorzulegen source) : ITranslate<ZurPruefungVorzulegen, LegeRechnungZurPruefungVor>
{
    public LegeRechnungZurPruefungVor Translate() => new(AggregateId: new(source.Vorgang));
}

public sealed class Mahnfall_Mahne_1(Mahnfall source) : ITranslate<Mahnfall, Mahne>
{
    public Mahne Translate() => new(Vorgang: source.Rechnung.Wert, Empfaenger: source.Konto.Wert, Betrag: source.Betrag, Stufe: source.Mahnstufe);
}

public sealed class Gemahnt_VermerkeMahnung_1(Gemahnt source) : ITranslate<Gemahnt, VermerkeMahnung>
{
    public VermerkeMahnung Translate() => new(AggregateId: new(source.Empfaenger), Rechnung: new(source.Vorgang), Versand: source.Versand, Stufe: source.Stufe);
}
