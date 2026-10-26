using Abstractions;
using Domain.Datensatz;

namespace Domain.Pipeline.Datensatz;

// Zusammenhang der beiden Datensatz-Flüsse: siehe DatensatzRangeAufloesung.cs.

/// <summary>Einfrieren anfordern → Mitglieder schnappschießen → Einfrieren abschließen.</summary>
public sealed class DatensatzEinfrieren : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var angefordert = p.Auf<EinfrierenAngefordert>();
        var einfrieren = angefordert.Rufe<IMitgliederEinfrieren>(angefordert => new FriereMitgliederEin(angefordert.Mitglieder, angefordert.Split));
        var abschliessen = p.Alle(einfrieren.Bei<MitgliederEingefroren>(), angefordert.Strom()).Sende<SchliesseEinfrierenAb>((einfrieren, angefordertStrom) => new SchliesseEinfrierenAb(new Id<Domain.Datensatz.Datensatz>(angefordertStrom.Id), einfrieren.Mitglieder));
    });
}
