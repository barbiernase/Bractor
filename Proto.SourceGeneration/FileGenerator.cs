using System.Text;
using Abstractions.SourceGeneration;

namespace Proto.SourceGeneration;

/// <summary>
/// Generiert eine vollständige cqrs.proto-Datei mit:
/// - gRPC Service Definition
/// - Transport Messages (ClientMessage, ServerMessage, etc.)
/// - Domain Types (Commands, Events, Queries, QueryResponses, Value Objects)
/// 
/// Alles in EINER Datei = kein Import nötig!
/// </summary>
public class FileGenerator
{
    private readonly Dictionary<string, string> _typeMapping = new()
    {
        // Non-nullable types
        { "System.String", "string" }, { "string", "string" },
        { "System.Int32", "int32" },   { "int", "int32" },
        { "System.Int64", "int64" },   { "long", "int64" },
        { "System.Boolean", "bool" },  { "bool", "bool" },
        { "System.Double", "double" }, { "double", "double" },
        { "System.Single", "float" },  { "float", "float" },
        { "System.Guid", "string" },
        { "System.Decimal", "string" }, { "decimal", "string" },
        { "System.DateTime", "int64" },
        { "System.DateTimeOffset", "int64" },
        
        // Nullable types - WICHTIG: Diese müssen auch gemappt werden!
        { "string?", "string" },
        { "System.String?", "string" },
        { "int?", "int32" },
        { "System.Int32?", "int32" },
        { "System.Nullable<System.Int32>", "int32" },
        { "long?", "int64" },
        { "System.Int64?", "int64" },
        { "System.Nullable<System.Int64>", "int64" },
        { "bool?", "int32" },           // ★ FIX: int32 statt bool — 0=null, 1=true, 2=false
        { "System.Boolean?", "int32" },
        { "System.Nullable<System.Boolean>", "int32" },
        { "double?", "double" },
        { "System.Double?", "double" },
        { "System.Nullable<System.Double>", "double" },
        { "float?", "float" },
        { "System.Single?", "float" },
        { "System.Nullable<System.Single>", "float" },
        { "decimal?", "string" },
        { "System.Decimal?", "string" },
        { "System.Nullable<System.Decimal>", "string" },
        { "System.Guid?", "string" },
        { "System.Nullable<System.Guid>", "string" },
        { "System.DateTime?", "int64" },
        { "System.Nullable<System.DateTime>", "int64" },
        { "System.DateTimeOffset?", "int64" },
        { "System.Nullable<System.DateTimeOffset>", "int64" },
    };

    /// <summary>
    /// FIX: Bekannte Enum-Typnamen — werden in GenerateProtoFile aus den objectTypes
    /// gesammelt, BEVOR diese gefiltert werden. Damit kann GetProtoTypeForName
    /// Enums als "int32" erkennen, auch wenn es nur einen String bekommt.
    /// </summary>
    private HashSet<string> _enumTypeNames = new();

