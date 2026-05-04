using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions.SourceGeneration;
using Microsoft.CodeAnalysis;

namespace Core.SourceGeneration
{
    public class DomainGraphAnalyzer : IDomainAnalyzer
    {
        private readonly ITypeResolver _typeResolver;

        public DomainGraphAnalyzer(ITypeResolver typeResolver)
        {
            _typeResolver = typeResolver;
        }

        public static DomainGraphAnalyzer Create(Compilation compilation, bool useGlobalNamespace = false)
        {
            return new DomainGraphAnalyzer(new CompilationTypeResolver(compilation, useGlobalNamespace));
        }

        public List<TypeNode> AnalyzeMessagePayloads()
        {
            return AnalyzeTypesImplementing("Abstractions.IMessagePayload");
        }

        public List<TypeNode> AnalyzeTypesImplementing(string interfaceFullName)
        {
            var results = new List<TypeNode>();
            var types = _typeResolver.GetTypesImplementing(interfaceFullName).ToList();

            foreach (var type in types)
            {
                var symbol = (INamedTypeSymbol)type;
                var rootNode = ProcessTypeWithQueue(symbol);
                results.Add(rootNode);
            }

            return results;
        }

        public TypeNode AnalyzeType(string typeName)
        {
            var type = _typeResolver.FindTypeByName(typeName) as INamedTypeSymbol;
            if (type == null)
                throw new ArgumentException($"Type '{typeName}' not found");
            
            return ProcessTypeWithQueue(type);
        }

