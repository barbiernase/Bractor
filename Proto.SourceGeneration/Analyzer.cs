using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

// Vereinfachte Knotenstruktur - Ein Knoten hat einfach eine Liste von Knoten!
public class TypeNode
{
    public string Name { get; set; }
    public string FullName { get; set; }
    public string DomainType { get; set; } // "Command", "Event" oder "Object"
    public bool IsCollection { get; set; }
    public bool IsDictionary { get; set; }
    public string CollectionElementType { get; set; } // Bei Collections: der Element-Typ
    public string DictionaryKeyType { get; set; }     // Bei Dictionary: Key-Typ
    public string DictionaryValueType { get; set; }   // Bei Dictionary: Value-Typ
    public List<TypeNode> ConstructorParameters { get; set; } = new List<TypeNode>();
    
    public override string ToString() => IsCollection ? $"{Name}: List<{CollectionElementType}>" : 
                                        IsDictionary ? $"{Name}: Dictionary<{DictionaryKeyType},{DictionaryValueType}>" : 
                                        $"{Name}: {FullName}";
}

public class DomainGraphAnalyzer
{
    private readonly Compilation _compilation;
    private readonly Dictionary<string, INamedTypeSymbol> _allTypes = new Dictionary<string, INamedTypeSymbol>();
    private readonly HashSet<string> _baseTypes = new HashSet<string>
    {
        "string", "int", "long", "bool", "double", "decimal", "float",
        "DateTime", "DateTimeOffset", "TimeSpan", "Guid",
        "byte", "short", "char", "object", "void"
    };

    public DomainGraphAnalyzer(Compilation compilation)
    {
        _compilation = compilation;
        CacheAllTypes();
    }

    // 1. Alle Typen im Projekt sammeln
    private void CacheAllTypes()
    {
        Console.WriteLine("[INFO] Sammle alle Typen im Projekt...");
        
        var allTypes = _compilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot()
                .DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Select(node => _compilation.GetSemanticModel(tree).GetDeclaredSymbol(node)))
            .OfType<INamedTypeSymbol>()
            .Where(t => t != null);

        foreach (var type in allTypes)
        {
            _allTypes[type.ToDisplayString()] = type;
            _allTypes[type.Name] = type; // Auch mit kurzem Namen speichern
        }
        
