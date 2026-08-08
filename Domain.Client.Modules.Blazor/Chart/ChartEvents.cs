using Client.Infrastructure.Abstractions;
namespace Domain.Client.Modules.Chart;
public record TagGewaehlt(DateTimeOffset? Datum) : IClientEvent;