    public string GenerateProtoFile(
    string solutionName,
    List<(string TypeName, int Depth, TypeNode Node)> objectTypes,
    List<(string TypeName, int Depth, TypeNode Node)> commandTypes,
    List<(string TypeName, int Depth, TypeNode Node)> eventTypes,
    List<(string TypeName, int Depth, TypeNode Node)> queryTypes,
    List<(string TypeName, int Depth, TypeNode Node)> queryResponseTypes)
{
    // FIX: Enum-Typen aus ALLEN Typ-Listen sammeln, bevor sie rausgefiltert werden.
    // Diese werden von GetProtoTypeForName benötigt, um Collection-Elemente
    // wie IReadOnlyList<Klassifikation> korrekt als "repeated int32" zu erkennen.
    _enumTypeNames = CollectEnumTypeNames(objectTypes, commandTypes, eventTypes, queryTypes, queryResponseTypes);

    // WICHTIG: Filtere primitive/nullable Typen aus Value Objects heraus!
    var filteredObjectTypes = objectTypes
        .Where(t => !IsPrimitiveOrNullableType(t.Node.FullName))
        .ToList();
    
    var valueObjectMessages = GenerateMessageDefinitions(filteredObjectTypes);
    var commandPayloadMessages = GenerateMessageDefinitions(commandTypes);
    var eventPayloadMessages = GenerateMessageDefinitions(eventTypes);
    var queryPayloadMessages = GenerateMessageDefinitions(queryTypes);
    var queryResponsePayloadMessages = GenerateMessageDefinitions(queryResponseTypes);

    var commandOneOfs = GenerateOneOfPart(commandTypes, 20);
    var eventOneOfs = GenerateOneOfPart(eventTypes, 20);
    var queryOneOfs = GenerateOneOfPart(queryTypes, 20);
    var queryResponseOneOfs = GenerateOneOfPart(queryResponseTypes, 20);
    
    // Query-Teil nur generieren wenn Queries existieren
    var queryRequestMessage = queryTypes.Any() 
        ? GenerateQueryRequestMessage()
        : "// QueryRequest: Keine Query-Typen definiert";
        
    var queryResponseMessage = queryResponseTypes.Any()
        ? GenerateQueryResponseMessage()
        : "// QueryResponse: Keine QueryResponse-Typen definiert";
        
    var queryPayloadMessage = queryTypes.Any()
        ? GenerateQueryPayloadMessage(queryOneOfs)
        : "// QueryPayloadDto: Keine Query-Typen definiert";
        
    var queryResponsePayloadMessage = queryResponseTypes.Any()
        ? GenerateQueryResponsePayloadMessage(queryResponseOneOfs)
        : "// QueryResponsePayloadDto: Keine QueryResponse-Typen definiert";
    
    return $$"""
             syntax = "proto3";

             option csharp_namespace = "ProtoRepo";

             package {{solutionName}};

             // ============================================================================
             // gRPC SERVICE
             // ============================================================================

             service CqrsClientService {
                 rpc Connect(stream ClientMessage) returns (stream ServerMessage);
             }

             // ============================================================================
             // CLIENT → SERVER (Upstream)
             // ============================================================================

             message ClientMessage {
                 oneof message {
                     CommandRequest command = 1;
                     SubscribeRequest subscribe = 2;
                     UnsubscribeRequest unsubscribe = 3;
                     CapabilitiesRequest capabilities = 4;
                     QueryRequest query = 5;
                 }
             }

             message CommandRequest {
                 CommandEnvelopeDto envelope = 1;
             }

             message SubscribeRequest {
                 string event_type = 1;
                 string aggregate_id = 2;
             }

             message UnsubscribeRequest {
                 string event_type = 1;
                 string aggregate_id = 2;
             }

             message CapabilitiesRequest {
                 repeated string event_types = 1;
             }

             {{queryRequestMessage}}

             // ============================================================================
             // SERVER → CLIENT (Downstream)
             // ============================================================================

             message ServerMessage {
                 oneof message {
                     EventNotification event = 1;
                     SubscriptionConfirmed subscription_confirmed = 2;
                     UnsubscriptionConfirmed unsubscription_confirmed = 3;
                     CommandAccepted command_accepted = 4;
                     ErrorResponse error = 5;
                     CapabilitiesResponse capabilities_response = 6;
                     QueryResponse query_response = 7;
                 }
             }

             message EventNotification {
                 EventEnvelopeDto envelope = 1;
             }

             message SubscriptionConfirmed {
                 string event_type = 1;
                 string aggregate_id = 2;
             }

             message UnsubscriptionConfirmed {
                 string event_type = 1;
                 string aggregate_id = 2;
             }

             message CommandAccepted {
                 string command_id = 1;
                 string correlation_id = 2;
             }

             message CapabilitiesResponse {
                 string session_id = 1;
                 repeated string allowed_commands = 2;
                 repeated string subscribed_events = 3;
                 repeated string supported_queries = 4;
             }

             {{queryResponseMessage}}

             message ErrorResponse {
                 string code = 1;
                 string message = 2;
                 string correlation_id = 3;
             }

             // ============================================================================
             // ENVELOPE TYPES
             // ============================================================================

             message CommandEnvelopeDto {
                 string command_id = 1;
                 string aggregate_id = 2;
                 string aggregate_type = 3;
                 int32 expected_version = 4;
                 int64 created_at_utc = 5;
                 string correlation_id = 6;
                 string user_id = 7;
                 string origin_session_id = 8;
             
                 oneof payload {
             {{commandOneOfs}}
                 }
             }

             message EventEnvelopeDto {
                 string event_id = 1;
                 string aggregate_id = 2;
                 string aggregate_type = 3;
                 int32 aggregate_version = 4;
                 int64 created_at_utc = 5;
                 string causation_id = 6;
                 string correlation_id = 7;
                 string user_id = 8;
                 string target_subscriber_id = 9;
                 
                 oneof payload {
             {{eventOneOfs}}
                 }
             }

             {{queryPayloadMessage}}

             {{queryResponsePayloadMessage}}

             // ============================================================================
             // VALUE OBJECTS
             // ============================================================================
             {{valueObjectMessages}}
             // ============================================================================
             // COMMAND PAYLOADS
             // ============================================================================
             {{commandPayloadMessages}}
             // ============================================================================
             // EVENT PAYLOADS
             // ============================================================================
             {{eventPayloadMessages}}
             // ============================================================================
             // QUERY PAYLOADS
             // ============================================================================
             {{queryPayloadMessages}}
             // ============================================================================
             // QUERY RESPONSE PAYLOADS
             // ============================================================================
             {{queryResponsePayloadMessages}}
             """;
}

private string GenerateQueryRequestMessage()
{
    return """
           message QueryRequest {
               string correlation_id = 1;
               QueryPayloadDto payload = 2;
           }
           """;
}

/// <summary>
/// QueryResponse enthält jetzt deps-Feld.
/// AggregateMetaDto ist ein Transport-Typ (wie CommandEnvelopeDto) — hardcoded, nicht generiert.
/// </summary>
private string GenerateQueryResponseMessage()
{
    return """
           message QueryResponse {
               string correlation_id = 1;
               QueryResponsePayloadDto payload = 2;
               repeated AggregateMetaDto deps = 3;
           }
           
           message AggregateMetaDto {
               string id = 1;
               string aggregate_type = 2;
               int32 version = 3;
           }
           """;
}

private string GenerateQueryPayloadMessage(string oneOfs)
{
    return $$"""
             message QueryPayloadDto {
                 oneof payload {
             {{oneOfs}}
                 }
             }
             """;
}

private string GenerateQueryResponsePayloadMessage(string oneOfs)
{
    return $$"""
             message QueryResponsePayloadDto {
                 oneof payload {
             {{oneOfs}}
                 }
             }
             """;
}