        private TypeNode ProcessTypeWithQueue(INamedTypeSymbol startType)
        {
            var processedTypes = new Dictionary<string, TypeNode>();
            var queue = new Queue<(INamedTypeSymbol type, TypeNode node, string paramName)>();
            
            var rootNode = new TypeNode 
            { 
                Name = startType.Name,
                FullName = startType.ToDisplayString(),
                DomainType = GetDomainType(startType),
                // FIX: IsEnum auch beim Root-Node setzen
                IsEnum = startType.TypeKind == TypeKind.Enum
            };
            
            queue.Enqueue((startType, rootNode, startType.Name));
            
            while (queue.Count > 0)
            {
                var (currentType, currentNode, nodeName) = queue.Dequeue();
                var typeKey = currentType.ToDisplayString();
                
                if (currentNode.Name == null)
                    currentNode.Name = nodeName;
                
                if (processedTypes.ContainsKey(typeKey))
                {
                    var existingNode = processedTypes[typeKey];
                    currentNode.FullName = existingNode.FullName + " (Ref)";
                    currentNode.ConstructorParameters = new List<TypeNode>();
                    continue;
                }
                
                processedTypes[typeKey] = currentNode;
                
                currentNode.DomainType = GetDomainType(currentType);
                
                // FIX: IsEnum setzen — ohne dieses Flag werden Enums wie Klassifikation
                // fälschlich als Value Objects behandelt, und der DtoMapper generiert
                // MapKlassifikation(KlassifikationDto dto) für einen Typ der nicht existiert.
                // MultiCompilationAnalyzer (Zeile 100) setzt dies korrekt —
                // hier fehlte es bisher.
                currentNode.IsEnum = currentType.TypeKind == TypeKind.Enum;
                
                // Enums haben keine Konstruktor-Parameter — früh abbrechen
                if (currentNode.IsEnum)
                {
                    continue;
                }
               
                var constructor = currentType.GetMembers(".ctor")
                    .OfType<IMethodSymbol>()
                    .Where(c => c.DeclaredAccessibility == Accessibility.Public)
                    .OrderByDescending(c => c.Parameters.Length)
                    .FirstOrDefault();
                
                if (constructor == null || constructor.Parameters.Length == 0)
                {
                    continue;
                }
                
                
                foreach (var param in constructor.Parameters)
                {
                    var paramNode = AnalyzeParameterType(param.Type, param.Name);
                    currentNode.ConstructorParameters.Add(paramNode);
                    
                    if (!_typeResolver.IsBaseType(paramNode.FullName) && !paramNode.IsCollection && !paramNode.IsDictionary && !paramNode.IsEnum)
                    {
                        var paramTypeSymbol = _typeResolver.FindTypeByName(paramNode.FullName) as INamedTypeSymbol;
                        if (paramTypeSymbol != null)
                        {
                            paramNode.DomainType = GetDomainType(paramTypeSymbol);
                            queue.Enqueue((paramTypeSymbol, paramNode, param.Name));
                        }
                    }
                    else if (paramNode.IsCollection && !_typeResolver.IsBaseType(paramNode.CollectionElementType))
                    {
                        var elementType = _typeResolver.FindTypeByName(paramNode.CollectionElementType) as INamedTypeSymbol;
                        if (elementType != null)
                        {
                            var elementNode = new TypeNode 
                            { 
                                Name = paramNode.CollectionElementType.Split('.').Last(),
                                FullName = paramNode.CollectionElementType,
                                DomainType = GetDomainType(elementType),
                                // FIX: IsEnum auch für Collection-Element-Nodes setzen
                                IsEnum = elementType.TypeKind == TypeKind.Enum
                            };
                            queue.Enqueue((elementType, elementNode, "Element"));
                            paramNode.ConstructorParameters.Add(elementNode);
                        }
                    }
                    else if (paramNode.IsDictionary)
                    {
                        if (!_typeResolver.IsBaseType(paramNode.DictionaryKeyType))
                        {
                            var keyType = _typeResolver.FindTypeByName(paramNode.DictionaryKeyType) as INamedTypeSymbol;
                            if (keyType != null)
                            {
                                var keyNode = new TypeNode 
                                { 
                                    Name = "Key",
                                    FullName = paramNode.DictionaryKeyType,
                                    DomainType = GetDomainType(keyType),
                                    // FIX: IsEnum auch für Dictionary-Key-Nodes setzen
                                    IsEnum = keyType.TypeKind == TypeKind.Enum
                                };
                                queue.Enqueue((keyType, keyNode, "Key"));
                                paramNode.ConstructorParameters.Add(keyNode);
                            }
                        }
                        if (!_typeResolver.IsBaseType(paramNode.DictionaryValueType))
                        {
                            var valueType = _typeResolver.FindTypeByName(paramNode.DictionaryValueType) as INamedTypeSymbol;
                            if (valueType != null)
                            {
                                var valueNode = new TypeNode 
                                { 
                                    Name = "Value",
                                    FullName = paramNode.DictionaryValueType,
                                    DomainType = GetDomainType(valueType),
                                    // FIX: IsEnum auch für Dictionary-Value-Nodes setzen
                                    IsEnum = valueType.TypeKind == TypeKind.Enum
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

        private TypeNode AnalyzeParameterType(ITypeSymbol paramType, string paramName)
        {
            var node = new TypeNode { Name = paramName };
            
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
            // Im Generator wird dann FullName.EndsWith("?") geprüft für nullable Behandlung.
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
            }
            else if (paramType is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var genericDef = namedType.OriginalDefinition.ToDisplayString();
                
                if (genericDef.Contains("Dictionary") || genericDef.Contains("IDictionary"))
                {
                    node.IsDictionary = true;
                    node.DictionaryKeyType = namedType.TypeArguments[0].ToDisplayString();
                    node.DictionaryValueType = namedType.TypeArguments[1].ToDisplayString();
                    node.FullName = paramType.ToDisplayString();
                    Console.WriteLine($"      • {paramName}: Dictionary<{node.DictionaryKeyType}, {node.DictionaryValueType}>");
                }
                else if (genericDef.Contains("List") || genericDef.Contains("IList") || 
                         genericDef.Contains("IEnumerable") || genericDef.Contains("ICollection") ||
                         genericDef.Contains("IReadOnlyList") || genericDef.Contains("IReadOnlyCollection"))
                {
                    node.IsCollection = true;
                    node.CollectionElementType = namedType.TypeArguments[0].ToDisplayString();
                    node.FullName = paramType.ToDisplayString();
                }
                else
                {
                    node.FullName = paramType.ToDisplayString();
                }
            }
            else
            {
                node.FullName = paramType.ToDisplayString();
            }
            
            return node;
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
    }
}