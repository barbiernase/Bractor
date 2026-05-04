using Abstractions;

namespace Client.Infrastructure.Abstractions;

/// <summary>
/// Typsichere Query-Erweiterung.
/// Verknüpft einen Query-Typ mit seinem Response-Typ zur Compile-Zeit.
/// 
/// Der Server-seitige IQuery (in Abstractions) hat keinen Typ-Parameter.
/// Diese Erweiterung ist client-spezifisch — die QueryBridge nutzt TResponse
/// um die gRPC-Response korrekt zu deserialisieren und zu publishen.
/// 
/// Beispiel:
///   public record GetAlleTodos() : IQuery&lt;TodoListe&gt;;
/// </summary>
public interface IQuery<TResponse> : IQuery where TResponse : IQueryResponse { }