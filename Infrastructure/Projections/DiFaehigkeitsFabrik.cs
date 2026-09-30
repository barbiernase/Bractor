using Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Projections;

/// <summary>
/// <see cref="IFaehigkeitsFabrik"/> über den DI-Container: ein Bereich = ein <see cref="IServiceScope"/>.
/// Die Stores sind SCOPED registriert (ProjectionServicesGenerator) → innerhalb eines Bereichs liefert jede
/// Fähigkeit eines Stores dieselbe Instanz (Co-Commit-Puffer + Marke teilen sich die Instanz).
/// </summary>
public sealed class DiFaehigkeitsFabrik : IFaehigkeitsFabrik
{
    private readonly IServiceScopeFactory _scopes;

    public DiFaehigkeitsFabrik(IServiceScopeFactory scopes) => _scopes = scopes;

    public IFaehigkeitsBereich Oeffne() => new Bereich(_scopes.CreateScope());

    private sealed class Bereich : IFaehigkeitsBereich
    {
        private readonly IServiceScope _scope;
        public Bereich(IServiceScope scope) => _scope = scope;
        public T Hole<T>() where T : class => _scope.ServiceProvider.GetRequiredService<T>();
        public void Dispose() => _scope.Dispose();
    }
}
