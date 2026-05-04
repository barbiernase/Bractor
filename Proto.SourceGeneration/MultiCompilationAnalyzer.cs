using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions.SourceGeneration;
using Microsoft.CodeAnalysis;

namespace Core.SourceGeneration;

/// <summary>
/// Analysiert Typen Ã¼ber mehrere Compilations hinweg.
/// UnterstÃ¼tzt Records mit Primary Constructors korrekt.
/// </summary>
public class MultiCompilationAnalyzer
{
    private readonly List<Compilation> _compilations;
    private readonly Dictionary<string, INamedTypeSymbol> _allTypes = new();
    
    // Cache fÃ¼r bereits vollstÃ¤ndig analysierte Typen
    private readonly Dictionary<string, TypeNode> _analyzedTypes = new();

    public MultiCompilationAnalyzer(IEnumerable<Compilation> compilations)
    {
        _compilations = compilations.ToList();
        CacheAllTypes();
    }

    private void CacheAllTypes()
    {
        foreach (var compilation in _compilations)
        {
            var visitor = new TypeCollector(_allTypes);
            visitor.Visit(compilation.GlobalNamespace);
        }
        
        Console.WriteLine($"   {_allTypes.Count} Typen gecached");
    }

    public List<TypeNode> AnalyzeTypesImplementing(string interfaceFullName)
    {
        var results = new List<TypeNode>();
        
        foreach (var (_, typeSymbol) in _allTypes)
        {
            var implementsInterface = typeSymbol.AllInterfaces
                .Any(i => i.ToDisplayString() == interfaceFullName);
            
            if (implementsInterface)
            {
                var node = BuildTypeNode(typeSymbol);
                results.Add(node);
            }
        }
        
        Console.WriteLine($"   {results.Count} Typen implementieren {interfaceFullName.Split('.').Last()}");
        return results;
    }

    private TypeNode BuildTypeNode(INamedTypeSymbol typeSymbol)
    {
        var fullName = typeSymbol.ToDisplayString();
        
        // Check cache first
        if (_analyzedTypes.TryGetValue(fullName, out var cached))
        {
            return CloneTypeNode(cached);
        }
        
        var processingStack = new HashSet<string>();
        return BuildTypeNodeRecursive(typeSymbol, processingStack);
    }
    
    private TypeNode BuildTypeNodeRecursive(INamedTypeSymbol typeSymbol, HashSet<string> processingStack)
    {
        var fullName = typeSymbol.ToDisplayString();
        
        // Check cache first
        if (_analyzedTypes.TryGetValue(fullName, out var cached))
        {
            return CloneTypeNode(cached);
        }
        
        // Prevent infinite recursion
        if (processingStack.Contains(fullName))
        {
            return new TypeNode 
            { 
                Name = typeSymbol.Name,
                FullName = fullName,
                DomainType = GetDomainType(typeSymbol)
            };
        }
        
        processingStack.Add(fullName);
        
        var node = new TypeNode 
        { 
            Name = typeSymbol.Name,
            FullName = fullName,
            DomainType = GetDomainType(typeSymbol),
            IsEnum = typeSymbol.TypeKind == TypeKind.Enum
        };
        
        // Enums haben keine Konstruktor-Parameter — sofort cachen und zurückgeben
        if (node.IsEnum)
        {
            _analyzedTypes[fullName] = node;
            processingStack.Remove(fullName);
            return node;
        }
        
        // Konstruktor-Parameter analysieren - WICHTIG: Nutze GetPrimaryConstructor!
        var constructor = GetPrimaryConstructor(typeSymbol);
        
        if (constructor != null && constructor.Parameters.Length > 0)
        {
            Console.WriteLine($"      [Analyzer] {typeSymbol.Name}: {constructor.Parameters.Length} Parameter gefunden (IsRecord={typeSymbol.IsRecord})");
            foreach (var param in constructor.Parameters)
            {
                var paramNode = AnalyzeParameter(param, processingStack);
                node.ConstructorParameters.Add(paramNode);
            }
        }
        else
        {
            Console.WriteLine($"      [Analyzer] {typeSymbol.Name}: Kein Konstruktor mit Parametern gefunden");
        }
        
        // Cache the fully analyzed node
        _analyzedTypes[fullName] = node;
        
        processingStack.Remove(fullName);
        
        return node;
    }
    
