// REPO-PFAD: Proto.SourceGeneration/FileGenerator.cs  (MODIFIZIERT)
using System.Text;
using Abstractions.SourceGeneration;
using Core.SourceGeneration;

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
    // Die Skalar-Regeln (Proto-Feldtyp je C#-Typ) leben in Core.SourceGeneration.ProtoScalarSpecs
    // (die EINE Quelle). Kein lokales _typeMapping mehr — siehe GetProtoType/GetProtoTypeForName.

    /// <summary>
    /// FIX: Bekannte Enum-Typnamen — werden in GenerateProtoFile aus den objectTypes
    /// gesammelt, BEVOR diese gefiltert werden. Damit kann GetProtoTypeForName
    /// Enums als "int32" erkennen, auch wenn es nur einen String bekommt.
    /// </summary>
    private HashSet<string> _enumTypeNames = new();

    /// <param name="paket">Das Proto-Paket der Leitung der Anwendung (der Aufrufer leitet es aus der Projektstruktur ab).</param>
    /// <param name="csharpNamespace">Der C#-Namespace der DTOs = das Leitungs-Projekt der Anwendung (docs/07, Rolle Leitung).</param>
    public string GenerateProtoFile(
    string paket,
    List<(string TypeName, int Depth, TypeNode Node)> objectTypes,
    List<(string TypeName, int Depth, TypeNode Node)> commandTypes,
    List<(string TypeName, int Depth, TypeNode Node)> eventTypes,
    List<(string TypeName, int Depth, TypeNode Node)> queryTypes,
    List<(string TypeName, int Depth, TypeNode Node)> queryResponseTypes,
    List<(string TypeName, int Depth, TypeNode Node)> triggerTypes,
    List<(string TypeName, int Depth, TypeNode Node)> auftragTypes,
    string csharpNamespace)
{

    // ═══════════════════════════════════════════════════
    // Payload-Kategorien — neue Kategorie = ein Eintrag
    // ═══════════════════════════════════════════════════

    var payloadCategories = new (string Label, List<(string TypeName, int Depth, TypeNode Node)> Types)[]
    {
        ("VALUE OBJECTS",     objectTypes.Where(t => !IsPrimitiveOrNullableType(t.Node.FullName)).ToList()),
        ("COMMAND PAYLOADS",  commandTypes),
        ("EVENT PAYLOADS",    eventTypes),
        ("QUERY PAYLOADS",    queryTypes),
        ("QUERY RESPONSE PAYLOADS", queryResponseTypes),
        ("TRIGGER PAYLOADS",  triggerTypes),
        ("AUFTRAG PAYLOADS",  auftragTypes),
    };

    // Enum-Typen aus ALLEN Listen sammeln (vor dem Filtern!)
    _enumTypeNames = CollectEnumTypeNames(objectTypes, commandTypes, eventTypes, queryTypes, queryResponseTypes, triggerTypes, auftragTypes);

    // Messages + OneOfs generieren — in einem Loop
    var allMessages = new StringBuilder();
    foreach (var (label, types) in payloadCategories)
    {
        var msgs = GenerateMessageDefinitions(types);
        if (!string.IsNullOrEmpty(msgs))
        {
            allMessages.AppendLine($"// ============================================================================");
            allMessages.AppendLine($"// {label}");
            allMessages.AppendLine($"// ============================================================================");
            allMessages.Append(msgs);
        }
    }

    // Nur die Domänen-DTOs: Hülle, Dienst und Protokoll-Nachrichten stehen im Framework (Cqrs.Leitung/cqrs.proto); eine Domänen-Nachricht
    // reist dort als Nutzlast (Typname + die hier definierte {Typ}Dto-Message als Bytes).
    return $$"""
             syntax = "proto3";

             option csharp_namespace = "{{csharpNamespace}}";

             package {{paket}};

             // ============================================================================
             // DIE DOMÄNEN-NACHRICHTEN DER ANWENDUNG (generiert von Cqrs.Codegen — nicht editieren)
             //
             // Je Command, Event, Query, Antwort, Trigger, Auftrag und Wertobjekt eine {Typ}Dto-Message. Auf der Leitung
             // (Cqrs.Leitung/cqrs.proto) reisen sie als Nutzlast { typ = "{Typ}", daten = {Typ}Dto als Bytes }.
             // ============================================================================

             {{allMessages}}
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
            var sanitizedNodeName = NameSanitizer.DtoName(simpleTypeName);   // die Namensregel der Leitung (eine Quelle)

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
        
        // Zuerst in der EINEN Quelle nachschauen (inkl. nullable Typen). Value-Type-Nullables
        // reisen als proto3 `optional` (native Presence). string? bleibt `string` mit Sentinel
        // (leer=null), weil Reference-Nullability zur Reflection-Zeit unsichtbar ist (IsRefType).
        if (ProtoScalarSpecs.TryGet(paramNode.FullName, out var scalar))
        {
            return scalar.IsNullable && !scalar.IsRefType ? "optional " + scalar.ProtoType : scalar.ProtoType;
        }

        // Collection handling
        if (paramNode.IsCollection)
        {
            var elementType = GetProtoTypeForName(paramNode.CollectionElementType);
            return $"repeated {elementType}";
        }

        // Prüfe ob es ein primitiver/nullable Typ ist, der nicht in der Spec-Tabelle war
        if (IsPrimitiveOrNullableType(paramNode.FullName))
        {
            // Für unbekannte nullable Typen: Basis-Typ extrahieren und mappen
            var baseType = GetBaseType(paramNode.FullName);
            if (ProtoScalarSpecs.TryGet(baseType, out var baseScalar))
            {
                return baseScalar.ProtoType;
            }
            // Fallback für unbekannte primitive Typen
            return "string";
        }
        
        var typeNameParts = paramNode.FullName.Split('.');
        var simpleTypeName = typeNameParts.Last();

        return NameSanitizer.DtoName(simpleTypeName);
    }

    private string GetProtoTypeForName(string typeName)
    {
        if (ProtoScalarSpecs.TryGet(typeName, out var scalar))
            return scalar.ProtoType;

        // Prüfe ob primitiv/nullable
        if (IsPrimitiveOrNullableType(typeName))
        {
            var baseType = GetBaseType(typeName);
            if (ProtoScalarSpecs.TryGet(baseType, out var baseScalar))
                return baseScalar.ProtoType;
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
        return NameSanitizer.DtoName(parts.Last());
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
        
        // Direkt in der EINEN Quelle (auch eine typisierte Id, Id<TAggregat>)?
        if (ProtoScalarSpecs.TryGet(typeName, out _))
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