using System.Globalization;
using Client.Infrastructure.Abstractions;
using Domain.Client.ImagePair;
using Domain.ImagePair;
using Domain.Projections;
using Microsoft.AspNetCore.Components;

namespace Domain.Client.Ui.Blazor.Components;

public partial class MainStage : ComponentBase, IDisposable
{
    [Inject] private ImagePairWorkspace Workspace { get; set; } = null!;
    [Inject] private EffectScope Fx { get; set; } = null!;

    private void Dispatch(object message) => Fx.Dispatch(message);
    private void Render() => InvokeAsync(StateHasChanged);

    // ── Connect — wie jede andere Smart Component ──
    protected override void OnInitialized()
    {
        Fx.OnChanged(Workspace.Chart, OnChartChanged);
        Fx.OnChanged(Workspace.Nav,   Render);
    }

    private void OnChartChanged()
    {
        BerechneAbgeleiteteWerte();
        Render();
    }

    // ── Selectors ──
    private IReadOnlyList<ProduktionsStripPunkt>? Punkte => Workspace.Chart.Strip?.Punkte;
    private int AnzahlAnomalie => Punkte?.Count(p => p.ProduktLabel == Klassifikation.Anomalie) ?? 0;
    private int AnzahlUngelabelt => Punkte?.Count(p => p.ProduktLabel == null) ?? 0;

    // ── Actions ──
    protected void SelectTag(DateTimeOffset datum)
    {
        Dispatch(new TagGewaehlt(datum));
        var von = new DateTimeOffset(datum.Date, TimeSpan.Zero);
        Dispatch(new GetProduktionsStrip(von, von.AddDays(1)));
    }

    protected void StarteTagLabeling()
        => Dispatch(new StarteTagLabelingAngefordert());

    protected void BeendeTagLabeling()
        => Dispatch(new BeendeTagLabelingAngefordert());

    // ── Abgeleitete Werte für SVG ──
    protected long MinTicks;
    protected double ZeitspanneMs;
    protected List<string> ZeitLabels = new();

    private const int ViewBoxW = 1000;
    private const int StripH = 32;
    private const int SpurH = 10;
    private const string StrichBreite = "1.5";

    protected static readonly List<(string Name, string Farbe, Func<Klassifikation?, bool> Filter)> Spuren = new()
    {
        ("Anomalie", "#f87171", label => label == Klassifikation.Anomalie),
        ("Fraglich", "#facc15", label => label == Klassifikation.Questionable),
        ("OK",       "#4ade80", label => label == Klassifikation.KeineAnomalie),
        ("Offen",    "#555",    label => label == null),
    };

    private void BerechneAbgeleiteteWerte()
    {
        var punkte = Workspace.Chart.Strip?.Punkte;
        if (punkte == null || punkte.Count == 0)
        {
            MinTicks = 0; ZeitspanneMs = 0; ZeitLabels.Clear();
            return;
        }

        MinTicks = punkte[0].Zeitpunkt.UtcTicks;
        var maxTicks = punkte[^1].Zeitpunkt.UtcTicks;
        ZeitspanneMs = (maxTicks - MinTicks) / (double)TimeSpan.TicksPerMillisecond;
        if (ZeitspanneMs <= 0) ZeitspanneMs = 1; // ein Punkt oder identische Zeitstempel

        ZeitLabels.Clear();
        if (punkte.Count == 1)
        {
            // Einzelner Punkt: nur einen Zeitlabel in der Mitte
            ZeitLabels.Add(punkte[0].Zeitpunkt.ToLocalTime().ToString("HH:mm"));
        }
        else
        {
            var anzahl = Math.Min(7, Math.Max(2, punkte.Count / 100));
            for (var i = 0; i < anzahl; i++)
            {
                var anteil = (double)i / (anzahl - 1);
                var ticks = MinTicks + (long)(anteil * ZeitspanneMs * TimeSpan.TicksPerMillisecond);
                var zeit = new DateTimeOffset(ticks, TimeSpan.Zero);
                ZeitLabels.Add(zeit.ToLocalTime().ToString("HH:mm"));
            }
        }
    }

    protected string XPos(DateTimeOffset zeitpunkt)
    {
        var offsetMs = (zeitpunkt.UtcTicks - MinTicks) / (double)TimeSpan.TicksPerMillisecond;
        var x = (offsetMs / ZeitspanneMs) * (ViewBoxW - 2);
        return x.ToString("F1", CultureInfo.InvariantCulture);
    }

    protected static string LabelFarbe(Klassifikation? label) => label switch
    {
        Klassifikation.Anomalie      => "#f87171",
        Klassifikation.Questionable  => "#facc15",
        Klassifikation.KeineAnomalie => "#4ade80",
        _                            => "#555"
    };

    public void Dispose() => Fx.Dispose();
}