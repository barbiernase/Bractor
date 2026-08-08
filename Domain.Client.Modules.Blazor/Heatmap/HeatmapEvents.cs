using Client.Infrastructure.Abstractions;
namespace Domain.Client.Modules.Heatmap;

public record HeatmapRegionSelected(int RegionIndex) : IClientEvent;
public record HeatmapDataRequested() : IClientEvent;
