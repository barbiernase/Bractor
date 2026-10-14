namespace Abstractions;

/// <summary>
/// Der Query-Dienst der Leseseite: führt eine Query über ihren Reader aus (mit Deps für Read-Your-Writes). Generiert in der Leseseite der
/// Anwendung (<c>ProjectionQueryService</c>); das Framework (gRPC, JSON-Eingang) kennt nur diese Schnittstelle.
/// </summary>
public interface IQueryDienst
{
    /// <summary>Die Query-Typen, die ein Reader der Leseseite bedient.</summary>
    IReadOnlyList<Type> QueryTypen { get; }

    Task<QueryResponse<IQueryResponse>> ExecuteAsync(IQuery query, IReadOnlyList<string>? expectedFreshIds = null);
}
