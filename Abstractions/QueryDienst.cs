namespace Abstractions;

/// <summary>
/// Der Query-Dienst der Leseseite: führt eine Query über ihren Reader aus, mit dem Stand je gezeigter Id. Generiert in der Leseseite der
/// Anwendung (<c>ProjectionQueryService</c>); das Framework (gRPC, JSON-Eingang) kennt nur diese Schnittstelle.
/// </summary>
public interface IQueryDienst
{
    /// <summary>Die Query-Typen, die ein Reader der Leseseite bedient.</summary>
    IReadOnlyList<Type> QueryTypen { get; }

    /// <param name="mindestens">Read-your-writes (docs/konzept-akteure.md §5): „mindestens Stream S auf Version v“ — der Reader wartet
    /// begrenzt, bis seine Projektion jeden dieser Streams so weit verarbeitet hat; reicht die Frist nicht, sagt die Antwort
    /// <see cref="QueryResponse{T}.NochNicht"/>.</param>
    Task<QueryResponse<IQueryResponse>> ExecuteAsync(IQuery query, IReadOnlyList<StreamStand>? mindestens = null);
}

/// <summary>Ein Stream und eine Version: „S auf v“ — was ein Client geschrieben hat (aus der Quittung) bzw. wie weit eine Antwort S zeigt.</summary>
public sealed record StreamStand(Guid Stream, int Version);