    /// <summary>
    /// Findet den Primary Constructor eines Typs.
    /// 
    /// WICHTIG: Bei Records ist der Primary Constructor Ã¼ber InstanceConstructors erreichbar,
    /// nicht Ã¼ber GetMembers(".ctor")!
    /// </summary>
    private IMethodSymbol? GetPrimaryConstructor(INamedTypeSymbol typeSymbol)
    {
        // ============================================================
        // STRATEGIE 1: InstanceConstructors (funktioniert fÃ¼r Records!)
        // ============================================================
        var instanceConstructors = typeSymbol.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Length)
            .ToList();
        
        if (instanceConstructors.Any())
        {
            var best = instanceConstructors.First();
            if (best.Parameters.Length > 0)
            {
                return best;
            }
        }
        
        // ============================================================
        // STRATEGIE 2: Auch non-public Konstruktoren prÃ¼fen
        // ============================================================
        var allInstanceConstructors = typeSymbol.InstanceConstructors
            .OrderByDescending(c => c.Parameters.Length)
            .ToList();
        
        if (allInstanceConstructors.Any())
        {
            var best = allInstanceConstructors.First();
            if (best.Parameters.Length > 0)
            {
                return best;
            }
        }
        
        // ============================================================
        // STRATEGIE 3: GetMembers(".ctor") als Fallback
        // ============================================================
        var memberConstructors = typeSymbol.GetMembers(".ctor")
            .OfType<IMethodSymbol>()
            .Where(c => c.MethodKind == MethodKind.Constructor)
            .OrderByDescending(c => c.Parameters.Length)
            .ToList();
        
        if (memberConstructors.Any())
        {
            var best = memberConstructors.First();
            if (best.Parameters.Length > 0)
            {
                return best;
            }
        }
        
        // ============================================================
        // STRATEGIE 4: Bei Records - Properties als Fallback
        // ============================================================
        if (typeSymbol.IsRecord)
        {
            // Records haben init-only Properties die vom Primary Constructor stammen
            var recordProperties = typeSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                           !p.IsStatic &&
                           !p.IsIndexer &&
                           p.SetMethod != null &&
                           p.SetMethod.IsInitOnly)
                .ToList();
            
            Console.WriteLine($"      [Analyzer] {typeSymbol.Name}: Record mit {recordProperties.Count} init-only Properties");
            