    private string GenerateMessageDefinitions(List<(string TypeName, int Depth, TypeNode Node)> types)
    {
        if (!types.Any()) return "";

        var sb = new StringBuilder();

        foreach (var (_, _, node) in types)
        {
            // WICHTIG: Überspringe primitive/nullable Typen - diese brauchen keine Message!
            if (IsPrimitiveOrNullableType(node.FullName))
                continue;
            
            // Enums brauchen keine eigene Message — sie werden als int32 serialisiert
            if (node.IsEnum)
                continue;
            
            var typeNameParts = node.FullName.Split('.');
            var simpleTypeName = typeNameParts.Last();
            var sanitizedNodeName = SanitizeName(simpleTypeName) + "Dto";

            sb.AppendLine($"message {sanitizedNodeName} {{");
            
            // Wenn keine Parameter vorhanden, generiere leere Message (für parameterlose Records)
            if (!node.ConstructorParameters.Any())
            {
                sb.AppendLine("    // Keine Parameter");
            }
            else
            {
                var fieldIndex = 1;
                foreach (var param in node.ConstructorParameters)
                {
                    var protoType = GetProtoType(param);
                    var fieldName = ToSnakeCase(param.Name);
                    sb.AppendLine($"    {protoType} {fieldName} = {fieldIndex++};");
                }
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private string GenerateOneOfPart(List<(string TypeName, int Depth, TypeNode Node)> types, int startIndex)
    {
        if (!types.Any()) return "";

        var sb = new StringBuilder();
        var fieldIndex = startIndex;
        foreach (var (_, _, node) in types)
        {
            // WICHTIG: Überspringe primitive/nullable Typen in OneOf
            if (IsPrimitiveOrNullableType(node.FullName))
                continue;
                
            var sanitizedNodeName = SanitizeName(node.Name) + "Dto";
            var fieldName = ToSnakeCase(node.Name);
            sb.AppendLine($"        {sanitizedNodeName} {fieldName} = {fieldIndex++};");
        }
        return sb.ToString();
    }
    
    private string GetProtoType(TypeNode paramNode)
    {
        // Enums: nullable → string (leerer String = null), non-nullable → int32
        if (paramNode.IsEnum)
        {
            if (paramNode.FullName.EndsWith("?") || 
                paramNode.FullName.StartsWith("System.Nullable<") ||
                paramNode.FullName.StartsWith("Nullable<"))
                return "string";
            return "int32";
        }
        
        // Zuerst im Mapping nachschauen (inkl. nullable Typen)
        if (_typeMapping.TryGetValue(paramNode.FullName, out var mappedType))
        {
            return mappedType;
        }
        
        // Collection handling
        if (paramNode.IsCollection)
        {
            var elementType = GetProtoTypeForName(paramNode.CollectionElementType);
            return $"repeated {elementType}";
        }
        
        // Prüfe ob es ein primitiver/nullable Typ ist, der nicht im Mapping war
        if (IsPrimitiveOrNullableType(paramNode.FullName))
        {
            // Für unbekannte nullable Typen: Basis-Typ extrahieren und mappen
            var baseType = GetBaseType(paramNode.FullName);
            if (_typeMapping.TryGetValue(baseType, out var baseMappedType))
            {
                return baseMappedType;
            }
            // Fallback für unbekannte primitive Typen
            return "string";
        }
        
        var typeNameParts = paramNode.FullName.Split('.');
        var simpleTypeName = typeNameParts.Last();

        return SanitizeName(simpleTypeName) + "Dto";
    }

    private string GetProtoTypeForName(string typeName)
    {
        if (_typeMapping.TryGetValue(typeName, out var mappedType))
            return mappedType;
        
        // Prüfe ob primitiv/nullable
        if (IsPrimitiveOrNullableType(typeName))
        {
            var baseType = GetBaseType(typeName);
            if (_typeMapping.TryGetValue(baseType, out var baseMappedType))
                return baseMappedType;
            return "string";
        }
        
        // FIX: Enum-Erkennung für Collection-Elemente.
        // GetProtoType(TypeNode) prüft paramNode.IsEnum, aber dieser Pfad
        // bekommt nur einen String (z.B. "Domain.ImagePair.Klassifikation").
        // Ohne diese Prüfung wird daraus fälschlicherweise "KlassifikationDto"
        // statt dem korrekten "int32".
        
        // Nullable Enum-Varianten erkennen (z.B. "Domain.ImagePair.Klassifikation?")
        // → string (leerer String = null)
        if (typeName.EndsWith("?"))
        {
            var baseType = typeName.TrimEnd('?');
            if (_enumTypeNames.Contains(baseType))
                return "string";
            var simpleBase = baseType.Split('.').Last();
            if (_enumTypeNames.Any(e => e.EndsWith("." + simpleBase) || e == simpleBase))
                return "string";
        }
        
        // Non-nullable Enums → int32
        if (_enumTypeNames.Contains(typeName))
            return "int32";
        
        var parts = typeName.Split('.');
        return SanitizeName(parts.Last()) + "Dto";
    }

    /// <summary>
    /// FIX: Sammelt alle Enum-Typnamen aus sämtlichen Typ-Listen.
    /// Durchsucht nicht nur die Top-Level-Nodes, sondern auch alle
    /// ConstructorParameter rekursiv, damit auch tief verschachtelte
    /// Enum-Referenzen (z.B. Klassifikation? in RegionBewertung) erkannt werden.
    /// </summary>
    private static HashSet<string> CollectEnumTypeNames(
        params List<(string TypeName, int Depth, TypeNode Node)>[] allTypeLists)
    {
        var enumNames = new HashSet<string>();
        
        foreach (var typeList in allTypeLists)
        {
            if (typeList == null) continue;
            foreach (var (_, _, node) in typeList)
            {
                CollectEnumsRecursive(node, enumNames);
            }
        }
        
        return enumNames;
    }

    private static void CollectEnumsRecursive(TypeNode node, HashSet<string> enumNames)
    {
        if (node == null) return;
        
        if (node.IsEnum && !string.IsNullOrEmpty(node.FullName))
        {
            enumNames.Add(node.FullName);
        }
        
        // Collection-Element-Typ könnte auch ein Enum sein
        if (node.IsCollection && !string.IsNullOrEmpty(node.CollectionElementType))
        {
            // Der CollectionElementType ist nur ein String — wir können ihn hier
            // nicht direkt als Enum erkennen. Aber wenn der Node Kinder hat
            // (vom DomainGraphAnalyzer als ConstructorParameters angehängt),
            // werden diese rekursiv geprüft.
        }
        
        foreach (var param in node.ConstructorParameters)
        {
            CollectEnumsRecursive(param, enumNames);
        }
    }
    
    /// <summary>
    /// Prüft ob ein Typ primitiv oder nullable ist.
    /// Diese Typen sollten NICHT als Message generiert werden!
    /// </summary>
    private bool IsPrimitiveOrNullableType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return false;
        
        // Direkt im Mapping?
        if (_typeMapping.ContainsKey(typeName))
            return true;
        
        // Nullable<T> Pattern
        if (typeName.StartsWith("System.Nullable<") || typeName.StartsWith("Nullable<"))
            return true;
        
        // Endet mit ? (nullable)
        if (typeName.EndsWith("?"))
            return true;
        
        // Bekannte System-Typen
        var primitiveTypes = new HashSet<string>
        {
            "System.String", "string",
            "System.Int32", "int",
            "System.Int64", "long",
            "System.Int16", "short",
            "System.Byte", "byte",
            "System.Boolean", "bool",
            "System.Double", "double",
            "System.Single", "float",
            "System.Decimal", "decimal",
            "System.Char", "char",
            "System.Object", "object",
            "System.Guid",
            "System.DateTime",
            "System.DateTimeOffset",
            "System.TimeSpan",
        };
        
        return primitiveTypes.Contains(typeName);
    }
    
    /// <summary>
    /// Extrahiert den Basis-Typ aus einem nullable Typ
    /// </summary>
    private string GetBaseType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return typeName;
        
        // Handle "Type?" syntax
        if (typeName.EndsWith("?"))
            return typeName.TrimEnd('?');
        
        // Handle "System.Nullable<Type>" syntax
        if (typeName.StartsWith("System.Nullable<") && typeName.EndsWith(">"))
            return typeName.Substring(16, typeName.Length - 17);
        
        // Handle "Nullable<Type>" syntax
        if (typeName.StartsWith("Nullable<") && typeName.EndsWith(">"))
            return typeName.Substring(9, typeName.Length - 10);
        
        return typeName;
    }
    
    private string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
            
        return name
            // Echte Unicode-Umlaute (encoding-sicher via Escapes)
            .Replace("\u00e4", "ae").Replace("\u00f6", "oe").Replace("\u00fc", "ue")
            .Replace("\u00c4", "Ae").Replace("\u00d6", "Oe").Replace("\u00dc", "Ue")
            .Replace("\u00df", "ss")
            // WICHTIG: Entferne '?' aus Namen
            .Replace("?", "");
    }
    private string ToSnakeCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        
        var sanitizedText = SanitizeName(text);
        var sb = new StringBuilder();
        sb.Append(char.ToLowerInvariant(sanitizedText[0]));
        for (int i = 1; i < sanitizedText.Length; ++i)
        {
            char c = sanitizedText[i];
            if (char.IsUpper(c))
            {
                sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}