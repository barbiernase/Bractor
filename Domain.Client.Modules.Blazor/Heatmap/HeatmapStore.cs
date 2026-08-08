using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.ImagePair;
namespace Domain.Client.Modules.Heatmap;

/// <summary>
/// Eigener Store — cross-modul lesbar.
/// Aggregiert KI-Klassifikationen über Regionen für die Heatmap-Visualisierung.
/// </summary>
public partial class HeatmapStore : StoreBase
{
    [ObservableProperty] private int[] _regionCounts = new int[8];
    [ObservableProperty] private int? _selectedRegion;

    void Handle(HeatmapRegionSelected evt, MessageContext ctx) => SelectedRegion = evt.RegionIndex;

    void Handle(EinzelBildDurchKiKlassifiziert evt, MessageContext ctx)
    {
        var counts = (int[])RegionCounts.Clone();
        for (int i = 0; i < evt.RegionLabels.Count && i < 8; i++)
            if (evt.RegionLabels[i] == Klassifikation.Anomalie) counts[i]++;
        RegionCounts = counts;
    }
}