            // Finde den Konstruktor der diese Properties als Parameter hat
            foreach (var ctor in typeSymbol.InstanceConstructors)
            {
                if (ctor.Parameters.Length == recordProperties.Count)
                {
                    return ctor;
                }
            }
        }
        
        return null;
    }

    private TypeNode AnalyzeParameter(IParameterSymbol param, HashSet<string> processingStack)
    {
        var paramType = param.Type;
        var node = new TypeNode { Name = param.Name };
        
        // NEU: Enum-Erkennung — Enums sind Skalare, keine komplexen Typen
        if (paramType is INamedTypeSymbol enumType && enumType.TypeKind == TypeKind.Enum)
        {
            node.FullName = paramType.ToDisplayString();
            node.IsEnum = true;
            return node;
        }
        
        // NEU: Nullable Enum Erkennung — Nullable<Enum> wird auch als Enum behandelt
        // Der FullName enthält "?" (z.B. "Domain.ImagePair.Klassifikation?"), 
        // und IsEnum wird true gesetzt, damit der Generator via Cast konvertiert 
        // statt einen Mapper aufzurufen.
        if (paramType is INamedTypeSymbol nullableType && 
            nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = nullableType.TypeArguments[0];
            if (underlyingType is INamedTypeSymbol underlyingNamed && 
                underlyingNamed.TypeKind == TypeKind.Enum)
            {
                node.FullName = paramType.ToDisplayString(); // z.B. "Domain.ImagePair.Klassifikation?"
                node.IsEnum = true;  // Markiere als Enum für Cast-Behandlung
                return node;
            }
        }
        
        if (paramType is IArrayTypeSymbol arrayType)
        {
            node.IsCollection = true;
            node.CollectionElementType = arrayType.ElementType.ToDisplayString();
            node.FullName = $"{node.CollectionElementType}[]";
            
            // Recursively analyze collection element type
            AnalyzeCollectionElementType(node, arrayType.ElementType, processingStack);
        }
        else if (paramType is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var genericDef = namedType.OriginalDefinition.ToDisplayString();
            
            if (genericDef.Contains("List") || genericDef.Contains("IList") || 
                genericDef.Contains("IEnumerable") || genericDef.Contains("ICollection") ||
                genericDef.Contains("IReadOnlyList") || genericDef.Contains("IReadOnlyCollection"))
            {
                node.IsCollection = true;
                node.CollectionElementType = namedType.TypeArguments[0].ToDisplayString();
                node.FullName = paramType.ToDisplayString();
                
                // Recursively analyze collection element type
                AnalyzeCollectionElementType(node, namedType.TypeArguments[0], processingStack);
            }
            else if (genericDef.Contains("Dictionary") || genericDef.Contains("IDictionary"))
            {
                node.IsDictionary = true;
                node.DictionaryKeyType = namedType.TypeArguments[0].ToDisplayString();
                node.DictionaryValueType = namedType.TypeArguments[1].ToDisplayString();
                node.FullName = paramType.ToDisplayString();
            }
            else
            {
                node.FullName = paramType.ToDisplayString();
                // Handle other generic types as complex types
                AnalyzeComplexType(node, namedType, processingStack);
            }
        }
        else if (paramType is INamedTypeSymbol simpleNamedType)
        {
            node.FullName = paramType.ToDisplayString();
            // Analyze complex types (Value Objects, etc.)
            AnalyzeComplexType(node, simpleNamedType, processingStack);
        }
        else
        {
            node.FullName = paramType.ToDisplayString();
        }
        
        return node;
    }
    
    private void AnalyzeComplexType(TypeNode node, INamedTypeSymbol typeSymbol, HashSet<string> processingStack)
    {
        if (BaseTypes.IsBaseType(node.FullName))
            return;
            
        // Try to find the type in our cache
        if (_allTypes.TryGetValue(node.FullName, out var referencedType))
        {
            node.DomainType = GetDomainType(referencedType);
            
            // Build the full type node to get constructor parameters
            var fullNode = BuildTypeNodeRecursive(referencedType, processingStack);
            
            // Copy constructor parameters to this node
            foreach (var param in fullNode.ConstructorParameters)
            {
                node.ConstructorParameters.Add(CloneTypeNode(param));
            }
        }
    }
    
    private void AnalyzeCollectionElementType(TypeNode parentNode, ITypeSymbol elementType, HashSet<string> processingStack)
    {
        var elementFullName = elementType.ToDisplayString();
        
        if (BaseTypes.IsBaseType(elementFullName))
            return;
            
        if (_allTypes.TryGetValue(elementFullName, out var elementSymbol))
        {
            // Build the full type node for the element
            var elementNode = BuildTypeNodeRecursive(elementSymbol, processingStack);
            
            // Store element node as a "child" for aggregation
            parentNode.ConstructorParameters.Add(CloneTypeNode(elementNode));
        }
    }
    
    private TypeNode CloneTypeNode(TypeNode source)
    {
        var clone = new TypeNode
        {
            Name = source.Name,
            FullName = source.FullName,
            DomainType = source.DomainType,
            IsCollection = source.IsCollection,
            IsDictionary = source.IsDictionary,
            IsEnum = source.IsEnum,
            CollectionElementType = source.CollectionElementType,
            DictionaryKeyType = source.DictionaryKeyType,
            DictionaryValueType = source.DictionaryValueType
        };
        
        foreach (var param in source.ConstructorParameters)
        {
            clone.ConstructorParameters.Add(CloneTypeNode(param));
        }
        
        return clone;
    }
    
    private DomainType GetDomainType(INamedTypeSymbol type)
    {
        var interfaces = type.AllInterfaces.Select(i => i.ToDisplayString()).ToHashSet();
        
        if (interfaces.Contains("Abstractions.IEvent"))
            return DomainType.Event;
            
        if (interfaces.Contains("Abstractions.ICommand") || 
            interfaces.Contains("Abstractions.ICreationCommand"))
            return DomainType.Command;

        if (interfaces.Contains("Abstractions.IQuery"))
            return DomainType.Query;

        if (interfaces.Contains("Abstractions.IQueryResponse"))
            return DomainType.QueryResponse;
            
        return DomainType.Object;
    }

    private class TypeCollector : SymbolVisitor
    {
        private readonly Dictionary<string, INamedTypeSymbol> _types;

        public TypeCollector(Dictionary<string, INamedTypeSymbol> types) => _types = types;

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            foreach (var member in symbol.GetMembers())
                member.Accept(this);
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            if (!symbol.IsAbstract && symbol.TypeKind != TypeKind.Interface)
            {
                _types.TryAdd(symbol.ToDisplayString(), symbol);
            }
            
            foreach (var nested in symbol.GetTypeMembers())
                nested.Accept(this);
        }
    }
}