        Console.WriteLine($"[INFO] {_allTypes.Count} Typen im Projekt gefunden.");
    }

    // 2. Typ anhand Namen finden
    private INamedTypeSymbol FindTypeByName(string typeName)
    {
        // Generics entfernen für die Suche
        var cleanName = typeName.Split('<')[0];
        
        // Erst im Cache suchen
        if (_allTypes.TryGetValue(typeName, out var type))
            return type;
        
        if (_allTypes.TryGetValue(cleanName, out type))
            return type;
        
        // Sonst versuchen über Compilation zu finden (für System-Typen)
        return _compilation.GetTypeByMetadataName(typeName) as INamedTypeSymbol;
    }

    // 3. Prüfen ob Basis-Typ
    private bool IsBaseType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return true;
            
        // Generics entfernen für den Check
        var cleanName = typeName.Split('<')[0].Split('.').Last();
        
        return _baseTypes.Contains(cleanName) || 
               typeName.StartsWith("System.") ||
               typeName.StartsWith("Microsoft.") ||
               FindTypeByName(typeName) == null;
    }

    // 4. Hauptanalyse - Ein Graph pro IMessagePayload
    public List<TypeNode> AnalyzeMessagePayloads()
    {
        var results = new List<TypeNode>();
        const string markerInterface = "Abstractions.IMessagePayload";
        
        // Alle IMessagePayload-Implementierungen finden
        var payloadTypes = _allTypes.Values
            .Where(t => t.AllInterfaces.Any(i => i.ToDisplayString() == markerInterface))
            .ToList();
        
        Console.WriteLine($"\n[INFO] Gefunden: {payloadTypes.Count} IMessagePayload-Implementierungen");

        // Jeden Payload-Typ als eigenen Graphen verarbeiten
        foreach (var payloadType in payloadTypes)
        {
            Console.WriteLine($"\n[ANALYSE] Erstelle Graph für: {payloadType.Name}");
            var rootNode = ProcessTypeWithQueue(payloadType);
            if (rootNode.DomainType != null)
                Console.WriteLine($"  → Typ: {rootNode.DomainType}");
            results.Add(rootNode);
        }
        
        return results;
    }

    // 5. Queue-basierte Typ-Verarbeitung - KEINE REKURSION!
    private TypeNode ProcessTypeWithQueue(INamedTypeSymbol startType)
    {
        var processedTypes = new Dictionary<string, TypeNode>();
        var queue = new Queue<(INamedTypeSymbol type, TypeNode node, string paramName)>();
        
        // Root-Knoten erstellen
        var rootNode = new TypeNode 
        { 
            Name = startType.Name,
            FullName = startType.ToDisplayString(),
            DomainType = GetDomainType(startType)
        };
        
        queue.Enqueue((startType, rootNode, startType.Name));
        
        while (queue.Count > 0)
        {
            var (currentType, currentNode, nodeName) = queue.Dequeue();
            var typeKey = currentType.ToDisplayString();
            
            // Name setzen (wichtig für Parameter)
            if (currentNode.Name == null)
                currentNode.Name = nodeName;
            
            // Bereits verarbeitet?
            if (processedTypes.ContainsKey(typeKey))
            {
                // Zyklus erkannt - verwende Referenz
                var existingNode = processedTypes[typeKey];
                currentNode.FullName = existingNode.FullName + " (Ref)";
                currentNode.ConstructorParameters = new List<TypeNode>(); // Keine weitere Verarbeitung
                Console.WriteLine($"  [ZYKLUS] {currentType.Name} - bereits verarbeitet");
                continue;
            }
            
            processedTypes[typeKey] = currentNode;
            Console.WriteLine($"  [PROCESS] {currentType.Name}");
            
            // DomainType bestimmen
            currentNode.DomainType = GetDomainType(currentType);
            if (currentNode.DomainType != "Object")
                Console.WriteLine($"    → Typ: {currentNode.DomainType}");
            
            // Konstruktor mit den meisten Parametern finden
            var constructor = currentType.GetMembers(".ctor")
                .OfType<IMethodSymbol>()
                .Where(c => c.DeclaredAccessibility == Accessibility.Public)
                .OrderByDescending(c => c.Parameters.Length)
                .FirstOrDefault();
            
            if (constructor == null || constructor.Parameters.Length == 0)
            {
                Console.WriteLine($"    → Keine Konstruktor-Parameter");
                continue;
            }
            
            Console.WriteLine($"    → {constructor.Parameters.Length} Parameter gefunden");
            
            // Konstruktor-Parameter verarbeiten
            foreach (var param in constructor.Parameters)
            {
                var paramNode = AnalyzeParameterType(param.Type, param.Name);
                currentNode.ConstructorParameters.Add(paramNode);
                
                // Wenn es kein Basis-Typ ist, zur Queue hinzufügen
                if (!IsBaseType(paramNode.FullName) && !paramNode.IsCollection && !paramNode.IsDictionary)
                {
                    var paramTypeSymbol = FindTypeByName(paramNode.FullName);
                    if (paramTypeSymbol != null)
                    {
                        paramNode.DomainType = GetDomainType(paramTypeSymbol);
                        queue.Enqueue((paramTypeSymbol, paramNode, param.Name));
                    }
                }
                // Bei Collections: Element-Typ zur Queue wenn es kein Basis-Typ ist
                else if (paramNode.IsCollection && !IsBaseType(paramNode.CollectionElementType))
                {
                    var elementType = FindTypeByName(paramNode.CollectionElementType);
                    if (elementType != null)
                    {
                        var elementNode = new TypeNode 
                        { 
                            Name = paramNode.CollectionElementType.Split('.').Last(),
                            FullName = paramNode.CollectionElementType,
                            DomainType = GetDomainType(elementType)
                        };
                        queue.Enqueue((elementType, elementNode, "Element"));
                        // Element als "virtuellen" Parameter der Collection hinzufügen
                        paramNode.ConstructorParameters.Add(elementNode);
                    }
                }
                // Bei Dictionary: Key und Value-Typen zur Queue
                else if (paramNode.IsDictionary)
                {
                    // Key-Type
                    if (!IsBaseType(paramNode.DictionaryKeyType))
                    {
                        var keyType = FindTypeByName(paramNode.DictionaryKeyType);
                        if (keyType != null)
                        {
                            var keyNode = new TypeNode 
                            { 
                                Name = "Key",
                                FullName = paramNode.DictionaryKeyType,
                                DomainType = GetDomainType(keyType)
                            };
                            queue.Enqueue((keyType, keyNode, "Key"));
                            paramNode.ConstructorParameters.Add(keyNode);
                        }
                    }
                    // Value-Type
                    if (!IsBaseType(paramNode.DictionaryValueType))
                    {
                        var valueType = FindTypeByName(paramNode.DictionaryValueType);
                        if (valueType != null)
                        {
                            var valueNode = new TypeNode 
                            { 
                                Name = "Value",
                                FullName = paramNode.DictionaryValueType,
                                DomainType = GetDomainType(valueType)
                            };
                            queue.Enqueue((valueType, valueNode, "Value"));
                            paramNode.ConstructorParameters.Add(valueNode);
                        }
                    }
                }
            }
        }
        
        return rootNode;
    }

    // 6. Parameter-Typ analysieren
    private TypeNode AnalyzeParameterType(ITypeSymbol paramType, string paramName)
    {
        var node = new TypeNode { Name = paramName };
        
        // Array?
        if (paramType is IArrayTypeSymbol arrayType)
        {
            node.IsCollection = true;
            node.CollectionElementType = arrayType.ElementType.ToDisplayString();
            node.FullName = $"{node.CollectionElementType}[]";
            Console.WriteLine($"      • {paramName}: Array von {node.CollectionElementType}");
        }
        // Generic Type?
        else if (paramType is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var genericDef = namedType.OriginalDefinition.ToDisplayString();
            
            // Dictionary?
            if (genericDef.Contains("Dictionary") || genericDef.Contains("IDictionary"))
            {
                node.IsDictionary = true;
                node.DictionaryKeyType = namedType.TypeArguments[0].ToDisplayString();
                node.DictionaryValueType = namedType.TypeArguments[1].ToDisplayString();
                node.FullName = paramType.ToDisplayString();
                Console.WriteLine($"      • {paramName}: Dictionary<{node.DictionaryKeyType}, {node.DictionaryValueType}>");
            }
            // Collection?
            else if (genericDef.Contains("List") || genericDef.Contains("IList") || 
                     genericDef.Contains("IEnumerable") || genericDef.Contains("ICollection") ||
                     genericDef.Contains("IReadOnlyList") || genericDef.Contains("IReadOnlyCollection"))
            {
                node.IsCollection = true;
                node.CollectionElementType = namedType.TypeArguments[0].ToDisplayString();
                node.FullName = paramType.ToDisplayString();
                Console.WriteLine($"      • {paramName}: Collection von {node.CollectionElementType}");
            }
            else
            {
                // Andere generische Typen
                node.FullName = paramType.ToDisplayString();
                Console.WriteLine($"      • {paramName}: {node.FullName}");
            }
        }
        else
        {
            // Normaler Typ
            node.FullName = paramType.ToDisplayString();
            Console.WriteLine($"      • {paramName}: {node.FullName}");
        }
        
        return node;
    }
    
    // 7. DomainType bestimmen basierend auf Interfaces
    private string GetDomainType(INamedTypeSymbol type)
    {
        var interfaces = type.AllInterfaces.Select(i => i.ToDisplayString()).ToHashSet();
        
        // Event?
        if (interfaces.Contains("Abstractions.IEvent"))
            return "Event";
            
        // Command? (IIdentifiedCommand oder ICreationCommand)
        if (interfaces.Contains("Abstractions.IIdentifiedCommand") || 
            interfaces.Contains("Abstractions.ICreationCommand"))
            return "Command";
            
        // Alles andere ist ein Object (Value Objects etc.)
        return "Object";
    }
}